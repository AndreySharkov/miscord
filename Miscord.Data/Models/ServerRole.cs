using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Miscord.Data.Models
{
    public class ServerRole
    {
        public int Id { get; set; }
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;
        public string? Color { get; set; } // Hex code
        public int Position { get; set; }
        
        // Permissions (Bitwise flags for simplicity)
        public long Permissions { get; set; } 

        public int ServerId { get; set; }
        public Server Server { get; set; } = null!;

        public ICollection<ServerMemberRole> MemberRoles { get; set; } = new List<ServerMemberRole>();
    }

    [Flags]
    public enum ServerPermissions : long
    {
        None = 0,
        Administrator = 1 << 0,
        ManageServer = 1 << 1,
        ManageRoles = 1 << 2,
        ManageChannels = 1 << 3,
        KickMembers = 1 << 4,
        BanMembers = 1 << 5,
        CreateInvite = 1 << 6,
        ChangeNickname = 1 << 7,
        ManageNicknames = 1 << 8,
        SendMessages = 1 << 9,
        EmbedLinks = 1 << 10,
        AttachFiles = 1 << 11,
        AddReactions = 1 << 12,
        MentionEveryone = 1 << 13,
        ManageMessages = 1 << 14,
        ReadMessageHistory = 1 << 15
    }
}
