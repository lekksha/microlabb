using System.Collections.Generic;
using System.Threading.Tasks;
using MassTransit;
using RtuItLab.Infrastructure.MassTransit.Shops.Requests;
using RtuItLab.Infrastructure.Models.Shops;
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
            // GetAllShops() is synchronous — no await needed
            ICollection<Shop> shops = ShopsService.GetAllShops();
            await context.RespondAsync(shops);
        }
    }
}
