using System.Threading.Tasks;
using MassTransit;
using Purchases.Domain.Services;
using VegasShop.Infrastructure.MassTransit.Purchases.Requests;

namespace Purchases.API.Consumers
{
    public class GetTransactions : PurchasesBaseConsumer, IConsumer<GetTransactionsRequest>
    {
        public GetTransactions(IPurchasesService purchasesService) : base(purchasesService)
        {
        }

        public async Task Consume(ConsumeContext<GetTransactionsRequest> context)
        {
            var transactions = await PurchasesService.GetTransactions(context.Message.User);
            await context.RespondAsync(transactions);
        }
    }
}
