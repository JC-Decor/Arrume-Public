using Arrume.Web.Data;
using Arrume.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Arrume.Web.Services;

public class LeadStoreSqlite : ILeadStore
{
    private readonly DevSqliteDbContext _db;
    public LeadStoreSqlite(DevSqliteDbContext db) => _db = db;

    public async Task<int> SalvarAsync(Lead lead, CancellationToken ct = default)
    {
        _db.Leads.Add(lead);
        await _db.SaveChangesAsync(ct);
        return lead.Id;
    }
}
