using System.Collections.Generic;
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

        private readonly Dictionary<(string, int), long> _permissionsCache = new();
        private readonly Dictionary<int, string?> _ownerCache = new();

        public PermissionHelper(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> HasPermission(string? userId, int serverId, ServerPermissions permission)
        {
            if (string.IsNullOrEmpty(userId)) return false;

            // Cache server owner lookup
            if (!_ownerCache.TryGetValue(serverId, out var ownerId))
            {
                ownerId = await _context.Servers
                    .AsNoTracking()
                    .Where(s => s.Id == serverId)
                    .Select(s => s.OwnerId)
                    .FirstOrDefaultAsync();
                _ownerCache[serverId] = ownerId;
            }

            if (ownerId == null) return false;
            if (ownerId == userId) return true;

            // Cache combined permissions lookup for the user
            var cacheKey = (userId, serverId);
            if (!_permissionsCache.TryGetValue(cacheKey, out var userPermissions))
            {
                var rolesPermissions = await _context.ServerMembers
                    .AsNoTracking()
                    .Where(sm => sm.ServerId == serverId && sm.UserId == userId)
                    .SelectMany(sm => sm.MemberRoles.Select(mr => mr.ServerRole.Permissions))
                    .ToListAsync();

                userPermissions = 0;
                foreach (var perm in rolesPermissions)
                {
                    userPermissions |= perm;
                }
                _permissionsCache[cacheKey] = userPermissions;
            }

            // Check if user has Administrator or the specified permission
            if ((userPermissions & (long)ServerPermissions.Administrator) != 0)
                return true;

            return (userPermissions & (long)permission) != 0;
        }
    }
}
