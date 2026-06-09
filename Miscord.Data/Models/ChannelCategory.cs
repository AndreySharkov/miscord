using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Miscord.Data.Models
{
    public class ChannelCategory
    {
        public int Id { get; set; }
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        public int ServerId { get; set; }
        public Server Server { get; set; } = null!;
        public int Position { get; set; }
        public ICollection<Channel> Channels { get; set; } = new List<Channel>();
        public bool IsDeleted { get; set; } = false;
    }
}