using Microsoft.EntityFrameworkCore;

namespace LinqStudio.Demo;

/// <summary>
/// DbContext for demo database with customers, orders, products, and order items.
/// </summary>
public class DemoDbContext : DbContext
{
	public DemoDbContext(DbContextOptions<DemoDbContext> options) : base(options)
	{
	}

	public DbSet<Customer> Customers { get; set; } = null!;
	public DbSet<Order> Orders { get; set; } = null!;
	public DbSet<Product> Products { get; set; } = null!;
	public DbSet<OrderItem> OrderItems { get; set; } = null!;
	public DbSet<UserProfile> UserProfiles { get; set; } = null!;
	public DbSet<SupportTicket> SupportTickets { get; set; } = null!;

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		// Customer configuration
		modelBuilder.Entity<Customer>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
			entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
			entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
			entity.Property(e => e.BirthDate)
				.IsRequired()
				.HasColumnType("date")
				.HasConversion(
					value => value.ToDateTime(TimeOnly.MinValue),
					value => DateOnly.FromDateTime(value));
			entity.Property(e => e.PreferredContactTime).IsRequired();
			entity.Property(e => e.IsActive).IsRequired();
			entity.Property(e => e.LoyaltyTier).IsRequired();
			entity.Property(e => e.LifetimePoints).IsRequired();
			entity.Property(e => e.CreatedDate).IsRequired();
		});

		// Order configuration
		modelBuilder.Entity<Order>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
			entity.Property(e => e.OrderDate).IsRequired();
			entity.Property(e => e.TotalAmount).HasPrecision(18, 2);

			entity.HasOne(e => e.Customer)
				.WithMany(c => c.Orders)
				.HasForeignKey(e => e.CustomerId)
				.OnDelete(DeleteBehavior.Cascade);
		});

		// Product configuration
		modelBuilder.Entity<Product>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
			entity.Property(e => e.Description).IsRequired().HasMaxLength(1000);
			entity.Property(e => e.Sku).IsRequired();
			entity.Property(e => e.WeightKg).IsRequired();
			entity.Property(e => e.Price).HasPrecision(18, 2);
			entity.Property(e => e.StockQuantity).IsRequired();
		});

		// OrderItem configuration
		modelBuilder.Entity<OrderItem>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Quantity).IsRequired();
			entity.Property(e => e.UnitPrice).HasPrecision(18, 2);

			entity.HasOne(e => e.Order)
				.WithMany(o => o.OrderItems)
				.HasForeignKey(e => e.OrderId)
				.OnDelete(DeleteBehavior.Cascade);

			entity.HasOne(e => e.Product)
				.WithMany(p => p.OrderItems)
				.HasForeignKey(e => e.ProductId)
				.OnDelete(DeleteBehavior.Restrict);
		});

		// These tables deliberately have no relationship configuration. They are
		// used to exercise custom relationships in the application.
		modelBuilder.Entity<UserProfile>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(150);
			entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
		});

		modelBuilder.Entity<SupportTicket>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.UserId).IsRequired();
			entity.Property(e => e.Subject).IsRequired().HasMaxLength(250);
			entity.Property(e => e.OpenedAt).IsRequired();
			entity.Property(e => e.IsResolved).IsRequired();
		});
	}
}
