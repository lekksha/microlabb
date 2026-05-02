using MassTransit;
using Purchases.Domain.Services;
using VegasShop.Infrastructure.MassTransit.Purchases.Requests;
using System.Threading.Tasks;

namespace Purchases.API.Consumers
{
    public class AddTransaction : PurchasesBaseConsumer, IConsumer<AddTransactionRequest>
    {
        public AddTransaction(IPurchasesService purchasesService) : base(purchasesService)
        {
        }

        public async Task Consume(ConsumeContext<AddTransactionRequest> context)
        {
            // Shop sends transaction via MassTransit — must be marked IsShopCreate=true
            await PurchasesService.AddShopTransaction(context.Message.User, context.Message.Transaction);
        }
    }
}
