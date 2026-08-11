using Bogus;

namespace LinqStudio.Demo;

/// <summary>
/// Generator for creating fake demo data using Bogus.
/// </summary>
public static class BogusDataGenerator
{
	/// <summary>
	/// Generates fake customers.
	/// </summary>
	public static List<Customer> GenerateCustomers(int count = 500, DateTime? referenceDate = null)
	{
		var asOf = referenceDate ?? DateTime.UtcNow;
		var faker = new Faker<Customer>()
			.RuleFor(c => c.FirstName, f => f.Name.FirstName())
			.RuleFor(c => c.LastName, f => f.Name.LastName())
			.RuleFor(c => c.Email, (f, c) => f.Internet.Email(c.FirstName, c.LastName))
			.RuleFor(c => c.BirthDate, f => DateOnly.FromDateTime(f.Date.Between(
				DateTime.Today.AddYears(-80),
				DateTime.Today.AddYears(-18))))
			.RuleFor(c => c.PreferredContactTime, f => new TimeSpan(f.Random.Int(8, 17), f.Random.Int(0, 59), 0))
			.RuleFor(c => c.IsActive, f => f.Random.Bool(0.85f))
			.RuleFor(c => c.LoyaltyTier, f => f.Random.Short(1, 4))
			.RuleFor(c => c.LifetimePoints, f => f.Random.Long(0, 250_000))
			.RuleFor(c => c.CreatedDate, f => f.Date.Between(asOf.AddYears(-2), asOf));

		return faker.Generate(count);
	}

	/// <summary>
	/// Generates fake products.
	/// </summary>
	public static List<Product> GenerateProducts(int count = 200)
	{
		var faker = new Faker<Product>()
			.RuleFor(p => p.Name, f => f.Commerce.ProductName())
			.RuleFor(p => p.Description, f => f.Commerce.ProductDescription())
			.RuleFor(p => p.Sku, _ => Guid.NewGuid())
			.RuleFor(p => p.WeightKg, f => Math.Round(f.Random.Double(0.1, 25), 2))
			.RuleFor(p => p.Price, f => f.Random.Decimal(10, 1000))
			.RuleFor(p => p.StockQuantity, f => f.Random.Int(0, 100));

		return faker.Generate(count);
	}

	/// <summary>
	/// Generates fake orders for given customers.
	/// </summary>
	public static List<Order> GenerateOrders(List<Customer> customers, int ordersPerCustomer = 3, DateTime? referenceDate = null)
	{
		var asOf = referenceDate ?? DateTime.UtcNow;
		var orders = new List<Order>();

		foreach (var customer in customers)
		{
			var faker = new Faker<Order>()
				.RuleFor(o => o.CustomerId, _ => customer.Id)
				.RuleFor(o => o.OrderNumber, f => f.Random.AlphaNumeric(10).ToUpper())
				.RuleFor(o => o.OrderDate, f => f.Date.Between(customer.CreatedDate, asOf))
				.RuleFor(o => o.TotalAmount, f => f.Random.Decimal(50, 5000));

			orders.AddRange(faker.Generate(ordersPerCustomer));
		}

		return orders;
	}

	public static List<UserProfile> GenerateUserProfiles(int count = 100)
	{
		var faker = new Faker<UserProfile>()
			.RuleFor(p => p.DisplayName, f => f.Name.FullName())
			.RuleFor(p => p.Email, (f, p) => f.Internet.Email(p.DisplayName.Replace(" ", ".")));

		return faker.Generate(count);
	}

	public static List<SupportTicket> GenerateSupportTickets(IReadOnlyList<UserProfile> userProfiles, DateTime referenceDate, int count = 250)
	{
		var profiles = userProfiles.ToArray();
		var faker = new Faker<SupportTicket>()
			.RuleFor(t => t.UserId, f => f.PickRandom(profiles).Id)
			.RuleFor(t => t.Subject, f => f.Lorem.Sentence(6))
			.RuleFor(t => t.OpenedAt, f => f.Date.Between(referenceDate.AddYears(-1), referenceDate))
			.RuleFor(t => t.IsResolved, f => f.Random.Bool(0.7f));

		return faker.Generate(count);
	}

	/// <summary>
	/// Generates fake order items for given orders and products.
	/// </summary>
	public static List<OrderItem> GenerateOrderItems(List<Order> orders, List<Product> products)
	{
		var orderItems = new List<OrderItem>();
		var random = new Random();

		foreach (var order in orders)
		{
			var itemCount = random.Next(1, 5); // 1-4 items per order
			var selectedProducts = products.OrderBy(_ => random.Next()).Take(itemCount).ToList();

			foreach (var product in selectedProducts)
			{
				var faker = new Faker<OrderItem>()
					.RuleFor(oi => oi.OrderId, _ => order.Id)
					.RuleFor(oi => oi.ProductId, _ => product.Id)
					.RuleFor(oi => oi.Quantity, f => f.Random.Int(1, 10))
					.RuleFor(oi => oi.UnitPrice, _ => product.Price);

				orderItems.Add(faker.Generate());
			}
		}

		return orderItems;
	}
}
