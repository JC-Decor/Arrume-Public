using System.Text;
using System.Text.Json;

namespace Arrume.Web.Services;

public class TinyUrlService : ILinkShortenerService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TinyUrlService> _logger;

    public TinyUrlService(HttpClient httpClient, ILogger<TinyUrlService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress = new Uri("https://api.tinyurl.com/");
    }

    public async Task<string> ShortenUrlAsync(string longUrl)
    {
        try
        {

            var response = await _httpClient.GetAsync($"create?url={Uri.EscapeDataString(longUrl)}");
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("tiny_url", out var tinyUrl))
                {
                    return tinyUrl.GetString() ?? longUrl;
                }
            }


            return await ShortenWithIsGdAsync(longUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao encurtar URL: {Url}", longUrl);
            return longUrl;
        }
    }

    private async Task<string> ShortenWithIsGdAsync(string longUrl)
    {
        try
        {
            var url = $"https://is.gd/create.php?format=simple&url={Uri.EscapeDataString(longUrl)}";
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var shortUrl = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(shortUrl) && shortUrl.StartsWith("http"))
                    return shortUrl.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha com is.gd");
        }
        
        return longUrl;
    }
}