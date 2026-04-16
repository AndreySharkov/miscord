using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;


namespace Miscord.Data.Models
{
    public class Channel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = null!;

        public int ServerId { get; set; }
        public Server Server { get; set; } = null!;

        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}