using System.Collections.Generic;

namespace VegasShop.Infrastructure.MassTransit.Shops.Responses
{
    public class GetProductsResponse
    {
        public bool IsSuccess { get; set; }
        public List<string> Errors { get; set; }
    }
}
