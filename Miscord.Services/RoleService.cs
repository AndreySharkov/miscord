using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Miscord.Data;
using Miscord.Data.Models;

namespace Miscord.Services
{
    public interface IRoleService
    {
        Task<List<ServerRole>> GetRolesAsync(int serverId);
        Task<ServerRole> CreateRoleAsync(int serverId, string name);
        Task UpdateRoleAsync(int serverId, int roleId, string name, string? color, long permissions);
        Task DeleteRoleAsync(int serverId, int roleId);
    }

    public class RoleService : IRoleService
    {
        private readonly AppDbContext _context;

        public RoleService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ServerRole>> GetRolesAsync(int serverId)
        {
            return await _context.ServerRoles
                .Where(r => r.ServerId == serverId)
                .OrderBy(r => r.Position)
                .ToListAsync();
        }

        public async Task<ServerRole> CreateRoleAsync(int serverId, string name)
        {
            var nextPosition = await _context.ServerRoles.CountAsync(r => r.ServerId == serverId);
            var role = new ServerRole 
            {
                ServerId = serverId,
                Name = name ?? "new role",
                Color = "#99aab5",
                Position = nextPosition,
                Permissions = (long)(ServerPermissions.SendMessages | ServerPermissions.AddReactions | ServerPermissions.ReadMessageHistory)
            };
            _context.ServerRoles.Add(role);
            await _context.SaveChangesAsync();
            return role;
        }

        public async Task UpdateRoleAsync(int serverId, int roleId, string name, string? color, long permissions)
        {
            var role = await _context.ServerRoles.FindAsync(roleId);
            if (role == null || role.ServerId != serverId) return;

            role.Name = name;
            role.Color = color;
            role.Permissions = permissions;
            await _context.SaveChangesAsync();
        }

        public async Task DeleteRoleAsync(int serverId, int roleId)
        {
            var role = await _context.ServerRoles.FindAsync(roleId);
            if (role == null || role.ServerId != serverId) return;

            _context.ServerRoles.Remove(role);
            await _context.SaveChangesAsync();
        }
    }
}
