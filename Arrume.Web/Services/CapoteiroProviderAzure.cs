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

    public async Task<List<Capoteiro>> BuscarAsync(string cidade, string bairroOuCep, string uf, string bairroLead, int limite, IEnumerable<string> _)
    {
        var cep = DigitsOnly(bairroOuCep);
        var isCep = cep.Length == 8;

        var lista = isCep
            ? await BuscarPorCepAsync(cidade ?? "", uf ?? "", bairroLead ?? "", cep, limite)
            : await BuscarPorCidadeBairroAsync(cidade ?? "", uf ?? "", bairroOuCep ?? "", limite);

        return lista
            .Where(t => !string.IsNullOrWhiteSpace(t.Telefone))
            .Take(Math.Max(1, limite))
            .ToList();
    }

    private async Task<List<Capoteiro>> BuscarPorCepAsync(string cidade, string uf, string bairroLead, string cep, int limit)
    {
        var sql = BuildBaseSql(
            whereExtra:
@"
    -- Não restringe por cidade; todos da VIEW concorrem
    1 = 1
",
            orderBy:
@"
    -- Ranqueamento de proximidade (CLIENTES)
    CASE
        WHEN REPLACE(REPLACE(ISNULL(c.cep_cliente,''),'-',''),' ','') = @cep THEN 0
        WHEN LEFT(REPLACE(REPLACE(ISNULL(c.cep_cliente,''),'-',''),' ','') , 5) = @cep5 THEN 1
        WHEN LEFT(REPLACE(REPLACE(ISNULL(c.cep_cliente,''),'-',''),' ','') , 4) = @cep4 THEN 2

        -- cidade + bairro EXATOS do lead (tie-break quando muitos têm mesmo prefixo de CEP)
        WHEN (UPPER(LTRIM(RTRIM(ISNULL(c.cidade_cliente,'')))) COLLATE Latin1_General_CI_AI
              = UPPER(LTRIM(RTRIM(@cidade))) COLLATE Latin1_General_CI_AI)
         AND (UPPER(LTRIM(RTRIM(ISNULL(c.bairro_cliente,'')))) COLLATE Latin1_General_CI_AI
              = UPPER(LTRIM(RTRIM(@bairro))) COLLATE Latin1_General_CI_AI) THEN 3

        -- cidade exata + bairro “parecido” (SOUNDEX)
        WHEN (UPPER(LTRIM(RTRIM(ISNULL(c.cidade_cliente,'')))) COLLATE Latin1_General_CI_AI
              = UPPER(LTRIM(RTRIM(@cidade))) COLLATE Latin1_General_CI_AI)
         AND SOUNDEX(LTRIM(RTRIM(ISNULL(c.bairro_cliente,''))))
              = SOUNDEX(LTRIM(RTRIM(@bairro))) THEN 4

        -- cidade exata
        WHEN (UPPER(LTRIM(RTRIM(ISNULL(c.cidade_cliente,'')))) COLLATE Latin1_General_CI_AI
              = UPPER(LTRIM(RTRIM(@cidade))) COLLATE Latin1_General_CI_AI) THEN 5

        -- Preferir mesma UF do lead (se informada)
        WHEN (NULLIF(@uf,'') IS NOT NULL
              AND UPPER(LTRIM(RTRIM(ISNULL(c.uf_cliente,''))))
                  = UPPER(LTRIM(RTRIM(@uf)))) THEN 6
        ELSE 9
    END ASC,

    -- Heurística fina por diferença do prefixo numérico do CEP (regionalidade)
    ABS(TRY_CAST(LEFT(REPLACE(REPLACE(ISNULL(c.cep_cliente,''),'-',''),' ',''),5) AS INT) - @cep5int) ASC,

    -- Preferir quem tem celular
    CASE WHEN NULLIF(REPLACE(REPLACE(c.celular_cliente,'-',''),' ',''),'') IS NULL THEN 1 ELSE 0 END ASC,

    c.id_cliente ASC
");

        var pars = BuildBaseParams(limit);

        var cep5 = (cep.Length >= 5 ? cep[..5] : cep);
        var cep4 = (cep.Length >= 4 ? cep[..4] : cep);

        pars.Add(new SqlParameter("@cep", SqlDbType.NVarChar, 20) { Value = cep });
        pars.Add(new SqlParameter("@cep5", SqlDbType.NVarChar, 20) { Value = cep5 });
        pars.Add(new SqlParameter("@cep4", SqlDbType.NVarChar, 20) { Value = cep4 });
        pars.Add(new SqlParameter("@cidade", SqlDbType.NVarChar, 200) { Value = cidade });
        // >>> agora passamos o bairro do lead (não mais string vazia)
        pars.Add(new SqlParameter("@bairro", SqlDbType.NVarChar, 200) { Value = bairroLead ?? "" });
        pars.Add(new SqlParameter("@uf", SqlDbType.NVarChar, 10) { Value = uf ?? "" });

        int cep5int;
        _ = int.TryParse(new string((cep5 ?? "").Where(char.IsDigit).ToArray()), out cep5int);
        pars.Add(new SqlParameter("@cep5int", SqlDbType.Int) { Value = cep5int });

        return await ExecutarAsync(sql, pars);
    }

    private async Task<List<Capoteiro>> BuscarPorCidadeBairroAsync(string cidade, string uf, string bairro, int limit)
    {
        var sql = BuildBaseSql(
            whereExtra:
@"
    -- Mesma cidade (case/acento-insensitive) e opcionalmente o mesmo bairro
    (UPPER(LTRIM(RTRIM(ISNULL(c.cidade_cliente,'')))) COLLATE Latin1_General_CI_AI
        = UPPER(LTRIM(RTRIM(@cidade))) COLLATE Latin1_General_CI_AI)
",
            orderBy:
@"
    CASE
        WHEN (UPPER(LTRIM(RTRIM(ISNULL(c.bairro_cliente,'')))) COLLATE Latin1_General_CI_AI
              = UPPER(LTRIM(RTRIM(@bairro))) COLLATE Latin1_General_CI_AI) THEN 0
        WHEN SOUNDEX(LTRIM(RTRIM(ISNULL(c.bairro_cliente,''))))
              = SOUNDEX(LTRIM(RTRIM(@bairro))) THEN 1
        ELSE 2
    END ASC,
    -- (opcional) desempate por mesma UF se informada
    CASE WHEN NULLIF(@uf,'') IS NOT NULL
          AND UPPER(LTRIM(RTRIM(ISNULL(c.uf_cliente,'')))) = UPPER(LTRIM(RTRIM(@uf)))
         THEN 0 ELSE 1 END ASC,
    CASE WHEN NULLIF(REPLACE(REPLACE(c.celular_cliente,'-',''),' ',''),'') IS NULL THEN 1 ELSE 0 END ASC,
    c.id_cliente ASC
");

        var pars = BuildBaseParams(limit);
        pars.Add(new SqlParameter("@cidade", SqlDbType.NVarChar, 200) { Value = cidade });
        pars.Add(new SqlParameter("@bairro", SqlDbType.NVarChar, 200) { Value = bairro ?? "" });
        pars.Add(new SqlParameter("@uf", SqlDbType.NVarChar, 10) { Value = uf ?? "" });

        return await ExecutarAsync(sql, pars);
    }

    private string BuildBaseSql(string whereExtra, string orderBy)
    {
        var catConds = new List<string>();
        for (int i = 0; i < _categoriasNorm.Length; i++)
            catConds.Add($"(REPLACE(REPLACE(UPPER(ISNULL(c.categoria_cliente,'')),' ',''),'-','') LIKE '%' + @cat{i} + '%')");

        var sql = $@"
SELECT TOP (@limitFetch)   -- overfetch para garantir TOP 3 com telefone
    c.id_cliente      AS Id,
    c.razao_cliente   AS Nome,
    c.celular_cliente AS Celular,
    c.fone_cliente    AS Fone,
    c.cidade_cliente  AS Cidade,
    c.bairro_cliente  AS Bairro,
    c.cep_cliente     AS Cep
FROM dbo.CLIENTES AS c WITH (NOLOCK)
INNER JOIN dbo.VW_ARRUME_PROFISSIONAIS AS v WITH (NOLOCK)
        ON v.id_cliente = c.id_cliente
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

        var limitFetch = Math.Max(limit * 8, limit + 10);
        pars.Add(new SqlParameter("@limit", SqlDbType.Int) { Value = limit });
        pars.Add(new SqlParameter("@limitFetch", SqlDbType.Int) { Value = limitFetch });
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
                var cel  = DigitsOnly(rd["Celular"] as string ?? "");
                var fone = DigitsOnly(rd["Fone"] as string ?? "");
                var tel  = !string.IsNullOrWhiteSpace(cel) ? cel : fone;

                if (!string.IsNullOrWhiteSpace(tel) && !tel.StartsWith("55") && (tel.Length is 10 or 11))
                    tel = "55" + tel;

                lista.Add(new Capoteiro
                {
                    Id       = Convert.ToInt32(rd["Id"]),
                    Nome     = rd["Nome"] as string ?? "",
                    Telefone = tel,
                    Cidade   = rd["Cidade"] as string ?? "",
                    Bairro   = rd["Bairro"] as string ?? "",
                    Cep      = rd["Cep"] as string ?? ""
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao consultar CLIENTES/VW_ARRUME_PROFISSIONAIS");
        }
        return lista;
    }

    private static string DigitsOnly(string s) =>
        new string((s ?? "").Where(char.IsDigit).ToArray());

    private static string NormalizeCat(string? s) =>
        (s ?? "").ToUpperInvariant().Replace(" ", "").Replace("-", "");
}
