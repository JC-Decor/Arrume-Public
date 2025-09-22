using Arrume.Web.Models;
using Microsoft.AspNetCore.Hosting;

namespace Arrume.Web.Services;

public class CapoteiroProviderDevColleagues : ICapoteiroProvider
{
    private readonly IConfiguration _cfg;
    private readonly IWebHostEnvironment _env;

    public CapoteiroProviderDevColleagues(IConfiguration cfg, IWebHostEnvironment env)
    {
        _cfg = cfg;
        _env = env;
    }

    public Task<List<Capoteiro>> BuscarAsync(string cidade, string bairroOuCep, int limite, IEnumerable<string> _categorias)
    {
        if (!_env.IsDevelopment())
            return Task.FromResult(new List<Capoteiro>());

        var colleagues = _cfg.GetSection("Dev:Colleagues").Get<List<Colleague>>() ?? new();

        var lista = colleagues
            .Select((c, i) => new Capoteiro
            {
                Id = 9000 + i,
                Nome = (c.nome ?? $"Colega {i + 1}").Trim(),
                Telefone = DigitsOnly(c.telefone),
                Cidade = (cidade ?? "").Trim(),
                Bairro = (bairroOuCep ?? "").Trim()
            })
            .Where(t => !string.IsNullOrWhiteSpace(t.Telefone))
            .Take(Math.Max(1, limite))
            .ToList();

        return Task.FromResult(lista);
    }

    private static string DigitsOnly(string? s) =>
        new string((s ?? "").Where(char.IsDigit).ToArray());

    private class Colleague
    {
        public string? nome { get; set; }
        public string? telefone { get; set; }
    }
}
