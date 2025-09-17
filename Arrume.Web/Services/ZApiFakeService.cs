using Arrume.Web.Models;

namespace Arrume.Web.Services;

public class ZApiFakeService : IZApiService
{
    private readonly ILogger<ZApiFakeService> _logger;
    public ZApiFakeService(ILogger<ZApiFakeService> logger) => _logger = logger;

    public Task EnviarMensagemClienteAsync(string telefoneCliente, IEnumerable<Capoteiro> capoteiros)
    {
        _logger.LogInformation("[FAKE-ZAPI] Cliente:{cli} -> {msg}",
            telefoneCliente,
            string.Join(" | ", (capoteiros ?? Enumerable.Empty<Capoteiro>())
                .Select((t, i) => $"{i + 1}. {t.Nome} - {t.Telefone}")));
        return Task.CompletedTask;
    }

    public Task EnviarMensagemCapoteiroAsync(string telefoneCapoteiro, Lead lead)
    {
        _logger.LogInformation("[FAKE-ZAPI] Capoteiro:{tel} <- Lead:{nome}/{whats}/{cidade}/{cep}",
            telefoneCapoteiro, lead.Nome, lead.Telefone, lead.Cidade, lead.Cep);
        return Task.CompletedTask;
    }
}
