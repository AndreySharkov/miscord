using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Miscord.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;

namespace Miscord.Client.Controllers
{
    public class ServerController : Controller
    {
        private readonly AppDbContext _context;
        public ServerController(AppDbContext context)
        {
            _context = context;
        }
        [HttpGet]

        public async Task<IActionResult> Index()
        {
            var servers = await _context.Servers.Where(s => s.Owner != null).ToListAsync();
            return View(servers);
        }
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var server = await _context.Servers.Include(s => s.Channels).FirstOrDefaultAsync(s => s.Id == id);
            if (server == null)
            {
                return NotFound();
            }
            ViewData["ServerName"] = server.Name;
            ViewData["Channels"] = server.Channels;
            return View(server);
        }
        [HttpGet]
        public async Task<IActionResult> GetChannels(int id)
        {
            var channels = await _context.Servers.Include(s => s.Channels).FirstOrDefaultAsync(s => s.Id == id);
            if (channels == null)
            {
                return NotFound();
            }            
            
            return PartialView("_ChannelList", channels.Channels);
            
        }

        [HttpGet]
        public async Task<IActionResult> GetChat(int channelId)
        {
            var channel = await _context.Channels
                .Include(c => c.Messages.OrderBy(m => m.Timestamp))
                .ThenInclude(m => m.Author)
                .FirstOrDefaultAsync(c => c.Id == channelId);
            if (channel == null)
            {
                return NotFound();
            }
            return PartialView("_ChatArea", channel);
        }

        
    }
}