using Arrume.Web.Models;

namespace Arrume.Web.Services;

public interface ICapoteiroProvider
{
    Task<List<Capoteiro>> BuscarAsync(string cidade, string bairroOuCep, int limite, IEnumerable<string> categorias);
}
