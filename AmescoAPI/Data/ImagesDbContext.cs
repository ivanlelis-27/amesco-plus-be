using Microsoft.EntityFrameworkCore;
using AmescoAPI.Models;

namespace AmescoAPI.Data
{
    public class ImagesDbContext : DbContext
    {
        public ImagesDbContext(DbContextOptions<ImagesDbContext> options) : base(options) { }
        public DbSet<AdBanner> AdBanners { get; set; }
        public DbSet<UserImage> UserImages { get; set; }

        public DbSet<PromoImage> PromoImages { get; set; }

        public DbSet<NotificationImage> NotificationImages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AdBanner>().ToTable("AdBanners");
            modelBuilder.Entity<UserImage>().ToTable("UserImages");
            modelBuilder.Entity<PromoImage>().ToTable("PromoImages");
            modelBuilder.Entity<NotificationImage>().ToTable("NotificationImages");
            modelBuilder.Entity<NotificationImage>().HasKey(n => n.ImageId);
        }
    }
}