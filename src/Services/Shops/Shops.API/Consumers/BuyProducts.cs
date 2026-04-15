using System;
using System.Threading.Tasks;
using MassTransit;
using RtuItLab.Infrastructure.MassTransit.Purchases.Requests;
using RtuItLab.Infrastructure.MassTransit.Shops.Requests;
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
            // 1. Сначала выполняем всю бизнес-логику...
            var order = await ShopsService.BuyProducts(context.Message.ShopId, context.Message.Products);
            var transaction = await ShopsService.CreateTransaction(context.Message.ShopId, order);
            await ShopsService.AddReceipt(transaction.Receipt);

            // 2. ...then send to Purchases (fire-and-forget: AddProductsByFactory
            //    consumer has no RespondAsync, consistent with that pattern)
            var endpoint = await _bus.GetSendEndpoint(_rabbitMqUrl);
            await endpoint.Send(new AddTransactionRequest
            {
                User        = context.Message.User,
                Transaction = transaction
            });

            // 3. Отвечаем клиенту только после успешного сохранения всех данных
            await context.RespondAsync(order);
        }
    }
}
