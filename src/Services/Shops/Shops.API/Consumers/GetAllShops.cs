using System.Threading.Tasks;
using MassTransit;
using RtuItLab.Infrastructure.MassTransit.Shops.Requests;
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
            // BUG FIX: was missing await — GetAllShops() returns Task<ICollection<Shop>>
            // Without await, RespondAsync received a Task object instead of the actual
            // collection, serialising the Task state machine rather than shop data.
            var shops = await ShopsService.GetAllShops();
            await context.RespondAsync(shops);
        }
    }
}
