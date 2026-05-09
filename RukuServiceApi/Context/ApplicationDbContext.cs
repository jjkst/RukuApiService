using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RukuServiceApi.Models;

namespace RukuServiceApi.Context;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<Service> Services { get; set; }
    public DbSet<Availability> Availabilities { get; set; }
    public DbSet<Schedule> Schedules { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var pricingPlansConverter = new ValueConverter<List<PricingPlan>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v =>
                JsonSerializer.Deserialize<List<PricingPlan>>(v, (JsonSerializerOptions?)null)
                ?? new()
        );

        var pricingPlansComparer = new ValueComparer<List<PricingPlan>>(
            (l, r) =>
                l != null
                && r != null
                && l.Count == r.Count
                && l.SequenceEqual(r),
            v => v != null ? v.Aggregate(0, (hash, plan) => HashCode.Combine(hash, plan)) : 0,
            v => v != null ? v.Select(plan => plan).ToList() : new List<PricingPlan>()
        );

        modelBuilder.Entity<Service>().HasIndex(p => p.Title).IsUnique();
        modelBuilder
            .Entity<Service>()
            .Property(s => s.PricingPlans)
            .HasConversion(pricingPlansConverter)
            .Metadata.SetValueComparer(pricingPlansComparer);

        // Add indexes for frequently queried columns
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.Uid).IsUnique();
        modelBuilder.Entity<Schedule>().HasIndex(s => s.SelectedDate);
        modelBuilder.Entity<Schedule>().HasIndex(s => s.Uid);
        modelBuilder.Entity<Availability>().HasIndex(a => new { a.StartDate, a.EndDate });

        base.OnModelCreating(modelBuilder);
    }
}
