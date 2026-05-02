using System.Collections.Generic;
using VegasShop.Infrastructure.Models.Shops;

namespace VegasShop.Infrastructure.MassTransit.Shops.Responses
{
    public class GetProductsResponse
    {
        public bool Success { get; set; }
        public List<Product> Products { get; set; }
    }
}
