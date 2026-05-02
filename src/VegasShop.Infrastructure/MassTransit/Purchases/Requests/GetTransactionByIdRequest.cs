using VegasShop.Infrastructure.Models.Identity;

namespace VegasShop.Infrastructure.MassTransit.Purchases.Requests
{
    public class GetTransactionByIdRequest
    {
        public User User { get; set; }
        public int Id { get; set; }
    }
}
