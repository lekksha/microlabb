using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using VegasShop.Infrastructure.MassTransit.Shops.Requests;
using VegasShop.Infrastructure.MassTransit.Shops.Responses;
using Shops.Domain.Services;

namespace Shops.API.Consumers
{
    public class GetProductsByShop : ShopsBaseConsumer, IConsumer<GetProductsRequest>
    {
        public GetProductsByShop(IShopsService shopsService) : base(shopsService)
        {
        }

        public async Task Consume(ConsumeContext<GetProductsRequest> context)
        {
            var products = await ShopsService.GetProductsByShop(context.Message.ShopId);
            await context.RespondAsync(new GetProductsResponse
            {
                Success  = true,
                Products = products.ToList()
            });
        }
    }
}
