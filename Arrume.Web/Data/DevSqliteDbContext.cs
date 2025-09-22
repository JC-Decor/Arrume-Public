using Arrume.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Arrume.Web.Data;

public class DevSqliteDbContext : DbContext
{
    public DevSqliteDbContext(DbContextOptions<DevSqliteDbContext> options) : base(options) { }

    public DbSet<Lead> Leads => Set<Lead>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Lead>(e =>
        {
            e.ToTable("LEADS");
            e.HasKey(x => x.Id);
            e.Property(x => x.CriadoEm).HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.HasIndex(x => x.Cidade);
        });
    }
}
