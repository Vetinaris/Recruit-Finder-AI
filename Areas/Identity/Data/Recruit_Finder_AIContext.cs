using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Recruit_Finder_AI.Models;
using Recruit_Finder_AI.Entities;

namespace Recruit_Finder_AI.Data;

public class Recruit_Finder_AIContext : IdentityDbContext<ApplicationUser>
{
    public Recruit_Finder_AIContext(DbContextOptions<Recruit_Finder_AIContext> options)
        : base(options)
    {
    }
    public DbSet<JobOffer> JobOffers { get; set; }
    public DbSet<PasswordHistory> PasswordHistories { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<SystemSetting> SystemSettings { get; set; }
    public DbSet<Notification> Notifications { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.NIP).HasMaxLength(15);
        });

        builder.Entity<Notification>(entity =>
        {
            entity.HasOne(n => n.User)
                  .WithMany()
                  .HasForeignKey(n => n.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}