using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using VegasShop.Infrastructure.MassTransit.Shops.Requests;
using VegasShop.Infrastructure.MassTransit.Shops.Responses;
using VegasShop.Infrastructure.Models.Shops;
using Shops.Domain.Services;

namespace Shops.API.Consumers
{
    public class GetAllShops : ShopsBaseConsumer, IConsumer<GetAllShopsRequest>
    {
        public GetAllShops(IShopsService shopsService) : base(shopsService)
        {
        }

        public async Task Consume(ConsumeContext<GetAllShopsRequest> context)
        {
            ICollection<Shop> shops = ShopsService.GetAllShops();
            await context.RespondAsync(new GetAllShopsResponse
            {
                Success = true,
                Shops   = shops.ToList()
            });
        }
    }
}
