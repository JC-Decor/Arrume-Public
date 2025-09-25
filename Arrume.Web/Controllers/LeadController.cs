using System.Text.RegularExpressions;
using System.Text.Json;
using System.Collections.Generic;
using Arrume.Web.Models;
using Arrume.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Arrume.Web.Controllers;

[Route("lead")]
public class LeadController : Controller
{
    private readonly ILeadStore _leadStore;
    private readonly ICapoteiroProvider _capoteiro;
    private readonly IZApiService _zapi;
    private readonly IConfiguration _cfg;
    private readonly ILogger<LeadController> _logger;
    private readonly int _limit;

    public LeadController(
        ILeadStore leadStore,
        ICapoteiroProvider capoteiro,
        IZApiService zapi,
        IConfiguration cfg,
        ILogger<LeadController> logger)
    {
        _leadStore = leadStore;
        _capoteiro = capoteiro;
        _zapi = zapi;
        _cfg = cfg;
        _logger = logger;
        _limit = Math.Max(1, _cfg.GetValue<int>("Capoteiros:Limit", 3));
    }

    [HttpPost("submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit([FromForm] Lead lead)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object?>{
            ["TraceId"] = HttpContext.TraceIdentifier
        });

        _logger.LogInformation("Lead recebido: Nome={Nome}, TelLen={TelLen}, CEP={CEP}",
            (lead.Nome ?? "").Trim(),
            (lead.Telefone ?? "").Length,
            (lead.Cep ?? "").Trim());

        NormalizeLead(lead);

        ReadAndApplyCheckbox(nameof(lead.AceiteContatoWhatsapp), v =>
        {
            lead.AceiteContatoWhatsapp = v;
            if (!v) ModelState.AddModelError(nameof(lead.AceiteContatoWhatsapp), "É necessário autorizar o contato via WhatsApp.");
        });
        ReadAndApplyCheckbox(nameof(lead.AceiteCompartilhamento), v =>
        {
            lead.AceiteCompartilhamento = v;
            if (!v) ModelState.AddModelError(nameof(lead.AceiteCompartilhamento), "É necessário autorizar o compartilhamento dos dados.");
        });
        ReadAndApplyCheckbox(nameof(lead.AceiteUso), v =>
        {
            lead.AceiteUso = v;
            if (!v) ModelState.AddModelError(nameof(lead.AceiteUso), "É necessário autorizar o uso das informações para coordenação do serviço.");
        });

        await TryPopulateFromCepAsync(lead);

        ModelState.Clear();
        TryValidateModel(lead);

        if (!lead.AceiteContatoWhatsapp)
            ModelState.AddModelError(nameof(lead.AceiteContatoWhatsapp), "É necessário autorizar o contato via WhatsApp.");
        if (!lead.AceiteCompartilhamento)
            ModelState.AddModelError(nameof(lead.AceiteCompartilhamento), "É necessário autorizar o compartilhamento dos dados.");
        if (!lead.AceiteUso)
            ModelState.AddModelError(nameof(lead.AceiteUso), "É necessário autorizar o uso das informações para coordenação do serviço.");

        if (!ModelState.IsValid)
            return View("~/Views/Home/Index.cshtml", lead);

        lead.ConsentTimestampUtc = DateTime.UtcNow;
        lead.ConsentIpAddress = GetIp();
        lead.ConsentUserAgent = Request.Headers["User-Agent"].ToString();

        try
        {
            await _leadStore.SalvarAsync(lead, HttpContext.RequestAborted);
            _logger.LogInformation("Lead salvo com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao salvar lead");
            ModelState.AddModelError(string.Empty, "Erro interno ao salvar seus dados. Tente novamente em instantes.");
            return View("~/Views/Home/Index.cshtml", lead);
        }

        var categorias = _cfg.GetSection("AzureSql:CategoriaClienteIds").Get<string[]>() ?? Array.Empty<string>();

        _logger.LogInformation("Buscando fornecedores: Cidade={Cidade}, UF={Uf}, CEP={Cep}, Bairro={Bairro}, Limite={Limite}",
            lead.Cidade, lead.Uf, lead.Cep, lead.Bairro, _limit);

        List<Capoteiro> forns = new();
        try
        {
            if (!string.IsNullOrWhiteSpace(lead.Cep) && lead.Cep.Length == 8)
            {
                // CEP + UF + BAIRRO do lead para tie-break
                forns = await _capoteiro.BuscarAsync(lead.Cidade, lead.Cep, lead.Uf, lead.Bairro, _limit, categorias);
            }

            if ((forns == null || forns.Count == 0))
            {
                var bairroBusca = lead.Bairro ?? string.Empty;
                // Bairro (2º arg) + UF + mesmo bairro (3º arg) como bairroLead
                forns = await _capoteiro.BuscarAsync(lead.Cidade, bairroBusca, lead.Uf, bairroBusca, _limit, categorias);
            }

            if ((forns == null || forns.Count == 0))
            {
                // Sem filtro de bairro/cep
                forns = await _capoteiro.BuscarAsync(lead.Cidade, string.Empty, lead.Uf, lead.Bairro ?? string.Empty, _limit, categorias);
            }

            _logger.LogInformation("Fornecedores encontrados: {Qtde}", forns?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao buscar fornecedores");
            forns = new();
        }

        var tasks = new List<Task> { _zapi.EnviarMensagemClienteAsync(lead.Telefone, forns) };
        foreach (var f in forns)
            tasks.Add(_zapi.EnviarMensagemCapoteiroAsync(f.Telefone, lead));

        try 
        { 
            await Task.WhenAll(tasks);
            _logger.LogInformation("Envios WhatsApp concluídos: cliente + {QtdeFornecedores}", forns.Count);
        }
        catch (Exception ex) 
        { 
            _logger.LogWarning(ex, "Falha em envio WhatsApp");
        }

        return RedirectToAction("Obrigado", "Home");
    }

    // ==== helpers (1x cada) ====

    private void ReadAndApplyCheckbox(string key, Action<bool> apply)
    {
        var values = Request.Form[key];
        var isTrue = values.Any(v =>
        {
            var t = (v ?? "").Trim().ToLowerInvariant();
            return t is "true" or "on" or "true,false" or "false,true";
        });
        apply(isTrue);
    }

    private string GetIp()
    {
        var ip = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        return string.IsNullOrWhiteSpace(ip)
            ? HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            : ip;
    }

    private static void NormalizeLead(Lead lead)
    {
        static string Digits(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());
        static string Trunc(string s, int max) => s.Length > max ? s[..max] : s;

        static string Clean(string? s, int max)
        {
            var sanitized = Regex.Replace(s ?? "", @"[<>""'&;\\]", "").Trim();
            return Trunc(sanitized, max);
        }
        static string Email(string? e)
        {
            var v = (e ?? "").Trim().ToLowerInvariant();
            if (!Regex.IsMatch(v, @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$")) return "";
            return Trunc(v, 200);
        }
        static string Serv(string? s)
        {
            var v = (s ?? "ambos").Trim().ToLowerInvariant();
            return v is "reforma" or "novo" or "ambos" ? v : "ambos";
        }

        lead.Nome = Clean(lead.Nome, 200);
        lead.Email = Email(lead.Email);

        var tel = Digits(lead.Telefone);
        if (tel.Length is 10 or 11 && !tel.StartsWith("55")) tel = "55" + tel;
        lead.Telefone = Trunc(tel, 13);

        var cep = Digits(lead.Cep);
        lead.Cep = Trunc(cep, 8);

        lead.Bairro = Clean(lead.Bairro, 200);
        lead.Cidade = Clean(lead.Cidade, 200);
        lead.Uf = Clean(lead.Uf, 2);
        lead.Logradouro = Clean(lead.Logradouro, 300);
        lead.Servico = Serv(lead.Servico);
    }

    private async Task TryPopulateFromCepAsync(Lead lead)
    {
        if (lead.Cep?.Length == 8 && (string.IsNullOrWhiteSpace(lead.Cidade) || string.IsNullOrWhiteSpace(lead.Uf)))
        {
            var ok = await TryViaCep(lead);
            if (!ok) ok = await TryBrasilApi(lead);
            if (!ok && string.IsNullOrWhiteSpace(lead.Cidade))
            {
                lead.Cidade = $"CEP-{lead.Cep}";
                lead.Uf ??= "";
            }
        }
    }

    private async Task<bool> TryViaCep(Lead lead)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var url = $"https://viacep.com.br/ws/{lead.Cep}/json/";
            using var resp = await http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return false;
            var json = await resp.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("erro", out var erro) && erro.ValueKind == JsonValueKind.True)
                return false;

            if (string.IsNullOrWhiteSpace(lead.Cidade) && root.TryGetProperty("localidade", out var c))
                lead.Cidade = c.GetString() ?? lead.Cidade;

            if (string.IsNullOrWhiteSpace(lead.Uf) && root.TryGetProperty("uf", out var u))
                lead.Uf = u.GetString() ?? lead.Uf;

            if (string.IsNullOrWhiteSpace(lead.Bairro) && root.TryGetProperty("bairro", out var b))
                lead.Bairro = b.GetString() ?? lead.Bairro;

            if (string.IsNullOrWhiteSpace(lead.Logradouro) && root.TryGetProperty("logradouro", out var l))
                lead.Logradouro = l.GetString() ?? lead.Logradouro;

            return !string.IsNullOrWhiteSpace(lead.Cidade);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ViaCEP server-side falhou");
            return false;
        }
    }

    private async Task<bool> TryBrasilApi(Lead lead)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var url = $"https://brasilapi.com.br/api/cep/v1/{lead.Cep}";
            using var resp = await http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return false;
            var json = await resp.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("errors", out _))
                return false;

            if (string.IsNullOrWhiteSpace(lead.Cidade) && root.TryGetProperty("city", out var c))
                lead.Cidade = c.GetString() ?? lead.Cidade;

            if (string.IsNullOrWhiteSpace(lead.Uf) && root.TryGetProperty("state", out var u))
                lead.Uf = u.GetString() ?? lead.Uf;

            if (string.IsNullOrWhiteSpace(lead.Bairro) && root.TryGetProperty("neighborhood", out var b))
                lead.Bairro = b.GetString() ?? lead.Bairro;

            if (string.IsNullOrWhiteSpace(lead.Logradouro) && root.TryGetProperty("street", out var l))
                lead.Logradouro = l.GetString() ?? lead.Logradouro;

            return !string.IsNullOrWhiteSpace(lead.Cidade);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "BrasilAPI server-side falhou");
            return false;
        }
    }
}
