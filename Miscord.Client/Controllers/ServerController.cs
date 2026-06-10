using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Miscord.Client.Models;
using Miscord.Data;
using Miscord.Data.Models;
using Miscord.Services;

namespace Miscord.Client.Controllers
{
    [Authorize]
    public class ServerController : Controller
    {
        private readonly IServerService _serverService;
        private readonly IMemberService _memberService;
        private readonly IRoleService _roleService;
        private readonly PermissionHelper _permissionHelper;
        private readonly AppDbContext _context; // Kept only for GetProfilePicture since there's no UserService yet

        public ServerController(
            IServerService serverService, 
            IMemberService memberService, 
            IRoleService roleService, 
            PermissionHelper permissionHelper,
            AppDbContext context)
        {
            _serverService = serverService;
            _memberService = memberService;
            _roleService = roleService;
            _permissionHelper = permissionHelper;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var servers = await _serverService.GetServersAsync();
            return View(servers);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var server = await _serverService.GetServerDetailsAsync(id);
            if (server == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewData["ServerName"] = server.Name;
            ViewData["Channels"] = server.Channels;
            ViewData["Categories"] = server.ChannelCategories;
            ViewData["Server"] = server;
            ViewData["CurrentUserId"] = userId;

            var hasManageChannels = await _permissionHelper.HasPermission(userId, id, ServerPermissions.ManageChannels);
            var hasManageServer = await _permissionHelper.HasPermission(userId, id, ServerPermissions.ManageServer);
            var hasManageRoles = await _permissionHelper.HasPermission(userId, id, ServerPermissions.ManageRoles);
            
            ViewData["IsAdmin"] = hasManageChannels || hasManageServer || hasManageRoles;
            ViewData["HasManageRoles"] = hasManageRoles;
            ViewData["HasKickMembers"] = await _permissionHelper.HasPermission(userId, id, ServerPermissions.KickMembers);
            ViewData["HasBanMembers"] = await _permissionHelper.HasPermission(userId, id, ServerPermissions.BanMembers);
            ViewData["HasManageNicknames"] = await _permissionHelper.HasPermission(userId, id, ServerPermissions.ManageNicknames);
            
            return View(server);
        }

        [HttpGet]
        public async Task<IActionResult> GetMembersSidebar(int serverId)
        {
            var server = await _serverService.GetServerDetailsAsync(serverId);
            if (server == null) return NotFound();
            return PartialView("_MembersSidebar", server);
        }

        [HttpGet]
        public async Task<IActionResult> GetChannels(int id)
        {
            var server = await _serverService.GetServerDetailsAsync(id);
            if (server == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            ViewData["CurrentUserId"] = userId;
            
            var hasManageChannels = await _permissionHelper.HasPermission(userId, id, ServerPermissions.ManageChannels);
            var hasManageServer = await _permissionHelper.HasPermission(userId, id, ServerPermissions.ManageServer);
            var hasManageRoles = await _permissionHelper.HasPermission(userId, id, ServerPermissions.ManageRoles);
            ViewData["IsAdmin"] = hasManageChannels || hasManageServer || hasManageRoles;

            return PartialView("_ChannelList", server);
        }

        [HttpPost]
        public async Task<IActionResult> CreateServer([FromForm] string ServerName, [FromForm] string? serverType, [FromForm] IFormFile? serverIcon)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            byte[]? iconData = null;
            if (serverIcon != null && serverIcon.Length > 0)
            {
                using var ms = new MemoryStream();
                await serverIcon.CopyToAsync(ms);
                iconData = ms.ToArray();
            }

            var serverId = await _serverService.CreateServerAsync(ServerName, userId, iconData, serverType);
            return Ok(new { serverId });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateServer([FromForm] int serverId, [FromForm] string serverName, [FromForm] IFormFile? serverIcon)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!await _permissionHelper.HasPermission(userId, serverId, ServerPermissions.ManageServer))
                return Unauthorized();

            byte[]? iconData = null;
            if (serverIcon != null && serverIcon.Length > 0)
            {
                using var ms = new MemoryStream();
                await serverIcon.CopyToAsync(ms);
                iconData = ms.ToArray();
            }

            await _serverService.UpdateServerAsync(serverId, serverName, iconData);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> DeleteServer(int serverId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var server = await _serverService.GetServerByIdAsync(serverId);
            if (server == null) return NotFound();

            if (server.OwnerId != userId) return Unauthorized("Only the owner can delete the server.");

            await _serverService.DeleteServerAsync(serverId);
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> GetServerIcon(int serverId)
        {
            var server = await _serverService.GetServerByIdAsync(serverId);
            if (server == null || server.IconData == null) return NotFound();
            
            Response.Headers["Cache-Control"] = "public, max-age=3600";
            return File(server.IconData, "image/png");
        }

        [HttpGet]
        public async Task<IActionResult> GetProfilePicture(string userId)
        {
            var user = await _context.Users.AsNoTracking().Select(u => new { u.Id, u.ProfilePictureData }).FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null || user.ProfilePictureData == null) return NotFound();

            Response.Headers["Cache-Control"] = "public, max-age=3600";
            return File(user.ProfilePictureData, "image/png");
        }

        [HttpGet]
        public async Task<IActionResult> GetRoles(int serverId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!await _permissionHelper.HasPermission(userId, serverId, ServerPermissions.ManageRoles)) return Unauthorized();

            var roles = await _roleService.GetRolesAsync(serverId);
            return Json(roles.Select(r => new { id = r.Id, name = r.Name, color = r.Color, position = r.Position, permissions = r.Permissions.ToString() }));
        }

        [HttpPost]
        public async Task<IActionResult> CreateRole([FromForm] int serverId, [FromForm] string name)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "User not found." });

            if (!await _permissionHelper.HasPermission(userId, serverId, ServerPermissions.ManageRoles)) 
                return Unauthorized(new { message = "You do not have permission to manage roles." });

            var role = await _roleService.CreateRoleAsync(serverId, name);
            return Json(new { id = role.Id, name = role.Name, color = role.Color, position = role.Position, permissions = role.Permissions.ToString() });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateRole([FromForm] int serverId, [FromForm] int roleId, [FromForm] string name, [FromForm] string? color, [FromForm] long permissions)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!await _permissionHelper.HasPermission(userId, serverId, ServerPermissions.ManageRoles)) return Unauthorized();

            await _roleService.UpdateRoleAsync(serverId, roleId, name, color, permissions);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> DeleteRole(int serverId, int roleId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!await _permissionHelper.HasPermission(userId, serverId, ServerPermissions.ManageRoles)) return Unauthorized();

            await _roleService.DeleteRoleAsync(serverId, roleId);
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> GetMembers(int serverId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!await _permissionHelper.HasPermission(userId, serverId, ServerPermissions.ManageRoles)) return Unauthorized();

            var members = await _memberService.GetMembersAsync(serverId);
            return Json(members.Select(sm => new {
                sm.UserId,
                DisplayName = sm.Nickname ?? sm.User.Nickname ?? sm.User.UserName,
                sm.User.UserName,
                HasPfp = sm.User.ProfilePictureData != null,
                Roles = sm.MemberRoles.Select(mr => new { mr.ServerRole.Id, mr.ServerRole.Name, mr.ServerRole.Color })
            }));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateMemberRoles([FromForm] int serverId, [FromForm] string userId, [FromForm] string roleIds)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!await _permissionHelper.HasPermission(currentUserId, serverId, ServerPermissions.ManageRoles)) return Unauthorized();

            var ids = string.IsNullOrEmpty(roleIds) ? new List<int>() : roleIds.Split(',').Select(int.Parse).ToList();
            await _memberService.UpdateMemberRolesAsync(serverId, userId, ids);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> KickMember([FromForm] int serverId, [FromForm] string userId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!await _permissionHelper.HasPermission(currentUserId, serverId, ServerPermissions.KickMembers)) return Unauthorized();

            var server = await _serverService.GetServerByIdAsync(serverId);
            if (server?.OwnerId == userId) return BadRequest("Cannot kick the server owner.");

            await _memberService.KickMemberAsync(serverId, userId);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> BanMember([FromForm] int serverId, [FromForm] string userId, [FromForm] string? reason)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!await _permissionHelper.HasPermission(currentUserId, serverId, ServerPermissions.BanMembers)) return Unauthorized();

            var server = await _serverService.GetServerByIdAsync(serverId);
            if (server?.OwnerId == userId) return BadRequest("Cannot ban the server owner.");

            await _memberService.BanMemberAsync(serverId, userId, reason);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateNickname([FromForm] int serverId, [FromForm] string userId, [FromForm] string? nickname)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            bool canChangeOwn = currentUserId == userId && await _permissionHelper.HasPermission(currentUserId, serverId, ServerPermissions.ChangeNickname);
            bool canManageOthers = await _permissionHelper.HasPermission(currentUserId, serverId, ServerPermissions.ManageNicknames);

            if (!canChangeOwn && !canManageOthers) return Unauthorized();

            await _memberService.UpdateNicknameAsync(serverId, userId, nickname);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> CreateInvite([FromForm] int serverId, [FromForm] int? expirationDays, [FromForm] int? maxUses)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!await _permissionHelper.HasPermission(userId, serverId, ServerPermissions.CreateInvite)) return Unauthorized();

            var token = await _serverService.CreateInviteAsync(serverId, userId, expirationDays, maxUses);
            return Ok(new { token });
        }

        [HttpPost]
        public async Task<IActionResult> LeaveServer(int serverId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _serverService.LeaveServerAsync(serverId, userId!);
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> Join(string id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Join", "Server", new { id = id }) });
            }

            try
            {
                var serverId = await _serverService.JoinServerAsync(id, userId);
                if (serverId == null) return NotFound("This invite link is invalid or has expired.");
                return RedirectToAction("Details", new { id = serverId });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }
    }
}