using Microsoft.EntityFrameworkCore;

namespace back.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options): base (options)
        {
            
        }

        public DbSet<Models.Visits> Visits { get; set; }
    }
}
