using Microsoft.EntityFrameworkCore;
using SmileyApi.Core.Models;

namespace SmileyApi.Infrastructure.Data;

public class SmileyDbContext(DbContextOptions<SmileyDbContext> options) : DbContext(options)
{
    public DbSet<Establishment> Establishments => Set<Establishment>();
    public DbSet<Inspection> Inspections => Set<Inspection>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<BusinessLocation> BusinessLocations => Set<BusinessLocation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Establishment>(e =>
        {
            e.HasIndex(x => x.Navnelbnr).IsUnique();
            e.Property(x => x.Name).HasMaxLength(512);
            e.Property(x => x.CvrNumber).HasMaxLength(20);
            e.Property(x => x.Address).HasMaxLength(512);
            e.Property(x => x.PostalCode).HasMaxLength(10);
            e.Property(x => x.City).HasMaxLength(256);
            e.Property(x => x.IndustryCode).HasMaxLength(32);
            e.Property(x => x.IndustryName).HasMaxLength(256);
            e.Property(x => x.ReportUrl).HasMaxLength(1024);
        });

        modelBuilder.Entity<Inspection>(e =>
        {
            e.HasIndex(x => new { x.EstablishmentId, x.InspectedOn }).IsUnique();
            e.HasOne(x => x.Establishment)
             .WithMany(x => x.Inspections)
             .HasForeignKey(x => x.EstablishmentId);
        });

        modelBuilder.Entity<ApiKey>(e =>
        {
            e.Property(x => x.KeyHash).HasMaxLength(64);
            e.Property(x => x.OwnerEmail).HasMaxLength(256);
            e.Property(x => x.Tier).HasMaxLength(32);
        });

        modelBuilder.Entity<WebhookSubscription>(e =>
        {
            e.Property(x => x.CallbackUrl).HasMaxLength(1024);
            e.Property(x => x.SecretKey).HasMaxLength(64);
            e.HasOne(x => x.ApiKey).WithMany().HasForeignKey(x => x.ApiKeyId);
            e.HasOne(x => x.Establishment).WithMany().HasForeignKey(x => x.EstablishmentId);
        });

        modelBuilder.Entity<AccessRequest>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(256);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Company).HasMaxLength(256);
            e.Property(x => x.UseCase).HasMaxLength(2000);
            e.HasOne(x => x.ApiKey)
             .WithMany()
             .HasForeignKey(x => x.ApiKeyId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Business>(e =>
        {
            e.HasIndex(x => x.BusinessId).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.BusinessId).HasMaxLength(32);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.CompanyName).HasMaxLength(256);
            e.Property(x => x.Tier).HasMaxLength(16);
            e.Property(x => x.MagicLinkToken).HasMaxLength(64);
            e.Property(x => x.StripeCustomerId).HasMaxLength(64);
            e.Property(x => x.StripeSubscriptionId).HasMaxLength(64);
        });

        modelBuilder.Entity<BusinessLocation>(e =>
        {
            e.HasIndex(x => x.Navnelbnr);
            e.HasIndex(x => new { x.BusinessId, x.Navnelbnr }).IsUnique();
            e.HasOne(x => x.Business)
             .WithMany(x => x.Locations)
             .HasForeignKey(x => x.BusinessId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
