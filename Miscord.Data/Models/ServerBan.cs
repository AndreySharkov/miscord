using System;
using System.ComponentModel.DataAnnotations;

namespace Miscord.Data.Models
{
    public class ServerBan
    {
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;

        public int ServerId { get; set; }
        public Server Server { get; set; } = null!;

        public string? Reason { get; set; }
        public DateTime BannedAt { get; set; } = DateTime.UtcNow;
    }
}
