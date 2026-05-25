using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Miscord.Data;
using Miscord.Data.Models;

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
            var servers = await _context.Servers.ToListAsync();
            return View(servers);
        }
    }
}