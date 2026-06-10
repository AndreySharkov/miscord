using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Miscord.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Miscord.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            
        }
        public DbSet<Server> Servers { get; set; }
        public DbSet<Channel> Channels { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Reaction> Reactions { get; set; }
        public DbSet<ChannelCategory> ChannelCategories { get; set; }
        public DbSet<ServerRole> ServerRoles { get; set; }
        public DbSet<ServerMember> ServerMembers { get; set; }
        public DbSet<ServerMemberRole> ServerMemberRoles { get; set; }
        public DbSet<Invite> Invites { get; set; }
        public DbSet<ServerBan> ServerBans { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ServerBan>()
                .HasOne(sb => sb.Server)
                .WithMany()
                .HasForeignKey(sb => sb.ServerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ServerBan>()
                .HasOne(sb => sb.User)
                .WithMany()
                .HasForeignKey(sb => sb.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Invite>()
                .HasOne(i => i.Server)
                .WithMany()
                .HasForeignKey(i => i.ServerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Invite>()
                .HasOne(i => i.Creator)
                .WithMany()
                .HasForeignKey(i => i.CreatorId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Server>()
                .HasOne(s => s.Owner)
                .WithMany(u => u.OwnedServers)
                .HasForeignKey(s => s.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Channel>()
                .HasOne(c => c.Server)
                .WithMany(s => s.Channels)
                .HasForeignKey(c => c.ServerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Message>()
                .HasOne(m => m.Channel)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ChannelId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Message>()
                .HasOne(m => m.Author)
                .WithMany(u => u.Messages)
                .HasForeignKey(m => m.AuthorId)
                .OnDelete(DeleteBehavior.Restrict); 
            builder.Entity<Reaction>()
                .HasOne(r => r.User)
                .WithMany() // Add a collection property like r.User.Reactions here if you ever define one
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.NoAction);
            builder.Entity<ChannelCategory>()
                .HasOne(cc => cc.Server)
                .WithMany(s => s.ChannelCategories)
                .HasForeignKey(cc => cc.ServerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ServerMember>()
                .HasOne(sm => sm.Server)
                .WithMany(s => s.Members)
                .HasForeignKey(sm => sm.ServerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ServerMember>()
                .HasOne(sm => sm.User)
                .WithMany()
                .HasForeignKey(sm => sm.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ServerRole>()
                .HasOne(sr => sr.Server)
                .WithMany(s => s.Roles)
                .HasForeignKey(sr => sr.ServerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ServerMemberRole>()
                .HasKey(smr => new { smr.ServerMemberId, smr.ServerRoleId });

            builder.Entity<ServerMemberRole>()
                .HasOne(smr => smr.ServerMember)
                .WithMany(sm => sm.MemberRoles)
                .HasForeignKey(smr => smr.ServerMemberId);

            builder.Entity<ServerMemberRole>()
                .HasOne(smr => smr.ServerRole)
                .WithMany(sr => sr.MemberRoles)
                .HasForeignKey(smr => smr.ServerRoleId)
                .OnDelete(DeleteBehavior.NoAction);
                
            builder.Entity<Message>()
                .HasQueryFilter(m => !m.IsDeleted);

            builder.Entity<Server>()
                .HasQueryFilter(s => !s.IsDeleted);

            builder.Entity<Channel>()
                .HasQueryFilter(c => !c.IsDeleted);

            builder.Entity<ApplicationUser>()
                .HasQueryFilter(u => !u.IsDeleted);

            builder.Entity<Reaction>()
                .HasQueryFilter(r => !r.Message.IsDeleted);

            

        }
    }
}