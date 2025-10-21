using Microsoft.EntityFrameworkCore;
using AmescoAPI.Models;

namespace AmescoAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Users> Users { get; set; }
        public DbSet<Memberships> Memberships { get; set; }
        public DbSet<UserSessions> UserSessions { get; set; }
        public DbSet<Points> Points { get; set; }
        public DbSet<UserImage> UserImages { get; set; } = null!;
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<TransactionProduct> TransactionProducts { get; set; }

        public DbSet<ExistingMember> ExistingMembers { get; set; }

        public DbSet<Branch> Branches { get; set; }

        public DbSet<AccessControl> AccessControls { get; set; }

        public DbSet<BranchPointsHistory> BranchPointsHistory { get; set; }

        public DbSet<Promo> Promos { get; set; }

        public DbSet<Notification> Notifications { get; set; }

        public DbSet<NotificationLike> NotificationLikes { get; set; }

        public DbSet<PromoGroup> PromoGroups { get; set; }

        public DbSet<Announcement> Announcements { get; set; }

        public DbSet<AnnouncementProduct> AnnouncementProducts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Users>().HasKey(u => u.UserId);
            modelBuilder.Entity<Memberships>().HasKey(m => m.MemberId);
            modelBuilder.Entity<UserSessions>().HasKey(s => s.SessionId);

            modelBuilder.Entity<Memberships>()
                .HasOne(m => m.User)
                .WithOne(u => u.Membership)
                .HasForeignKey<Memberships>(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserSessions>()
                .HasOne(s => s.User)
                .WithMany(u => u.Sessions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Voucher>()
                .Property(v => v.VoucherId)
                .ValueGeneratedNever();

            modelBuilder.Entity<Branch>()
            .Property(b => b.Latitude)
            .HasColumnType("decimal(18,15)");

            modelBuilder.Entity<Branch>()
                .Property(b => b.Longitude)
                .HasColumnType("decimal(18,15)");

            modelBuilder.Entity<NotificationLike>().HasKey(nl => nl.LikeId);
        }



    }
}
