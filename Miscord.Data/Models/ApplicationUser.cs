using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace Miscord.Data.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? Nickname { get; set; } 
        public byte[]? ProfilePictureData { get; set; }

        public ICollection<Server> OwnedServers { get; set; } = new List<Server>();
        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}