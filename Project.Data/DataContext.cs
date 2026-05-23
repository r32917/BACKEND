using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace project
{
    public class DataContext : DbContext
    {
        private readonly IConfiguration _configuration;

        public DataContext(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public DbSet<Babies> babies { get; set; }
        public DbSet<Nurses> nurses { get; set; }
        public DbSet<Turns> turns { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(_configuration.GetConnectionString("DefaultConnection"));
        }
    }
}
