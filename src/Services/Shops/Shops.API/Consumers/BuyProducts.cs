using System;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using VegasShop.Infrastructure.MassTransit.Purchases.Requests;
using VegasShop.Infrastructure.MassTransit.Shops.Requests;
using VegasShop.Infrastructure.MassTransit.Shops.Responses;
using Shops.Domain.Services;

namespace Shops.API.Consumers
{
    public class BuyProducts : ShopsBaseConsumer, IConsumer<BuyProductsRequest>
    {
        private readonly IBus _bus;
        private readonly Uri _rabbitMqUrl = new Uri("rabbitmq://rabbit/purchasesQueue");

        public BuyProducts(IShopsService shopsService, IBus bus) : base(shopsService)
        {
            _bus = bus;
        }

        public async Task Consume(ConsumeContext<BuyProductsRequest> context)
        {
            var order       = await ShopsService.BuyProducts(context.Message.ShopId, context.Message.Products);
            var transaction = await ShopsService.CreateTransaction(context.Message.ShopId, order);
            await ShopsService.AddReceipt(transaction.Receipt);

            var endpoint = await _bus.GetSendEndpoint(_rabbitMqUrl);
            await endpoint.Send(new AddTransactionRequest
            {
                User        = context.Message.User,
                Transaction = transaction
            });

            await context.RespondAsync(new GetProductsResponse
            {
                Success  = true,
                Products = order.ToList()
            });
        }
    }
}
