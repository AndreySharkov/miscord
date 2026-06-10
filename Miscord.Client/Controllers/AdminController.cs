using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Miscord.Data;
using Miscord.Data.Models;
using Miscord.Services;
using System.Linq;
using System.Threading.Tasks;

namespace Miscord.Client.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IServerService _serverService;

        public AdminController(AppDbContext context, IServerService serverService)
        {
            _context = context;
            _serverService = serverService;
        }

        private bool IsAuthorizedAdmin()
        {
            return User.Identity?.Name == "admin@miscord.com";
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsAuthorizedAdmin()) return Forbid();
            var totalUsers = await _context.Users.CountAsync();
            var totalServers = await _context.Servers.Where(s => !s.IsDeleted).CountAsync();
            var totalMessages = await _context.Messages.CountAsync();
            var totalChannels = await _context.Channels.CountAsync();

            var servers = await _context.Servers
                .AsNoTracking()
                .Where(s => !s.IsDeleted)
                .Select(s => new Server
                {
                    Id = s.Id,
                    Name = s.Name,
                    OwnerId = s.OwnerId,
                    Owner = new ApplicationUser
                    {
                        UserName = s.Owner.UserName,
                        Nickname = s.Owner.Nickname
                    }
                })
                .ToListAsync();

            var users = await _context.Users
                .AsNoTracking()
                .Select(u => new ApplicationUser
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    Email = u.Email,
                    Nickname = u.Nickname
                })
                .ToListAsync();

            ViewData["TotalUsers"] = totalUsers;
            ViewData["TotalServers"] = totalServers;
            ViewData["TotalMessages"] = totalMessages;
            ViewData["TotalChannels"] = totalChannels;
            ViewData["ServersList"] = servers;
            ViewData["UsersList"] = users;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteServer(int serverId)
        {
            var server = await _context.Servers.FindAsync(serverId);
            if (server == null)
            {
                TempData["Error"] = "Server not found.";
                return RedirectToAction(nameof(Index));
            }

            await _serverService.DeleteServerAsync(serverId);
            TempData["Message"] = $"Server '{server.Name}' has been successfully deleted by Administrator.";

            return RedirectToAction(nameof(Index));
        }
    }
}
