using BounceRoute.Api.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BounceRoute.Api.Data;

public sealed class BounceRouteDbContext(DbContextOptions<BounceRouteDbContext> options) : IdentityDbContext<AppUser>(options)
{
	public DbSet<Booking> Bookings => Set<Booking>();

	public DbSet<Job> Jobs => Set<Job>();

	public DbSet<BookingItem> BookingItems => Set<BookingItem>();

	public DbSet<CrewMember> CrewMembers => Set<CrewMember>();

	public DbSet<JobAssignment> JobAssignments => Set<JobAssignment>();

	public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

	protected override void OnModelCreating(ModelBuilder model)
	{
		base.OnModelCreating(model);

		model.HasSequence<int>("job_numbers").StartsAt(1001);

		model.Entity<Booking>(booking =>
		{
			booking.Property(b => b.JobNumber).HasDefaultValueSql("nextval('job_numbers')");
			booking.HasIndex(b => b.JobNumber).IsUnique();
			booking.Property(b => b.CustomerName).HasMaxLength(100);
			booking.Property(b => b.Phone).HasMaxLength(20);
			booking.Property(b => b.Street).HasMaxLength(150);
			booking.Property(b => b.CityStateZip).HasMaxLength(100);
			booking.Property(b => b.SiteNotes).HasMaxLength(500);
			booking.Property(b => b.Surface).HasConversion<string>().HasMaxLength(20);
		});

		model.Entity<Job>(job =>
		{
			job.HasIndex(j => j.ScheduledAt);
			job.HasIndex(j => new { j.BookingId, j.Type }).IsUnique();
			job.Property(j => j.Truck).HasMaxLength(30);
			job.Property(j => j.Type).HasConversion<string>().HasMaxLength(10);
			job.Property(j => j.Status).HasConversion<string>().HasMaxLength(10);
			job.HasOne(j => j.CompletedBy).WithMany().OnDelete(DeleteBehavior.SetNull);
		});

		model.Entity<BookingItem>(item => item.Property(i => i.Name).HasMaxLength(80));

		model.Entity<InventoryItem>(item =>
		{
			item.HasIndex(i => i.Name).IsUnique();
			item.Property(i => i.Name).HasMaxLength(80);
			item.Property(i => i.Notes).HasMaxLength(500);
			item.Property(i => i.Category).HasConversion<string>().HasMaxLength(20);
			item.Property(i => i.WaterUse).HasConversion<string>().HasMaxLength(10);
			item.Property(i => i.DailyPrice).HasPrecision(8, 2);
			item.ToTable(table => table.HasCheckConstraint("CK_InventoryItems_Quantity", "\"Quantity\" >= 1"));
		});

		model.Entity<CrewMember>(crew =>
		{
			crew.Property(c => c.Name).HasMaxLength(80);
			crew.Property(c => c.Initials).HasMaxLength(3);
			crew.Property(c => c.Role).HasMaxLength(50);
		});

		model.Entity<AppUser>(user =>
		{
			user.Property(u => u.DisplayName).HasMaxLength(80);
			user.HasOne(u => u.CrewMember).WithMany().OnDelete(DeleteBehavior.SetNull);
			user.HasIndex(u => u.CrewMemberId).IsUnique();
		});

		model.Entity<JobAssignment>(assignment =>
		{
			assignment.HasKey(a => new { a.JobId, a.CrewMemberId });
			assignment.HasOne<Job>().WithMany(j => j.Assignments).HasForeignKey(a => a.JobId);
			assignment.HasOne(a => a.CrewMember).WithMany().HasForeignKey(a => a.CrewMemberId).OnDelete(DeleteBehavior.Restrict);
		});
	}
}
