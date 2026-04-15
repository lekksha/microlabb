using System.Collections.Generic;
using RtuItLab.Infrastructure.Models.Purchases;

namespace RtuItLab.Infrastructure.MassTransit.Purchases.Responses
{
    public class GetTransactionsResponse : BaseResponseMassTransit
    {
        public List<Transaction> Transactions { get; set; }
    }
}
