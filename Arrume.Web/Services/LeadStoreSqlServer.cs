using System.Data;
using Arrume.Web.Models;
using Microsoft.Data.SqlClient;

namespace Arrume.Web.Services;

public class LeadStoreSqlServer : ILeadStore
{
    private readonly string _conn;
    private readonly ILogger<LeadStoreSqlServer> _logger;
    public LeadStoreSqlServer(IConfiguration cfg, ILogger<LeadStoreSqlServer> logger)
    {
        _conn = cfg.GetConnectionString("AzureSql")
            ?? cfg["ConnectionStrings:AzureSql"]
            ?? throw new InvalidOperationException("AzureSql ConnectionString não configurada.");
        _logger = logger;
    }

    public async Task<int> SalvarAsync(Lead lead, CancellationToken ct = default)
    {
        const string sql = @"
INSERT INTO dbo.LEADS
(Nome, Telefone, Cep, Logradouro, Bairro, Cidade, Uf, Servico, CriadoEm, Email,
 AceiteContatoWhatsapp, AceiteCompartilhamento, AceiteUso, ConsentTimestampUtc, ConsentIpAddress, ConsentUserAgent, ConsentVersion)
OUTPUT INSERTED.Id
VALUES (@Nome,@Telefone,@Cep,@Logradouro,@Bairro,@Cidade,@Uf,@Servico, SYSUTCDATETIME(), @Email,
        @AceiteContatoWhatsapp,@AceiteCompartilhamento,@AceiteUso,@ConsentTimestampUtc,@ConsentIpAddress,@ConsentUserAgent,@ConsentVersion);
";
        await using var cn = new SqlConnection(_conn);
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, cn);

        cmd.Parameters.Add(new SqlParameter("@Nome", SqlDbType.NVarChar, 200) { Value = lead.Nome });
        cmd.Parameters.Add(new SqlParameter("@Telefone", SqlDbType.NVarChar, 30) { Value = lead.Telefone });
        cmd.Parameters.Add(new SqlParameter("@Cep", SqlDbType.NVarChar, 20) { Value = lead.Cep });
        cmd.Parameters.Add(new SqlParameter("@Logradouro", SqlDbType.NVarChar, 300) { Value = (object?)lead.Logradouro ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Bairro", SqlDbType.NVarChar, 200) { Value = (object?)lead.Bairro ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Cidade", SqlDbType.NVarChar, 200) { Value = lead.Cidade });
        cmd.Parameters.Add(new SqlParameter("@Uf", SqlDbType.NVarChar, 10) { Value = (object?)lead.Uf ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@Servico", SqlDbType.NVarChar, 50) { Value = lead.Servico });
        cmd.Parameters.Add(new SqlParameter("@Email", SqlDbType.NVarChar, 200) { Value = lead.Email });
        cmd.Parameters.Add(new SqlParameter("@AceiteContatoWhatsapp", SqlDbType.Bit) { Value = lead.AceiteContatoWhatsapp });
        cmd.Parameters.Add(new SqlParameter("@AceiteCompartilhamento", SqlDbType.Bit) { Value = lead.AceiteCompartilhamento });
        cmd.Parameters.Add(new SqlParameter("@AceiteUso", SqlDbType.Bit) { Value = lead.AceiteUso });
        cmd.Parameters.Add(new SqlParameter("@ConsentTimestampUtc", SqlDbType.DateTime2) { Value = (object?)lead.ConsentTimestampUtc ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@ConsentIpAddress", SqlDbType.NVarChar, 50) { Value = (object?)lead.ConsentIpAddress ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@ConsentUserAgent", SqlDbType.NVarChar, 500) { Value = (object?)lead.ConsentUserAgent ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@ConsentVersion", SqlDbType.NVarChar, 20) { Value = (object?)lead.ConsentVersion ?? DBNull.Value });

        var id = (int)await cmd.ExecuteScalarAsync(ct);
        _logger.LogInformation("Lead salvo PRD Id={Id}", id);
        return id;
    }
}
