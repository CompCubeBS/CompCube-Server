using CompCube_Models.Models.ClientData;
using CompCube_Server.Data;
using CompCube_Server.Models.CompCube_Models.Models.Contributors;
using Microsoft.AspNetCore.Mvc;

namespace CompCube_Server.Api.Controllers;

[ApiController]
public class ContributorsApiController(UserData userData) : ControllerBase
{
    [HttpGet("api/contributors")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<UserInfo[]> GetContributors(string permissionFilter)
    {
        var users = userData.GetUserInfoByPermission(permissionFilter);
        
        if (users.Length == 0)
            return NotFound();

        return users;
    }
}