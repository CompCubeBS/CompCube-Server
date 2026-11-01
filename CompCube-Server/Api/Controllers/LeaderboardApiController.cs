using CompCube_Models.Models.ClientData;
using CompCube_Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CompCube_Server.Api.Controllers;

[ApiController]
[AllowAnonymous]
public class LeaderboardApiController(UserData userData) : ControllerBase
{
    [HttpGet("api/leaderboard/range/")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<UserStatistics[]> GetLeaderboardRange(int start, int range) =>
        userData.GetLeaderboardRange(start, range).ToArray();

    [HttpGet("/api/leaderboard/aroundUser/{userId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<UserStatistics[]> GetAroundUser(string userId)
    {
        var aroundUser = userData.GetAroundUser(userId);

        if (aroundUser == null)
            return NotFound();

        return aroundUser.ToArray();
    }
}