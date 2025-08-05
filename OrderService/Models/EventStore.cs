using System.ComponentModel.DataAnnotations;

namespace OrderService.Models
{
    public class EventStore
    {
        [Key]
        public Guid Id { get; set; }
        public string AggregateId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public long Version { get; set; }
        public string Data { get; set; } = string.Empty;
        public string? CorrelationId { get; set; }
        public string? CausationId { get; set; }
    }

    public class EventStream
    {
        [Key]
        public string AggregateId { get; set; } = string.Empty;
        public long CurrentVersion { get; set; }
        public DateTime LastModified { get; set; }
    }
} 