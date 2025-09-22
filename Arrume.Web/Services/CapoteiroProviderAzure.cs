using System.Data;
using Arrume.Web.Models;
using Microsoft.Data.SqlClient;

namespace Arrume.Web.Services;

public class CapoteiroProviderAzure : ICapoteiroProvider
{
    private readonly string _conn;
    private readonly ILogger<CapoteiroProviderAzure> _logger;
    private readonly string[] _categoriasNorm;

    public CapoteiroProviderAzure(IConfiguration cfg, ILogger<CapoteiroProviderAzure> logger)
    {
        _conn = cfg.GetConnectionString("AzureSql")
            ?? cfg["ConnectionStrings:AzureSql"]
            ?? throw new InvalidOperationException("AzureSql ConnectionString não configurada.");
        _logger = logger;

        var cats = cfg.GetSection("AzureSql:CategoriaClienteIds").Get<string[]>() ?? Array.Empty<string>();
        _categoriasNorm = cats.Select(NormalizeCat).ToArray();
    }

    public async Task<List<Capoteiro>> BuscarAsync(string cidade, string bairroOuCep, int limite, IEnumerable<string> _)
    {
        var cep = DigitsOnly(bairroOuCep);
        var isCep = cep.Length == 8;
        var lista = isCep
            ? await BuscarPorCepAsync(cidade ?? "", cep, limite)
            : await BuscarPorCidadeBairroAsync(cidade ?? "", bairroOuCep ?? "", limite);

        return lista
            .Where(t => !string.IsNullOrWhiteSpace(t.Telefone))
            .Take(Math.Max(1, limite))
            .ToList();
    }

    private async Task<List<Capoteiro>> BuscarPorCepAsync(string cidade, string cep, int limit)
    {
        var sql = BuildBaseSql(whereExtra:
@"
    -- Filtro por CEP e/ou cidade
    (
        REPLACE(REPLACE(ISNULL(cep_cliente,''),'-',''),' ','') = @cep
        OR LEFT(REPLACE(REPLACE(ISNULL(cep_cliente,''),'-',''),' ','') , 5) = @cep5
        OR (UPPER(LTRIM(RTRIM(ISNULL(cidade_cliente,'')))) COLLATE Latin1_General_CI_AI
            = UPPER(LTRIM(RTRIM(@cidade))) COLLATE Latin1_General_CI_AI)
    )
",
        orderBy:
@"
    -- Ordena pela maior proximidade: CEP exato -> prefixo(5) -> prefixo(4) -> mesma cidade+bairro (exato) -> mesma cidade+bairro (soundex) -> mesma cidade -> demais
    CASE
        WHEN REPLACE(REPLACE(ISNULL(cep_cliente,''),'-',''),' ','') = @cep THEN 0
        WHEN LEFT(REPLACE(REPLACE(ISNULL(cep_cliente,''),'-',''),' ','') , 5) = @cep5 THEN 1
        WHEN LEFT(REPLACE(REPLACE(ISNULL(cep_cliente,''),'-',''),' ','') , 4) = @cep4 THEN 2
        WHEN (UPPER(LTRIM(RTRIM(ISNULL(cidade_cliente,'')))) COLLATE Latin1_General_CI_AI
              = UPPER(LTRIM(RTRIM(@cidade))) COLLATE Latin1_General_CI_AI)
         AND (UPPER(LTRIM(RTRIM(ISNULL(bairro_cliente,'')))) COLLATE Latin1_General_CI_AI
              = UPPER(LTRIM(RTRIM(@bairro))) COLLATE Latin1_General_CI_AI) THEN 3
        WHEN (UPPER(LTRIM(RTRIM(ISNULL(cidade_cliente,'')))) COLLATE Latin1_General_CI_AI
              = UPPER(LTRIM(RTRIM(@cidade))) COLLATE Latin1_General_CI_AI)
         AND SOUNDEX(LTRIM(RTRIM(ISNULL(bairro_cliente,''))))
              = SOUNDEX(LTRIM(RTRIM(@bairro))) THEN 4
        WHEN (UPPER(LTRIM(RTRIM(ISNULL(cidade_cliente,'')))) COLLATE Latin1_General_CI_AI
              = UPPER(LTRIM(RTRIM(@cidade))) COLLATE Latin1_General_CI_AI) THEN 5
        ELSE 9
    END ASC,
    -- Preferir quem tem celular
    CASE WHEN NULLIF(REPLACE(REPLACE(celular_cliente,'-',''),' ',''),'') IS NULL THEN 1 ELSE 0 END ASC,
    id_cliente ASC
");

        var pars = BuildBaseParams(limit);
        pars.Add(new SqlParameter("@cep", SqlDbType.NVarChar, 20) { Value = cep });
        pars.Add(new SqlParameter("@cep5", SqlDbType.NVarChar, 20) { Value = cep.Length >= 5 ? cep[..5] : cep });
        pars.Add(new SqlParameter("@cep4", SqlDbType.NVarChar, 20) { Value = cep.Length >= 4 ? cep[..4] : cep });
        pars.Add(new SqlParameter("@cidade", SqlDbType.NVarChar, 200) { Value = cidade });
        pars.Add(new SqlParameter("@bairro", SqlDbType.NVarChar, 200) { Value = "" });

        return await ExecutarAsync(sql, pars);
    }

    private async Task<List<Capoteiro>> BuscarPorCidadeBairroAsync(string cidade, string bairro, int limit)
    {
        var sql = BuildBaseSql(whereExtra:
@"
    -- Mesma cidade (case/acento-insensitive) e opcionalmente o mesmo bairro
    (UPPER(LTRIM(RTRIM(ISNULL(cidade_cliente,'')))) COLLATE Latin1_General_CI_AI
        = UPPER(LTRIM(RTRIM(@cidade))) COLLATE Latin1_General_CI_AI)
",
        orderBy:
@"
    CASE
        WHEN (UPPER(LTRIM(RTRIM(ISNULL(bairro_cliente,'')))) COLLATE Latin1_General_CI_AI
              = UPPER(LTRIM(RTRIM(@bairro))) COLLATE Latin1_General_CI_AI) THEN 0
        WHEN SOUNDEX(LTRIM(RTRIM(ISNULL(bairro_cliente,''))))
              = SOUNDEX(LTRIM(RTRIM(@bairro))) THEN 1
        ELSE 2
    END ASC,
    CASE WHEN NULLIF(REPLACE(REPLACE(celular_cliente,'-',''),' ',''),'') IS NULL THEN 1 ELSE 0 END ASC,
    id_cliente ASC
");

        var pars = BuildBaseParams(limit);
        pars.Add(new SqlParameter("@cidade", SqlDbType.NVarChar, 200) { Value = cidade });
        pars.Add(new SqlParameter("@bairro", SqlDbType.NVarChar, 200) { Value = bairro ?? "" });

        return await ExecutarAsync(sql, pars);
    }

    private string BuildBaseSql(string whereExtra, string orderBy)
    {
        var catConds = new List<string>();
        for (int i = 0; i < _categoriasNorm.Length; i++)
        {
            catConds.Add($"(REPLACE(REPLACE(UPPER(ISNULL(categoria_cliente,'')),' ',''),'-','') LIKE '%' + @cat{i} + '%')");
        }

        var sql = $@"
SELECT TOP (@limit)
    id_cliente AS Id,
    razao_cliente AS Nome,
    celular_cliente AS Celular,
    fone_cliente AS Fone,
    cidade_cliente AS Cidade,
    bairro_cliente AS Bairro,
    cep_cliente AS Cep
FROM dbo.CLIENTES WITH (NOLOCK)
WHERE
    ({string.Join(" OR ", catConds)})
    AND ({whereExtra})
ORDER BY
    {orderBy};
";
        return sql;
    }

    private List<SqlParameter> BuildBaseParams(int limit)
    {
        var pars = new List<SqlParameter>();
        for (int i = 0; i < _categoriasNorm.Length; i++)
            pars.Add(new SqlParameter($"@cat{i}", SqlDbType.NVarChar, 200) { Value = _categoriasNorm[i] });
        pars.Add(new SqlParameter("@limit", SqlDbType.Int) { Value = limit });
        return pars;
    }

    private async Task<List<Capoteiro>> ExecutarAsync(string sql, List<SqlParameter> pars)
    {
        var lista = new List<Capoteiro>();
        try
        {
            await using var cn = new SqlConnection(_conn);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddRange(pars.ToArray());
            await using var rd = await cmd.ExecuteReaderAsync();

            while (await rd.ReadAsync())
            {
                var cel = DigitsOnly(rd["Celular"] as string ?? "");
                var fone = DigitsOnly(rd["Fone"] as string ?? "");
                var tel = !string.IsNullOrWhiteSpace(cel) ? cel : fone;
                if (!string.IsNullOrWhiteSpace(tel) && !tel.StartsWith("55") && (tel.Length is 10 or 11))
                    tel = "55" + tel;

                lista.Add(new Capoteiro
                {
                    Id = Convert.ToInt32(rd["Id"]),
                    Nome = rd["Nome"] as string ?? "",
                    Telefone = tel,
                    Cidade = rd["Cidade"] as string ?? "",
                    Bairro = rd["Bairro"] as string ?? "",
                    Cep = rd["Cep"] as string ?? ""
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao consultar CLIENTES");
        }
        return lista;
    }

    private static string DigitsOnly(string s) => new string((s ?? "").Where(char.IsDigit).ToArray());

    private static string NormalizeCat(string? s)
        => (s ?? "").ToUpperInvariant().Replace(" ", "").Replace("-", "");
}
