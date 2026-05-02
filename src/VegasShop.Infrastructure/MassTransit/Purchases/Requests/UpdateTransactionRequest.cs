using VegasShop.Infrastructure.Models.Identity;
using VegasShop.Infrastructure.Models.Purchases;

namespace VegasShop.Infrastructure.MassTransit.Purchases.Requests
{
    public class UpdateTransactionRequest
    {
        public User User { get; set; }
        public UpdateTransaction Transaction { get; set; }
    }
}
