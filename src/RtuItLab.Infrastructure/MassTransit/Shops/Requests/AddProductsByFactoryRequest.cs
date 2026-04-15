using System.Collections.Generic;

namespace VegasShop.Infrastructure.MassTransit.Shops.Requests
{
    public class AddProductsByFactoryRequest
    {
        public int ShopId { get; set; }
        public List<int> ProductIds { get; set; }
    }
}
