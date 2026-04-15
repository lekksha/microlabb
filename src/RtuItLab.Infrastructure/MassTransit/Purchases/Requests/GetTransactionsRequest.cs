using RtuItLab.Infrastructure.Models.Identity;

namespace RtuItLab.Infrastructure.MassTransit.Purchases.Requests
{
    public class GetTransactionsRequest
    {
        public User User { get; set; }
    }
}
