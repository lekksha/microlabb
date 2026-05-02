using System.Collections.Generic;
using VegasShop.Infrastructure.Models.Shops;

namespace VegasShop.Infrastructure.MassTransit.Shops.Responses
{
    public class GetAllShopsResponse
    {
        public bool Success { get; set; }
        public List<Shop> Shops { get; set; }
    }
}
