using System.Collections.Generic;
using VegasShop.Infrastructure.Models.Purchases;

namespace VegasShop.Infrastructure.MassTransit.Purchases.Responses
{
    public class GetTransactionsResponse
    {
        public List<Transaction> Transactions { get; set; }
        public int Count { get; set; }
    }
}
