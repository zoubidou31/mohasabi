using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace Factur.Api.Controllers;

[ApiController]
[Route("api/version")]
public class VersionController : ControllerBase
{
    /// <summary>Version de l'application installée.</summary>
    [HttpGet]
    public ActionResult<object> Get()
    {
        var info = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? "1.0.4";
        var plus = info.IndexOf('+');
        return Ok(new { version = plus > 0 ? info[..plus] : info, product = "Mohasabi" });
    }
}
