namespace VegasShop.Infrastructure.MassTransit.Purchases.Requests
{
    public class GetTransactionByIdRequest
    {
        public string UserId { get; set; }
        public int TransactionId { get; set; }
    }
}
