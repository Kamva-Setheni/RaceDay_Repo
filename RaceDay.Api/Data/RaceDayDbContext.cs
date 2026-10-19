using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// Entity Framework Core context. The configuration below mirrors the Part 1 ERD and
/// raceday-schema.sql exactly (same tables, keys, constraints, defaults and constraint names).
/// </summary>
public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ---------- Users ----------
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users", t =>
                t.HasCheckConstraint("CK_Users_Role", "[Role] IN ('Organiser', 'Participant')"));

            entity.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(150).IsRequired();
            entity.HasIndex(u => u.Email).IsUnique().HasDatabaseName("UQ_Users_Email");
            entity.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(u => u.Role).HasMaxLength(20).IsRequired();
            entity.Property(u => u.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
        });

        // ---------- Events ----------
        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("Events", t =>
                t.HasCheckConstraint("CK_Events_EventType", "[EventType] IN ('Run', 'Walk', 'Cycle')"));

            entity.Property(e => e.Name).HasMaxLength(150).IsRequired();
            entity.Property(e => e.EventDate).HasColumnType("datetime");
            entity.Property(e => e.Location).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Distance).HasPrecision(6, 2);
            entity.Property(e => e.EventType).HasMaxLength(10).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");

            entity.HasOne(e => e.Organiser)
                  .WithMany(u => u.Events)
                  .HasForeignKey(e => e.OrganiserId)
                  .HasConstraintName("FK_Events_Users")
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Categories ----------
        modelBuilder.Entity<Category>(entity =>
        {
            entity.Property(c => c.Name).HasMaxLength(100).IsRequired();
            entity.Property(c => c.Distance).HasPrecision(6, 2);
            entity.Property(c => c.EntryFee).HasPrecision(8, 2).HasDefaultValue(0m);

            entity.HasOne(c => c.Event)
                  .WithMany(e => e.Categories)
                  .HasForeignKey(c => c.EventId)
                  .HasConstraintName("FK_Categories_Events")
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Enrolments ----------
        modelBuilder.Entity<Enrolment>(entity =>
        {
            entity.Property(e => e.EnrolmentDate).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Pending");

            entity.HasOne(e => e.Participant)
                  .WithMany(u => u.Enrolments)
                  .HasForeignKey(e => e.ParticipantId)
                  .HasConstraintName("FK_Enrolments_Users")
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Enrolments)
                  .HasForeignKey(e => e.CategoryId)
                  .HasConstraintName("FK_Enrolments_Categories")
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.ParticipantId, e.CategoryId })
                  .IsUnique()
                  .HasDatabaseName("UQ_Enrolments_Participant_Category");
        });

        // ---------- Results (one-to-one with Enrolments) ----------
        modelBuilder.Entity<Result>(entity =>
        {
            entity.Property(r => r.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Not Started");

            entity.HasOne(r => r.Enrolment)
                  .WithOne(e => e.Result)
                  .HasForeignKey<Result>(r => r.EnrolmentId)
                  .HasConstraintName("FK_Results_Enrolments")
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(r => r.EnrolmentId).IsUnique().HasDatabaseName("UQ_Results_EnrolmentId");
        });

        // ---------- Payments (one-to-one with Enrolments) ----------
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.Property(p => p.Amount).HasPrecision(8, 2);
            entity.Property(p => p.PaymentDate).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            entity.Property(p => p.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Pending");

            entity.HasOne(p => p.Enrolment)
                  .WithOne(e => e.Payment)
                  .HasForeignKey<Payment>(p => p.EnrolmentId)
                  .HasConstraintName("FK_Payments_Enrolments")
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(p => p.EnrolmentId).IsUnique().HasDatabaseName("UQ_Payments_EnrolmentId");
        });
    }
}
