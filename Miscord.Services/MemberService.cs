using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Miscord.Data;
using Miscord.Data.Models;

namespace Miscord.Services
{
    public interface IMemberService
    {
        Task<List<ServerMember>> GetMembersAsync(int serverId);
        Task UpdateMemberRolesAsync(int serverId, string userId, IEnumerable<int> roleIds);
        Task KickMemberAsync(int serverId, string userId);
        Task BanMemberAsync(int serverId, string userId, string? reason);
        Task UpdateNicknameAsync(int serverId, string userId, string? nickname);
    }

    public class MemberService : IMemberService
    {
        private readonly AppDbContext _context;

        public MemberService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ServerMember>> GetMembersAsync(int serverId)
        {
            return await _context.ServerMembers
                .AsNoTracking()
                .Where(sm => sm.ServerId == serverId)
                .Select(sm => new ServerMember
                {
                    Id = sm.Id,
                    ServerId = sm.ServerId,
                    UserId = sm.UserId,
                    Nickname = sm.Nickname,
                    JoinedAt = sm.JoinedAt,
                    User = new ApplicationUser
                    {
                        Id = sm.User.Id,
                        UserName = sm.User.UserName,
                        Nickname = sm.User.Nickname,
                        ProfilePictureData = sm.User.ProfilePictureData != null ? new byte[0] : null
                    },
                    MemberRoles = sm.MemberRoles.Select(mr => new ServerMemberRole
                    {
                        ServerMemberId = mr.ServerMemberId,
                        ServerRoleId = mr.ServerRoleId,
                        ServerRole = new ServerRole
                        {
                            Id = mr.ServerRole.Id,
                            Name = mr.ServerRole.Name,
                            Color = mr.ServerRole.Color,
                            Position = mr.ServerRole.Position
                        }
                    }).ToList()
                })
                .ToListAsync();
        }

        public async Task UpdateMemberRolesAsync(int serverId, string userId, IEnumerable<int> roleIds)
        {
            var member = await _context.ServerMembers
                .Include(sm => sm.MemberRoles)
                .FirstOrDefaultAsync(sm => sm.ServerId == serverId && sm.UserId == userId);

            if (member == null) return;

            _context.ServerMemberRoles.RemoveRange(member.MemberRoles);

            foreach (var rid in roleIds)
            {
                _context.ServerMemberRoles.Add(new ServerMemberRole { ServerMemberId = member.Id, ServerRoleId = rid });
            }

            await _context.SaveChangesAsync();
        }

        public async Task KickMemberAsync(int serverId, string userId)
        {
            var member = await _context.ServerMembers.FirstOrDefaultAsync(sm => sm.ServerId == serverId && sm.UserId == userId);
            if (member != null)
            {
                _context.ServerMembers.Remove(member);
                await _context.SaveChangesAsync();
            }
        }

        public async Task BanMemberAsync(int serverId, string userId, string? reason)
        {
            var member = await _context.ServerMembers.FirstOrDefaultAsync(sm => sm.ServerId == serverId && sm.UserId == userId);
            if (member != null)
            {
                _context.ServerMembers.Remove(member);
            }

            if (!await _context.ServerBans.AnyAsync(b => b.ServerId == serverId && b.UserId == userId))
            {
                _context.ServerBans.Add(new ServerBan { ServerId = serverId, UserId = userId, Reason = reason });
            }

            await _context.SaveChangesAsync();
        }

        public async Task UpdateNicknameAsync(int serverId, string userId, string? nickname)
        {
            var member = await _context.ServerMembers.FirstOrDefaultAsync(sm => sm.ServerId == serverId && sm.UserId == userId);
            if (member == null) return;

            member.Nickname = string.IsNullOrWhiteSpace(nickname) ? null : nickname;
            await _context.SaveChangesAsync();
        }
    }
}
