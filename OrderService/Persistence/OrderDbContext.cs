using Microsoft.EntityFrameworkCore;
using OrderService.Models;

namespace OrderService.Persistence
{
    public class OrderDbContext : DbContext
    {
        public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

        public DbSet<EventStore> Events { get; set; }
        public DbSet<EventStream> EventStreams { get; set; }
        
        // Inventory tables
        public DbSet<Product> Products { get; set; }
        public DbSet<InventoryItem> InventoryItems { get; set; }
        public DbSet<InventoryReservation> InventoryReservations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<EventStore>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.AggregateId);
                entity.HasIndex(e => e.Timestamp);
                entity.Property(e => e.Data).HasColumnType("nvarchar(max)");
            });

            modelBuilder.Entity<EventStream>(entity =>
            {
                entity.HasKey(e => e.AggregateId);
            });

            // Inventory configuration
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.ProductId);
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<InventoryItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ProductId).IsUnique();
                entity.HasOne(e => e.Product)
                    .WithMany(p => p.InventoryItems)
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<InventoryReservation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.OrderId);
                entity.HasIndex(e => new { e.OrderId, e.ProductId });
                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
    