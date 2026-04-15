using System.Collections.Generic;

namespace VegasShop.Infrastructure.MassTransit.Purchases.Requests
{
    public class AddTransactionRequest
    {
        public string UserId { get; set; }
        public bool IsShopCreate { get; set; }
        public List<int> ProductIds { get; set; }
        public int ShopId { get; set; }
    }
}
