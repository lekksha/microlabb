using System.Collections.Generic;

namespace VegasShop.Infrastructure.MassTransit.Purchases.Responses
{
    public class GetTransactionsResponse
    {
        public bool IsSuccess { get; set; }
        public List<string> Errors { get; set; }
    }
}
