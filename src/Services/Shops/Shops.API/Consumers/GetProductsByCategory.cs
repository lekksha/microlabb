using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using VegasShop.Infrastructure.MassTransit.Shops.Requests;
using VegasShop.Infrastructure.MassTransit.Shops.Responses;
using Shops.Domain.Services;

namespace Shops.API.Consumers
{
    public class GetProductsByCategory : ShopsBaseConsumer, IConsumer<GetProductsByCategoryRequest>
    {
        public GetProductsByCategory(IShopsService shopsService) : base(shopsService)
        {
        }

        public async Task Consume(ConsumeContext<GetProductsByCategoryRequest> context)
        {
            var products = await ShopsService.GetProductsByCategory(
                context.Message.ShopId,
                context.Message.Category);
            await context.RespondAsync(new GetProductsResponse
            {
                Success  = true,
                Products = products.ToList()
            });
        }
    }
}
