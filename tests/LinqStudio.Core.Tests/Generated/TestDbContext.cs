using Microsoft.EntityFrameworkCore;

namespace Test;
public class TestDbContext : DbContext
{
    public DbSet<Person> People { get; set; }
}
