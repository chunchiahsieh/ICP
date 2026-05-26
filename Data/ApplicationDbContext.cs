using Microsoft.EntityFrameworkCore;

namespace ICP.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // 之後新增 Entity 時，把 DbSet 加在這裡，例如：
    // public DbSet<Customer> Customers => Set<Customer>();
}
