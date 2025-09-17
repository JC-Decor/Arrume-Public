using System.ComponentModel.DataAnnotations;

namespace Arrume.Web.Models;

public class Capoteiro
{
    public int Id { get; set; }

    [Required, MaxLength(200)] public string Nome { get; set; } = string.Empty;

    [Required, RegularExpression(@"^55\d{10,11}$"), MaxLength(20)]
    public string Telefone { get; set; } = string.Empty;

    [Required, MaxLength(200)] public string Cidade { get; set; } = string.Empty;

    [MaxLength(200)] public string Bairro { get; set; } = string.Empty;

    [MaxLength(20)] public string Cep { get; set; } = string.Empty;

    [MaxLength(500)] public string Observacoes { get; set; } = string.Empty;
}
