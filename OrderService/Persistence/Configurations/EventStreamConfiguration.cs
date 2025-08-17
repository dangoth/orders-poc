using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Models;

namespace OrderService.Persistence.Configurations
{
    public class EventStreamConfiguration : IEntityTypeConfiguration<EventStream>
    {
        public void Configure(EntityTypeBuilder<EventStream> builder)
        {
            builder.HasKey(e => e.AggregateId);
        }
    }
}