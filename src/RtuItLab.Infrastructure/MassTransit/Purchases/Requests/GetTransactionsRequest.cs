using RtuItLab.Infrastructure.Models.Identity;

namespace RtuItLab.Infrastructure.MassTransit.Purchases.Requests
{
    public class GetTransactionsRequest
    {
        public User User { get; set; }
        public int Page { get; set; }
        public int Count { get; set; }
        public bool IsShopCreate { get; set; }
    }
}
