using System;
using System.ComponentModel.DataAnnotations;

namespace Miscord.Data.Models
{
    public class Invite
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string Token { get; set; } = string.Empty;

        public int ServerId { get; set; }
        public Server Server { get; set; } = null!;

        public string CreatorId { get; set; } = null!;
        public ApplicationUser Creator { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
        public int? MaxUses { get; set; }
        public int Uses { get; set; } = 0;

        public bool IsExpired => (ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow) || (MaxUses.HasValue && Uses >= MaxUses.Value);
    }
}
