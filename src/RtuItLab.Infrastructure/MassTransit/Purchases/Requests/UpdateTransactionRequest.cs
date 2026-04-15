namespace VegasShop.Infrastructure.MassTransit.Purchases.Requests
{
    public class UpdateTransactionRequest
    {
        public string UserId { get; set; }
        public int TransactionId { get; set; }
        public int TransactionType { get; set; }
    }
}
