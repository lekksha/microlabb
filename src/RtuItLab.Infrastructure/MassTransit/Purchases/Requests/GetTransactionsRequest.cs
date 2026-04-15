namespace VegasShop.Infrastructure.MassTransit.Purchases.Requests
{
    public class GetTransactionsRequest
    {
        public string UserId { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
