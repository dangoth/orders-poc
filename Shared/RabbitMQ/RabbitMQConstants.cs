namespace Shared.RabbitMQ
{
    public static class RabbitMQConstants
    {
        public const string OrdersExchange = "orders_exchange";
        public const string OrdersQueue = "orders_queue";
        public const string OrdersRoutingKey = "orders";

        public const string ProcessedOrdersExchange = "processed_orders_exchange";
        public const string ProcessedOrdersQueue = "processed_orders_queue";
        public const string ProcessedOrdersRoutingKey = "processed_orders";
    }
}