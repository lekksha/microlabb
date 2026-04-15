using System.Collections.Generic;

namespace VegasShop.Infrastructure.MassTransit.Shops.Requests
{
    public class BuyProductsRequest
    {
        public string UserId { get; set; }
        public int ShopId { get; set; }
        public List<int> ProductIds { get; set; }
    }
}
