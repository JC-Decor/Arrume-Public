namespace Arrume.Web.Services;

public class ZApiOptions
{
    public string UrlBase { get; set; } = "https://api.z-api.io";
    public string Instance { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string MessageEndpoint { get; set; } = "send-text";
    public bool UseFake { get; set; } = true;
    public string FakeLogFile { get; set; } = "mock_messages.log";
    public string NomeInstancia { get; set; } = string.Empty;
    public string ClientToken { get; set; } = string.Empty;
    public string? SenderPhone { get; set; }
}
