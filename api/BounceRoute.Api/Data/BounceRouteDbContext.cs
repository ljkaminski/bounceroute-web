using BounceRoute.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BounceRoute.Api.Data;

public sealed class BounceRouteDbContext(DbContextOptions<BounceRouteDbContext> options) : DbContext(options)
{
	public DbSet<Booking> Bookings => Set<Booking>();

	public DbSet<Job> Jobs => Set<Job>();

	public DbSet<BookingItem> BookingItems => Set<BookingItem>();

	public DbSet<CrewMember> CrewMembers => Set<CrewMember>();

	public DbSet<JobAssignment> JobAssignments => Set<JobAssignment>();

	protected override void OnModelCreating(ModelBuilder model)
	{
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

		model.Entity<CrewMember>(crew =>
		{
			crew.Property(c => c.Name).HasMaxLength(80);
			crew.Property(c => c.Initials).HasMaxLength(3);
			crew.Property(c => c.Role).HasMaxLength(50);
		});

		model.Entity<JobAssignment>(assignment =>
		{
			assignment.HasKey(a => new { a.JobId, a.CrewMemberId });
			assignment.HasOne<Job>().WithMany(j => j.Assignments).HasForeignKey(a => a.JobId);
			assignment.HasOne(a => a.CrewMember).WithMany().HasForeignKey(a => a.CrewMemberId).OnDelete(DeleteBehavior.Restrict);
		});
	}
}
