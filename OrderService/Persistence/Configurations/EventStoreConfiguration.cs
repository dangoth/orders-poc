using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Models;

namespace OrderService.Persistence.Configurations
{
    public class EventStoreConfiguration : IEntityTypeConfiguration<EventStore>
    {
        public void Configure(EntityTypeBuilder<EventStore> builder)
        {
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => e.AggregateId);
            builder.HasIndex(e => e.Timestamp);
            builder.Property(e => e.Data).HasColumnType("nvarchar(max)");
        }
    }
}