using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Arrume.Web.Models;
using Microsoft.Extensions.Options;

namespace Arrume.Web.Services;

public class ZApiService : IZApiService
{
    private readonly HttpClient _http;
    private readonly ZApiOptions _opcoes;
    private readonly string _urlEnvio;
    private readonly ILogger<ZApiService> _logger;
    private readonly ILinkShortenerService _linkShortener;

    public ZApiService(
        HttpClient http, 
        IOptions<ZApiOptions> opcoes, 
        ILogger<ZApiService> logger,
        ILinkShortenerService linkShortener)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _opcoes = opcoes?.Value ?? new ZApiOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _linkShortener = linkShortener;

        var endpoint = _opcoes.MessageEndpoint?.Trim('/') ?? "send-text";
        var instance = _opcoes.Instance ?? string.Empty;
        var token = _opcoes.Token ?? string.Empty;

        _urlEnvio = $"/instances/{instance}/token/{token}/{endpoint}";
        if (_http.BaseAddress is null && !string.IsNullOrWhiteSpace(_opcoes.UrlBase))
            _http.BaseAddress = new Uri(_opcoes.UrlBase);
    }

    public async Task EnviarMensagemClienteAsync(string telefoneCliente, IEnumerable<Capoteiro> capoteiros)
    {
        capoteiros ??= Array.Empty<Capoteiro>();

        var sb = new StringBuilder();
        sb.AppendLine("🏠 *JC Decor - Indicação de Profissionais*");
        sb.AppendLine();
        sb.AppendLine("Olá! Recebemos seu pedido na plataforma *ARRUME* e selecionamos os melhores profissionais para atender sua necessidade:");
        sb.AppendLine();

        int i = 1;
        foreach (var t in capoteiros)
        {
            var telefoneFormatado = FormatTelefoneDisplay(t.Telefone);
            var longLink = GerarLinkWhatsApp(t.Telefone, 
                $"Olá {t.Nome}, vi seu contato através da indicação da JC Decor pela plataforma ARRUME. Gostaria de solicitar um orçamento.");
            
            var shortLink = await _linkShortener.ShortenUrlAsync(longLink);
            
            sb.AppendLine($"*{i}. {t.Nome}*");
            sb.AppendLine($"📞 {telefoneFormatado}");
            sb.AppendLine($"💬 {shortLink}");
            sb.AppendLine();
            i++;
        }

        sb.AppendLine("💡 *Dica*: Clique nos links acima para iniciar a conversa diretamente com cada profissional.");
        sb.AppendLine();
        sb.AppendLine("Agradecemos sua confiança! 🙏");
        sb.AppendLine("*JC Decor - Plataforma ARRUME*");

        await EnviarWhatsAppAsync(telefoneCliente, sb.ToString(), "cliente");
    }

    public async Task EnviarMensagemCapoteiroAsync(string telefoneCapoteiro, Lead lead)
    {
        var longLink = GerarLinkWhatsApp(lead.Telefone,
            $"Olá {lead.Nome}, sou o profissional indicado pela JC Decor através da plataforma ARRUME. Entendi que você tem interesse em {FriendlyServicoLabel(lead.Servico)}. Como posso ajudá-lo?");

        var shortLink = await _linkShortener.ShortenUrlAsync(longLink);

        var texto = new StringBuilder();
        texto.AppendLine("🎯 *JC Decor - Novo Lead Disponível*");
        texto.AppendLine();
        texto.AppendLine("Temos uma nova oportunidade de negócio para você através da plataforma *ARRUME*:");
        texto.AppendLine();
        texto.AppendLine($"*Nome:* {lead.Nome}");
        texto.AppendLine($"*Contato:* {FormatTelefoneDisplay(lead.Telefone)}");
        if (!string.IsNullOrWhiteSpace(lead.Email)) 
            texto.AppendLine($"*Email:* {lead.Email}");
        texto.AppendLine($"*Cidade:* {lead.Cidade} / *UF:* {lead.Uf}");
        if (!string.IsNullOrWhiteSpace(lead.Bairro)) 
            texto.AppendLine($"*Bairro:* {lead.Bairro}");
        texto.AppendLine($"*CEP:* {FormatCep(lead.Cep)}");
        texto.AppendLine($"*Serviço solicitado:* {FriendlyServicoLabel(lead.Servico)}");
        texto.AppendLine();
        texto.AppendLine($"💬 {shortLink}");
        texto.AppendLine();
        texto.AppendLine("💡 *Dica*: Clique nos links acima para iniciar a conversa diretamente com o cliente.");
        texto.AppendLine("⏰ *Recomendamos o contato imediato para melhor aproveitamento da oportunidade.*");
        texto.AppendLine();
        texto.AppendLine("Atenciosamente,");
        texto.AppendLine("*JC Decor - Plataforma ARRUME*");

        await EnviarWhatsAppAsync(telefoneCapoteiro, texto.ToString(), "capoteiro");
    }

    private static string FriendlyServicoLabel(string serv)
    {
        if (string.IsNullOrWhiteSpace(serv)) return "Não informado";
        serv = serv.Trim().ToLowerInvariant();
        return serv switch
        {
            "reforma" => "Reforma de Estofados",
            "novo"    => "Estofado Novo",
            "ambos"   => "Reforma e Estofado Novo",
            _         => serv
        };
    }

    private static string FormatTelefoneDisplay(string telefone)
    {
        if (string.IsNullOrWhiteSpace(telefone)) return "(telefone não disponível)";
        
        var digits = new string(telefone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("55") && digits.Length >= 12)
        {
            var ddd = digits.Substring(2, 2);
            var numero = digits.Substring(4);
            
            if (numero.Length == 8)
                return $"({ddd}) {numero.Substring(0, 4)}-{numero.Substring(4)}";
            else if (numero.Length == 9)
                return $"({ddd}) {numero.Substring(0, 5)}-{numero.Substring(5)}";
        }
        
        return telefone;
    }

    private static string FormatCep(string cep)
    {
        if (string.IsNullOrWhiteSpace(cep) || cep.Length != 8) return cep ?? "";
        return $"{cep.Substring(0, 5)}-{cep.Substring(5)}";
    }

    private static string GerarLinkWhatsApp(string telefone, string mensagem)
    {
        if (string.IsNullOrWhiteSpace(telefone)) return "#";
        
        var digits = new string(telefone.Where(char.IsDigit).ToArray());
        var mensagemCodificada = Uri.EscapeDataString(mensagem);
        
        return $"https://wa.me/{digits}?text={mensagemCodificada}";
    }

    private async Task EnviarWhatsAppAsync(string telefone, string mensagem, string destino)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.Instance) || string.IsNullOrWhiteSpace(_opcoes.Token))
        {
            _logger.LogWarning("ZAPI({Destino}): instância/token não configurados. Mensagem não enviada.", destino);
            return;
        }

        var phone = NormalizePhone(telefone);

        if (!string.IsNullOrWhiteSpace(_opcoes.SenderPhone) && phone == _opcoes.SenderPhone)
        {
            _logger.LogWarning("ZAPI({Destino}): evitando envio para o próprio número da instância ({Phone}).", destino, phone);
            return;
        }

        var payload = new { phone = phone, message = mensagem };
        var payloadJson = JsonSerializer.Serialize(payload);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, _urlEnvio)
            {
                Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(_opcoes.ClientToken))
                req.Headers.TryAddWithoutValidation("Client-Token", _opcoes.ClientToken.Trim());

            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("Arrume", "1.0"));

            var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            var body = await res.Content.ReadAsStringAsync();

            if (res.IsSuccessStatusCode)
            {
                _logger.LogInformation("ZAPI({Destino})-> OK {Status} | body: {Body}", destino, (int)res.StatusCode, SafeTrunc(body, 500));
                TryLogMessageIds(body);
                return;
            }

            _logger.LogWarning("ZAPI({Destino})-> FAIL {Status} | body: {Body}", destino, (int)res.StatusCode, SafeTrunc(body, 1000));

            if (res.StatusCode == System.Net.HttpStatusCode.BadRequest &&
                body.IndexOf("client-token", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _logger.LogWarning("ZAPI({Destino}): verifique o 'Client-Token' habilitado na sua instância.", destino);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ZAPI({Destino}) -> exceção ao enviar.", destino);
        }
    }

    private static string NormalizePhone(string? raw)
    {
        var digits = new string((raw ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length is 10 or 11 && !digits.StartsWith("55")) digits = "55" + digits;
        if (digits.Length > 13) digits = digits[..13];
        return digits;
    }

    private static void TryLogMessageIds(string? responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return;
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            string? messageId = TryGetString(root, "messageId") ??
                                TryGetString(root, "message_id") ??
                                TryGetString(root, "id");
            string? zaapId = TryGetString(root, "zaapId") ??
                             TryGetString(root, "zaap_id");

            if (!string.IsNullOrWhiteSpace(messageId) || !string.IsNullOrWhiteSpace(zaapId))
                Console.WriteLine($"ZAPI -> envio confirmado. messageId={messageId ?? "(n/d)"}, zaapId={zaapId ?? "(n/d)"}");
        }
        catch { /* ignore */ }

        static string? TryGetString(JsonElement el, string prop)
            => el.ValueKind == JsonValueKind.Object &&
               el.TryGetProperty(prop, out var p) &&
               p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    }

    private static string SafeTrunc(string s, int max) => s.Length > max ? s[..max] + "..." : s;
}