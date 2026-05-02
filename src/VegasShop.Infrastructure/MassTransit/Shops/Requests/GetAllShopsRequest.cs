using VegasShop.Infrastructure.Models.Identity;

namespace VegasShop.Infrastructure.MassTransit.Shops.Requests
{
    public class GetAllShopsRequest
    {
        public User User { get; set; }
    }
}
