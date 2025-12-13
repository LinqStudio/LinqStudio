using Bogus;

namespace LinqStudio.App.WebServer.TestData;

/// <summary>
/// Entity representing a customer in the test database.
/// </summary>
public class Customer
{
	public int Id { get; set; }
	public required string FirstName { get; set; }
	public required string LastName { get; set; }
	public required string Email { get; set; }
	public DateTime CreatedDate { get; set; }
	public ICollection<Order> Orders { get; set; } = new List<Order>();
}

/// <summary>
/// Entity representing an order in the test database.
/// </summary>
public class Order
{
	public int Id { get; set; }
	public int CustomerId { get; set; }
	public required string OrderNumber { get; set; }
	public DateTime OrderDate { get; set; }
	public decimal TotalAmount { get; set; }
	public Customer Customer { get; set; } = null!;
	public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

/// <summary>
/// Entity representing a product in the test database.
/// </summary>
public class Product
{
	public int Id { get; set; }
	public required string Name { get; set; }
	public required string Description { get; set; }
	public decimal Price { get; set; }
	public int StockQuantity { get; set; }
	public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

/// <summary>
/// Entity representing an order item in the test database (junction table).
/// </summary>
public class OrderItem
{
	public int Id { get; set; }
	public int OrderId { get; set; }
	public int ProductId { get; set; }
	public int Quantity { get; set; }
	public decimal UnitPrice { get; set; }
	public Order Order { get; set; } = null!;
	public Product Product { get; set; } = null!;
}

/// <summary>
/// Generator for creating fake test data using Bogus.
/// </summary>
public static class BogusDataGenerator
{
	/// <summary>
	/// Generates fake customers.
	/// </summary>
	public static List<Customer> GenerateCustomers(int count = 10)
	{
		var faker = new Faker<Customer>()
			.RuleFor(c => c.FirstName, f => f.Name.FirstName())
			.RuleFor(c => c.LastName, f => f.Name.LastName())
			.RuleFor(c => c.Email, (f, c) => f.Internet.Email(c.FirstName, c.LastName))
			.RuleFor(c => c.CreatedDate, f => f.Date.Past(2));

		return faker.Generate(count);
	}

	/// <summary>
	/// Generates fake products.
	/// </summary>
	public static List<Product> GenerateProducts(int count = 20)
	{
		var faker = new Faker<Product>()
			.RuleFor(p => p.Name, f => f.Commerce.ProductName())
			.RuleFor(p => p.Description, f => f.Commerce.ProductDescription())
			.RuleFor(p => p.Price, f => f.Random.Decimal(10, 1000))
			.RuleFor(p => p.StockQuantity, f => f.Random.Int(0, 100));

		return faker.Generate(count);
	}

	/// <summary>
	/// Generates fake orders for given customers.
	/// </summary>
	public static List<Order> GenerateOrders(List<Customer> customers, int ordersPerCustomer = 3)
	{
		var orders = new List<Order>();

		foreach (var customer in customers)
		{
			var faker = new Faker<Order>()
				.RuleFor(o => o.CustomerId, _ => customer.Id)
				.RuleFor(o => o.OrderNumber, f => f.Random.AlphaNumeric(10).ToUpper())
				.RuleFor(o => o.OrderDate, f => f.Date.Between(customer.CreatedDate, DateTime.Now))
				.RuleFor(o => o.TotalAmount, f => f.Random.Decimal(50, 5000));

			orders.AddRange(faker.Generate(ordersPerCustomer));
		}

		return orders;
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
