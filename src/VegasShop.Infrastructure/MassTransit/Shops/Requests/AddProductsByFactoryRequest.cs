using System.Collections.Generic;
using VegasShop.Infrastructure.Models.Shops;

namespace VegasShop.Infrastructure.MassTransit.Shops.Requests
{
    public class AddProductsByFactoryRequest
    {
        public List<ProductByFactory> Products { get; set; }
    }
}
