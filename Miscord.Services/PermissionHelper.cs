using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Miscord.Data;
using Miscord.Data.Models;

namespace Miscord.Services
{
    public class PermissionHelper
    {
        private readonly AppDbContext _context;

        public PermissionHelper(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> HasPermission(string userId, int serverId, ServerPermissions permission)
        {
            var server = await _context.Servers.FindAsync(serverId);
            if (server == null) return false;

            // Owner always has all permissions
            if (server.OwnerId == userId) return true;

            var member = await _context.ServerMembers
                .Include(sm => sm.MemberRoles)
                    .ThenInclude(mr => mr.ServerRole)
                .FirstOrDefaultAsync(sm => sm.ServerId == serverId && sm.UserId == userId);

            if (member == null) return false;

            // Check if any role has the permission
            foreach (var memberRole in member.MemberRoles)
            {
                if ((memberRole.ServerRole.Permissions & (long)ServerPermissions.Administrator) != 0)
                    return true;

                if ((memberRole.ServerRole.Permissions & (long)permission) != 0)
                    return true;
            }

            return false;
        }
    }
}
