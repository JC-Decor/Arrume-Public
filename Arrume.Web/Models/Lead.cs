using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Arrume.Web.Models;

public class Lead
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Nome é obrigatório.")]
    [MinLength(2, ErrorMessage = "Nome deve ter ao menos 2 caracteres.")]
    [MaxLength(200, ErrorMessage = "Nome deve ter no máximo 200 caracteres.")]
    [RegularExpression(@"^[A-Za-zÀ-ÖØ-öø-ÿ\s'.-]+$", ErrorMessage = "Nome contém caracteres inválidos.")]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Telefone (WhatsApp) é obrigatório.")]
    [RegularExpression(@"^55\d{10,11}$", ErrorMessage = "Telefone deve estar no formato 55DDDNÚMERO (somente dígitos).")]
    public string Telefone { get; set; } = string.Empty;

    [Required(ErrorMessage = "CEP é obrigatório.")]
    [RegularExpression(@"^\d{8}$", ErrorMessage = "CEP deve conter 8 dígitos.")]
    public string Cep { get; set; } = string.Empty;

    [MaxLength(300, ErrorMessage = "Logradouro deve ter no máximo 300 caracteres.")]
    public string Logradouro { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "Bairro deve ter no máximo 200 caracteres.")]
    public string Bairro { get; set; } = string.Empty;

    [Required(ErrorMessage = "Cidade é obrigatória.")]
    [MaxLength(200, ErrorMessage = "Cidade deve ter no máximo 200 caracteres.")]
    public string Cidade { get; set; } = string.Empty;

    [MaxLength(10, ErrorMessage = "UF deve ter no máximo 10 caracteres.")]
    public string Uf { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escolha o tipo de serviço.")]
    [MaxLength(50, ErrorMessage = "Serviço deve ter no máximo 50 caracteres.")]
    public string Servico { get; set; } = "ambos";

    [Required(ErrorMessage = "Email é obrigatório.")]
    [EmailAddress(ErrorMessage = "Email inválido.")]
    [MaxLength(200, ErrorMessage = "Email deve ter no máximo 200 caracteres.")]
    public string Email { get; set; } = string.Empty;
    public bool AceiteContatoWhatsapp { get; set; }
    public bool AceiteCompartilhamento { get; set; }
    public bool AceiteUso { get; set; }

    public DateTime? ConsentTimestampUtc { get; set; }
    [MaxLength(50)] public string? ConsentIpAddress { get; set; }
    [MaxLength(500)] public string? ConsentUserAgent { get; set; }
    [MaxLength(20)] public string? ConsentVersion { get; set; } = "1.0";
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public string TelefoneHash =>
        string.IsNullOrEmpty(Telefone) ? "" :
        Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{Telefone}|{Id}")));
}
