using Arrume.Web.Models;

namespace Arrume.Web.Services;

public class ZApiFakeService : IZApiService
{
    private readonly ILogger<ZApiFakeService> _logger;
    private readonly ILinkShortenerService _linkShortener;

    public ZApiFakeService(ILogger<ZApiFakeService> logger, ILinkShortenerService linkShortener)
    {
        _logger = logger;
        _linkShortener = linkShortener;
    }

    public async Task EnviarMensagemClienteAsync(string telefoneCliente, IEnumerable<Capoteiro> capoteiros)
    {
        var telefoneFormatado = FormatTelefoneDisplay(telefoneCliente);
        _logger.LogInformation("[FAKE-ZAPI] Cliente:{cli} -> {qtde} indicações",
            telefoneFormatado, (capoteiros ?? Enumerable.Empty<Capoteiro>()).Count());
        
        foreach (var t in capoteiros ?? Enumerable.Empty<Capoteiro>())
        {
            var telForn = FormatTelefoneDisplay(t.Telefone);
            var longLink = GerarLinkWhatsApp(t.Telefone, $"Olá {t.Nome}, vi seu contato através da JC Decor/ARRUME");
            var shortLink = await _linkShortener.ShortenUrlAsync(longLink);
            
            _logger.LogInformation("[FAKE-ZAPI]   → {nome} - {telefone} | Link: {link}", 
                t.Nome, telForn, shortLink);
        }
    }

    public async Task EnviarMensagemCapoteiroAsync(string telefoneCapoteiro, Lead lead)
    {
        var telCapoteiro = FormatTelefoneDisplay(telefoneCapoteiro);
        var telCliente = FormatTelefoneDisplay(lead.Telefone);
        var longLink = GerarLinkWhatsApp(lead.Telefone, $"Olá {lead.Nome}, sou o profissional indicado pela JC Decor");
        var shortLink = await _linkShortener.ShortenUrlAsync(longLink);
        
        _logger.LogInformation("[FAKE-ZAPI] Capoteiro:{tel} ← Lead:{nome} ({whats}) | Cidade:{cidade}/{uf} | Link: {link}",
            telCapoteiro, lead.Nome, telCliente, lead.Cidade, lead.Uf, shortLink);
    }

    private static string FormatTelefoneDisplay(string telefone)
    {
        if (string.IsNullOrWhiteSpace(telefone)) return "(sem telefone)";
        
        var digits = new string(telefone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("55") && digits.Length >= 12)
        {
            var ddd = digits.Substring(2, 2);
            var numero = digits.Substring(4);
            return numero.Length == 8 ? $"({ddd}) {numero.Substring(0, 4)}-{numero.Substring(4)}" 
                 : numero.Length == 9 ? $"({ddd}) {numero.Substring(0, 5)}-{numero.Substring(5)}" 
                 : telefone;
        }
        return telefone;
    }

    private static string GerarLinkWhatsApp(string telefone, string mensagem)
    {
        if (string.IsNullOrWhiteSpace(telefone)) return "#";
        var digits = new string(telefone.Where(char.IsDigit).ToArray());
        return $"https://wa.me/{digits}?text={Uri.EscapeDataString(mensagem)}";
    }
}