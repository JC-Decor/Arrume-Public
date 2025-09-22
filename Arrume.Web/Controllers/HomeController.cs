using Microsoft.AspNetCore.Mvc;

namespace Arrume.Web.Controllers;

public class HomeController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();

    [HttpGet("obrigado")]
    public IActionResult Obrigado() => View();

    [Route("Home/Error")] 
    public IActionResult Error() =>
        Problem(title: "Ocorreu um erro", statusCode: 500);
}
