namespace LinqStudio.Demo;

/// <summary>
/// Entity representing a customer in the demo database.
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
/// Entity representing an order in the demo database.
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
/// Entity representing a product in the demo database.
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
/// Entity representing an order item in the demo database (junction table).
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
