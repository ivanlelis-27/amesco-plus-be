using Microsoft.EntityFrameworkCore;
using AmescoAPI.Models;

namespace AmescoAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Users> Users { get; set; }
        public DbSet<Points> Points { get; set; }
       public DbSet<UserImage> UserImages { get; set; } = null!;

    }
}
