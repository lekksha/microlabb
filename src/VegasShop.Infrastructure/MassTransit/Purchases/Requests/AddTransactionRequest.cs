using VegasShop.Infrastructure.Models.Identity;
using VegasShop.Infrastructure.Models.Purchases;

namespace VegasShop.Infrastructure.MassTransit.Purchases.Requests
{
    public class AddTransactionRequest
    {
        public User User { get; set; }
        public Transaction Transaction { get; set; }
    }
}
