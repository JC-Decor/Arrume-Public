using Arrume.Web.Models;

namespace Arrume.Web.Services;

public interface IZApiService
{
    Task EnviarMensagemClienteAsync(string telefoneCliente, IEnumerable<Capoteiro> capoteiros);
    Task EnviarMensagemCapoteiroAsync(string telefoneCapoteiro, Lead lead);
}
