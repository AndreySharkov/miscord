using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Miscord.Data.Models
{
    public class Reaction
    {
        public int Id { get; set; }
        [Required]
        public int MessageId { get; set; }
        public Message Message { get; set; } = null!;
        [Required]
        public string UserId { get; set; } = null!;
        public ApplicationUser User { get; set; } = null!;

        [Required]
        [StringLength(10)]
        public string Emoji { get; set; } = null!;
    }
}