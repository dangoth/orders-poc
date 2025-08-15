using Shared.Models;

namespace OrderService.Models
{
    public static class OrderMessageFactory
    {
        public static OrderMessage CreateFromAggregate(OrderAggregate aggregate)
        {
            return new OrderMessage
            {
                OrderId = ParseOrderId(aggregate.Id),
                CustomerName = aggregate.CustomerName,
                TotalAmount = aggregate.TotalAmount,
                ProductIds = aggregate.ProductIds.ToArray(),
                Items = aggregate.Items,
                Status = aggregate.Status
            };
        }

        public static OrderMessage CreateFromAggregate(OrderAggregate aggregate, Shared.Models.OrderStatus status)
        {
            var orderMessage = CreateFromAggregate(aggregate);
            orderMessage.Status = status;
            return orderMessage;
        }

        public static OrderMessage CreateFromAggregate(OrderAggregate aggregate, Shared.Models.OrderStatus status, string reason)
        {
            var orderMessage = CreateFromAggregate(aggregate, status);
            orderMessage.Reason = reason;
            return orderMessage;
        }

        private static int ParseOrderId(string aggregateId)
        {
            try
            {
                return int.Parse(aggregateId.Substring(0, 8), System.Globalization.NumberStyles.HexNumber);
            }
            catch
            {
                return aggregateId.GetHashCode();
            }
        }
    }
}