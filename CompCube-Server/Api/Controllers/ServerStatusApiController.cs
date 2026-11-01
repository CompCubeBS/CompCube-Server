using CompCube_Server.Networking.ServerStatus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace CompCube_Server.Api.Controllers;

[ApiController]
[AllowAnonymous]
public class ServerStatusApiController(ServerStatusManager serverStatusManager) : ControllerBase
{
    [HttpGet("/api/server/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<string> GetServerStatus() => JsonConvert.SerializeObject(serverStatusManager.GetServerStatus());
}