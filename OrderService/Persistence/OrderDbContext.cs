using Microsoft.EntityFrameworkCore;
using OrderService.Models;

namespace OrderService.Persistence
{
    public class OrderDbContext : DbContext
    {
        public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderProduct> OrderProducts { get; set; }
        public DbSet<EventStore> Events { get; set; }
        public DbSet<EventStream> EventStreams { get; set; }

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
        }
    }
}
    