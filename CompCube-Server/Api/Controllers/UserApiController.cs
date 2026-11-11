using CompCube_Models.Models.ClientData;
using CompCube_Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SkiaSharp;

namespace CompCube_Server.Api.Controllers;

[ApiController]
public class UserApiController(UserData userData) : ControllerBase
{
    private static readonly HttpClient HttpClient = new();

    [HttpGet("api/user/@me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<UserProfileFull> GetSelf()
    {
        var self = User.Claims.FirstOrDefault(c => c.Type == "platformId")?.Value;

        if (self == null)
            return Unauthorized();

        return GetUserByPlatformId(self);
    }
    
    [HttpGet("/api/user/id/{platformId}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<UserProfileFull> GetUserByPlatformId(string id)
    {
        var user = userData.GetFullUserProfileByPlatformId(id);
        
        if (user == null) 
            return NotFound();

        return user;
    }

    [HttpPatch("/api/user/id/{platformId}/ban")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Authorize(Policy = "RequireModerator")]
    public IActionResult BanUser(string platformId, bool banned = true)
    {
        if (userData.SetBannedStatus(platformId, banned))
            return Ok();

        return NotFound();
    }

    [HttpPatch("/api/user/id/{platformId}/flairs")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Authorize(Policy = "RequireAdmin")]
    public IActionResult SetFlairs(string platformId, string flairGuids)
    {
        var flairs = flairGuids.Split(',');
        
        if (userData.SetFlairs(platformId, flairs))
            return Ok();
        
        return NotFound();
    }

    [HttpGet("/api/user/id/{platformId}/avatar")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetUserAvatar(string platformId)
    {
        var url = userData.GetUserProfilePictureUrl(platformId);

        if (url == null)
            return NotFound();

        var response = await HttpClient.GetAsync(new Uri(url, UriKind.Absolute));

        if (!response.IsSuccessStatusCode)
            return StatusCode(503);

        var data = await response.Content.ReadAsStreamAsync();

        using var image = SKBitmap.Decode(data);
        
        using var targetBitmap = new SKBitmap(new SKImageInfo(image.Width, image.Height, SKImageInfo.PlatformColorType, SKAlphaType.Opaque));

        using var final = SKImage.FromBitmap(image);
        
        return File(final.Encode(SKEncodedImageFormat.Jpeg, 80).ToArray(), "image/jpeg");
    }
}