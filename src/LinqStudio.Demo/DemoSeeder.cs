using Microsoft.EntityFrameworkCore;

namespace LinqStudio.Demo;

/// <summary>
/// Seeds demo data into database contexts.
/// </summary>
public static class DemoSeeder
{
	public static async Task SeedAsync(DbContext context, DateTime? snapshotDate = null)
	{
		await context.Database.EnsureCreatedAsync();
		if (await context.Set<Customer>().AnyAsync()) return; // already seeded

		var asOf = snapshotDate ?? DateTime.UtcNow;
		var customers = BogusDataGenerator.GenerateCustomers(referenceDate: asOf);
		var products = BogusDataGenerator.GenerateProducts();
		var userProfiles = BogusDataGenerator.GenerateUserProfiles();

		await context.AddRangeAsync(customers);
		await context.AddRangeAsync(products);
		await context.AddRangeAsync(userProfiles);
		await context.SaveChangesAsync(); // customers and products now have real DB-assigned IDs

		var orders = BogusDataGenerator.GenerateOrders(customers, referenceDate: asOf);
		await context.AddRangeAsync(orders);
		await context.SaveChangesAsync(); // orders now have real DB-assigned IDs

		var orderItems = BogusDataGenerator.GenerateOrderItems(orders, products);
		await context.AddRangeAsync(orderItems);
		await context.SaveChangesAsync();

		var supportTickets = BogusDataGenerator.GenerateSupportTickets(userProfiles, asOf);
		await context.AddRangeAsync(supportTickets);
		await context.SaveChangesAsync();
	}
}
