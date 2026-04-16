using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace Miscord.Data.Models
{
    public class Message
    {
        public int Id { get; set; }
        [Required]
        [StringLength(200)]
        public string Content { get; set; } = null!;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int ChannelId { get; set; }
        [ForeignKey("ChannelId")]
        public Channel Channel { get; set; } = null!;

        [Required]
        public string AuthorId { get; set; } = null!;
        [ForeignKey("AuthorId")]
        public ApplicationUser Author { get; set; } = null!;
    }
}