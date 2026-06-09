using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Miscord.Data.Models
{
    public class ServerMember
    {
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;

        public int ServerId { get; set; }
        public Server Server { get; set; } = null!;

        public string? Nickname { get; set; }
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ServerMemberRole> MemberRoles { get; set; } = new List<ServerMemberRole>();
    }

    public class ServerMemberRole
    {
        public int ServerMemberId { get; set; }
        public ServerMember ServerMember { get; set; } = null!;

        public int ServerRoleId { get; set; }
        public ServerRole ServerRole { get; set; } = null!;
    }
}
