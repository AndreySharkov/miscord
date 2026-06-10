using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Miscord.Data;
using Miscord.Data.Models;
using System.Security.Claims;

namespace Miscord.Client.ViewComponents
{
    public class ServerSidebarViewComponent : ViewComponent
    {
        private readonly AppDbContext _context;
        public ServerSidebarViewComponent(AppDbContext context){
            _context = context; 
        }
        public async Task<IViewComponentResult> InvokeAsync()
        {
            var userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return View(new List<Server>());
            }

            var servers = await _context.ServerMembers
                .AsNoTracking()
                .Where(sm => sm.UserId == userId)
                .Select(sm => new Server
                {
                    Id = sm.Server.Id,
                    Name = sm.Server.Name,
                    IconData = sm.Server.IconData
                })
                .ToListAsync();

            return View(servers);
        }
    }
}