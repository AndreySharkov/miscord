using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Miscord.Data.Models;
using Miscord.Services;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Miscord.Client.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/server")]
    public class ServerApiController : ControllerBase
    {
        private readonly IMemberService _memberService;
        private readonly PermissionHelper _permissionHelper;

        public ServerApiController(IMemberService memberService, PermissionHelper permissionHelper)
        {
            _memberService = memberService;
            _permissionHelper = permissionHelper;
        }

        [HttpGet("members")]
        public async Task<IActionResult> GetMembers(int serverId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // Check if user has permission to manage roles or is just a member of the server
            // For general member list, usually membership is enough, but we follow previous logic
            if (!await _permissionHelper.HasPermission(userId!, serverId, ServerPermissions.None)) return Unauthorized();

            var members = await _memberService.GetMembersAsync(serverId);
            return Ok(members.Select(sm => new {
                sm.UserId,
                DisplayName = sm.Nickname ?? sm.User.Nickname ?? sm.User.UserName,
                sm.User.UserName,
                HasPfp = sm.User.ProfilePictureData != null,
                Roles = sm.MemberRoles.Select(mr => new { mr.ServerRole.Id, mr.ServerRole.Name, mr.ServerRole.Color })
            }));
        }
    }
}
