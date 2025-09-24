namespace Arrume.Web.Services;

public interface ILinkShortenerService
{
    Task<string> ShortenUrlAsync(string longUrl);
}