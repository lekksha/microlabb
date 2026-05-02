using System.Threading.Tasks;
using MassTransit;
using Purchases.Domain.Services;
using VegasShop.Infrastructure.MassTransit.Purchases.Requests;

namespace Purchases.API.Consumers
{
    public class UpdateTransaction : PurchasesBaseConsumer, IConsumer<UpdateTransactionRequest>
    {
        public UpdateTransaction(IPurchasesService purchasesService) : base(purchasesService)
        {
        }
        public async Task Consume(ConsumeContext<UpdateTransactionRequest> context)
        {
           var order = await PurchasesService.UpdateTransaction(context.Message.User, context.Message.Transaction);
           await context.RespondAsync(order);
        }
    }
}
