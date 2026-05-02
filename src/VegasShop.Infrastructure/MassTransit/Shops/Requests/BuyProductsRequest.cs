using System.Collections.Generic;
using VegasShop.Infrastructure.Models.Identity;
using VegasShop.Infrastructure.Models.Shops;

namespace VegasShop.Infrastructure.MassTransit.Shops.Requests
{
    public class BuyProductsRequest
    {
        public User User { get; set; }
        public int ShopId { get; set; }
        public ICollection<Product> Products { get; set; }
    }
}
