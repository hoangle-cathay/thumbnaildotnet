using Microsoft.EntityFrameworkCore;

namespace ThumbnailService.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<ImageUpload> ImageUploads => Set<ImageUpload>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<ImageUpload>()
                .HasIndex(i => new { i.UserId, i.UploadedAtUtc });
        }
    }
}


