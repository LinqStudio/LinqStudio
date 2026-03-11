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
		var orders = BogusDataGenerator.GenerateOrders(customers);
		var orderItems = BogusDataGenerator.GenerateOrderItems(orders, products);

		await context.AddRangeAsync(customers);
		await context.AddRangeAsync(products);
		await context.SaveChangesAsync();
		await context.AddRangeAsync(orders);
		await context.SaveChangesAsync();
		await context.AddRangeAsync(orderItems);
		await context.SaveChangesAsync();
	}
}
