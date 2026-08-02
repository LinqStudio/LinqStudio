using Microsoft.EntityFrameworkCore;

namespace LinqStudio.Demo;

/// <summary>
/// Seeds demo data into database contexts.
/// </summary>
public static class DemoSeeder
{
	public static async Task SeedAsync(DbContext context)
	{
		await context.Database.EnsureCreatedAsync();
		if (await context.Set<Customer>().AnyAsync()) return; // already seeded

		var customers = BogusDataGenerator.GenerateCustomers();
		var products = BogusDataGenerator.GenerateProducts();
		var userProfiles = new[]
		{
			new UserProfile { DisplayName = "Avery Johnson", Email = "avery.johnson@example.test" },
			new UserProfile { DisplayName = "Morgan Lee", Email = "morgan.lee@example.test" },
			new UserProfile { DisplayName = "Riley Smith", Email = "riley.smith@example.test" },
		};
		var supportTickets = new[]
		{
			new SupportTicket { UserId = 1, Subject = "Cannot update billing address", OpenedAt = DateTime.UtcNow.AddDays(-5), IsResolved = false },
			new SupportTicket { UserId = 2, Subject = "Question about order history", OpenedAt = DateTime.UtcNow.AddDays(-3), IsResolved = true },
			new SupportTicket { UserId = 3, Subject = "Export is missing a column", OpenedAt = DateTime.UtcNow.AddDays(-1), IsResolved = false },
		};

		await context.AddRangeAsync(customers);
		await context.AddRangeAsync(products);
		await context.AddRangeAsync(userProfiles);
		await context.AddRangeAsync(supportTickets);
		await context.SaveChangesAsync(); // customers and products now have real DB-assigned IDs

		var orders = BogusDataGenerator.GenerateOrders(customers);
		await context.AddRangeAsync(orders);
		await context.SaveChangesAsync(); // orders now have real DB-assigned IDs

		var orderItems = BogusDataGenerator.GenerateOrderItems(orders, products);
		await context.AddRangeAsync(orderItems);
		await context.SaveChangesAsync();
	}
}
