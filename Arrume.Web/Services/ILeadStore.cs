using Arrume.Web.Models;

namespace Arrume.Web.Services;

public interface ILeadStore
{
    Task<int> SalvarAsync(Lead lead, CancellationToken ct = default);
}
