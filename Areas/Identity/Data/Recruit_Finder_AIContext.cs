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
    public DbSet<Cv> Cvs { get; set; }
    public DbSet<JobApplication> Applications { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.NIP).HasMaxLength(15);

            entity.HasMany(u => u.Cvs)
                  .WithOne(c => c.User)
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Notification>(entity =>
        {
            entity.HasOne(n => n.User)
                  .WithMany()
                  .HasForeignKey(n => n.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<JobApplication>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.HasOne(a => a.JobOffer)
                  .WithMany(o => o.Applications)
                  .HasForeignKey(a => a.JobOfferId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Cv)
                  .WithMany()
                  .HasForeignKey(a => a.CvId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Candidate)
                  .WithMany()
                  .HasForeignKey(a => a.CandidateId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(a => a.Status).HasMaxLength(50).HasDefaultValue("Pending");
            entity.Property(a => a.AppliedAt).IsRequired();
        });
        builder.Entity<Cv>(entity =>
        {
            entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Surname).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Email).IsRequired().HasMaxLength(255);

            entity.Property(c => c.ProfessionalExperience).IsRequired();
            entity.Property(c => c.Education).IsRequired();

            entity.Property(c => c.PhoneNumber).HasMaxLength(20);
            entity.Property(c => c.Address).HasMaxLength(250);
            entity.Property(c => c.DateOfBirth).HasMaxLength(50);
            entity.Property(c => c.Skills).IsRequired(false);
            entity.Property(c => c.Languages).IsRequired(false);
            entity.Property(c => c.Portfolio).HasMaxLength(500).IsRequired(false);
            entity.Property(c => c.Interests).HasMaxLength(500).IsRequired(false);
            entity.Property(c => c.AiFeedback).IsRequired(false);
        });
    }
}