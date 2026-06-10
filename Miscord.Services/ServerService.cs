using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Miscord.Data;
using Miscord.Data.Models;

namespace Miscord.Services
{
    public interface IServerService
    {
        Task<List<Server>> GetServersAsync();
        Task<Server?> GetServerDetailsAsync(int serverId);
        Task<Server?> GetServerByIdAsync(int serverId);
        Task<Server?> GetServerForSidebarAsync(int serverId);
        Task<int> CreateServerAsync(string name, string ownerId, byte[]? iconData, string? serverType);
        Task UpdateServerAsync(int serverId, string name, byte[]? iconData);
        Task DeleteServerAsync(int serverId);
        Task LeaveServerAsync(int serverId, string userId);
        Task<string> CreateInviteAsync(int serverId, string creatorId, int? expirationDays, int? maxUses);
        Task<int?> JoinServerAsync(string inviteToken, string userId);
    }

    public class ServerService : IServerService
    {
        private readonly AppDbContext _context;

        public ServerService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Server>> GetServersAsync()
        {
            return await _context.Servers.Where(s => s.Owner != null && !s.IsDeleted).ToListAsync();
        }

        public async Task<Server?> GetServerDetailsAsync(int serverId)
        {
            return await _context.Servers
                .Include(s => s.ChannelCategories.OrderBy(cc => cc.Position))
                    .ThenInclude(cc => cc.Channels.OrderBy(c => c.Position))
                .Include(s => s.Channels.Where(c => c.ChannelCategoryId == null).OrderBy(c => c.Position))
                .Include(s => s.Members)
                    .ThenInclude(m => m.User)
                .Include(s => s.Members)
                    .ThenInclude(m => m.MemberRoles)
                        .ThenInclude(mr => mr.ServerRole)
                .Include(s => s.Roles.OrderByDescending(r => r.Position))
                .FirstOrDefaultAsync(s => s.Id == serverId && !s.IsDeleted);
        }

        public async Task<Server?> GetServerByIdAsync(int serverId)
        {
            return await _context.Servers.FirstOrDefaultAsync(s => s.Id == serverId && !s.IsDeleted);
        }

        public async Task<Server?> GetServerForSidebarAsync(int serverId)
        {
            return await _context.Servers
                .AsNoTracking()
                .Include(s => s.Members)
                    .ThenInclude(m => m.User)
                .Include(s => s.Members)
                    .ThenInclude(m => m.MemberRoles)
                        .ThenInclude(mr => mr.ServerRole)
                .Include(s => s.Roles)
                .Select(s => new Server
                {
                    Id = s.Id,
                    OwnerId = s.OwnerId,
                    Roles = s.Roles.Select(r => new ServerRole
                    {
                        Id = r.Id,
                        Name = r.Name,
                        Color = r.Color,
                        Position = r.Position
                    }).ToList(),
                    Members = s.Members.Select(m => new ServerMember
                    {
                        Id = m.Id,
                        UserId = m.UserId,
                        Nickname = m.Nickname,
                        User = new ApplicationUser
                        {
                            Id = m.User.Id,
                            UserName = m.User.UserName,
                            Nickname = m.User.Nickname,
                            ProfilePictureData = m.User.ProfilePictureData != null ? new byte[0] : null // Flag to indicate if PFP exists
                        },
                        MemberRoles = m.MemberRoles.Select(mr => new ServerMemberRole
                        {
                            ServerRoleId = mr.ServerRoleId,
                            ServerRole = new ServerRole
                            {
                                Id = mr.ServerRole.Id,
                                Position = mr.ServerRole.Position
                            }
                        }).ToList()
                    }).ToList()
                })
                .FirstOrDefaultAsync(s => s.Id == serverId && !s.IsDeleted);
        }

        public async Task<int> CreateServerAsync(string name, string ownerId, byte[]? iconData, string? serverType)
        {
            var server = new Server { Name = name, OwnerId = ownerId, IconData = iconData };
            _context.Servers.Add(server);
            await _context.SaveChangesAsync();

            // Default Channels
            _context.Channels.Add(new Channel { Name = "general", ServerId = server.Id });
            _context.Channels.Add(new Channel { Name = "announcements", ServerId = server.Id });
            
            if (serverType == "gaming") _context.Channels.Add(new Channel { Name = "lobby", ServerId = server.Id });
            else if (serverType == "school") _context.Channels.Add(new Channel { Name = "homework-help", ServerId = server.Id });
            
            // Add creator as member
            var member = new ServerMember { ServerId = server.Id, UserId = ownerId, JoinedAt = DateTime.UtcNow };
            _context.ServerMembers.Add(member);

            // Add Default Roles
            var adminRole = new ServerRole
            {
                Name = "Admin",
                ServerId = server.Id,
                Position = 0,
                Color = "#e74c3c",
                Permissions = (long)ServerPermissions.Administrator
            };
            var memberRole = new ServerRole
            {
                Name = "Member",
                ServerId = server.Id,
                Position = 1,
                Color = "#9b59b6",
                Permissions = (long)(ServerPermissions.SendMessages | ServerPermissions.AddReactions | ServerPermissions.ReadMessageHistory | ServerPermissions.CreateInvite | ServerPermissions.ChangeNickname)
            };

            _context.ServerRoles.Add(adminRole);
            _context.ServerRoles.Add(memberRole);
            await _context.SaveChangesAsync();

            // Assign Admin role to creator
            _context.ServerMemberRoles.Add(new ServerMemberRole { ServerMemberId = member.Id, ServerRoleId = adminRole.Id });
            await _context.SaveChangesAsync();

            return server.Id;
        }

        public async Task UpdateServerAsync(int serverId, string name, byte[]? iconData)
        {
            var server = await _context.Servers.FindAsync(serverId);
            if (server == null) return;

            server.Name = name;
            if (iconData != null)
            {
                server.IconData = iconData;
            }
            await _context.SaveChangesAsync();
        }

        public async Task DeleteServerAsync(int serverId)
        {
            var server = await _context.Servers.FindAsync(serverId);
            if (server != null)
            {
                server.IsDeleted = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task LeaveServerAsync(int serverId, string userId)
        {
            var server = await _context.Servers
                .Include(s => s.Members)
                .FirstOrDefaultAsync(s => s.Id == serverId);
            
            if (server == null) return;

            var member = await _context.ServerMembers.FirstOrDefaultAsync(sm => sm.ServerId == serverId && sm.UserId == userId);
            if (member == null) return;

            if (server.OwnerId == userId)
            {
                var nextMember = await _context.ServerMembers
                    .Where(sm => sm.ServerId == serverId && sm.UserId != userId)
                    .OrderBy(sm => sm.JoinedAt)
                    .FirstOrDefaultAsync();

                if (nextMember != null)
                {
                    server.OwnerId = nextMember.UserId;
                }
                else
                {
                    server.IsDeleted = true;
                }
            }

            _context.ServerMembers.Remove(member);
            await _context.SaveChangesAsync();
        }

        public async Task<string> CreateInviteAsync(int serverId, string creatorId, int? expirationDays, int? maxUses)
        {
            var token = Guid.NewGuid().ToString("N").Substring(0, 10);
            var invite = new Invite
            {
                Token = token,
                ServerId = serverId,
                CreatorId = creatorId,
                ExpiresAt = expirationDays.HasValue ? DateTime.UtcNow.AddDays(expirationDays.Value) : null,
                MaxUses = maxUses
            };

            _context.Invites.Add(invite);
            await _context.SaveChangesAsync();
            return token;
        }

        public async Task<int?> JoinServerAsync(string inviteToken, string userId)
        {
            var invite = await _context.Invites.FirstOrDefaultAsync(i => i.Token == inviteToken);
            if (invite == null || invite.IsExpired) return null;

            var isBanned = await _context.ServerBans.AnyAsync(b => b.ServerId == invite.ServerId && b.UserId == userId);
            if (isBanned) throw new UnauthorizedAccessException("You are banned from this server.");

            var existingMember = await _context.ServerMembers.FirstOrDefaultAsync(sm => sm.ServerId == invite.ServerId && sm.UserId == userId);
            if (existingMember == null)
            {
                _context.ServerMembers.Add(new ServerMember { ServerId = invite.ServerId, UserId = userId, JoinedAt = DateTime.UtcNow });
                invite.Uses++;
                await _context.SaveChangesAsync();
            }

            return invite.ServerId;
        }
    }
}
