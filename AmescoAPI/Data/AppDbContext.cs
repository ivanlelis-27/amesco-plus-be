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
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<TransactionProduct> TransactionProducts { get; set; }

        public DbSet<ExistingMember> ExistingMembers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Voucher>()
                .Property(v => v.VoucherId)
                .ValueGeneratedNever();
        }

    }
}
