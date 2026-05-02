using System.Linq;
using System.Threading.Tasks;
using Identity.Domain.Services;
using MassTransit;
using Microsoft.Extensions.Logging;
using VegasShop.Infrastructure.Models.Identity;

namespace Identity.API.Consumers
{
    public class CreateUser : IdentityBaseConsumer<CreateUser>, IConsumer<RegisterRequest>
    {
        public CreateUser(IUserService userService,
            ILogger<CreateUser> logger) : base(userService, logger)
        {
        }

        public async Task Consume(ConsumeContext<RegisterRequest> context)
        {
            Logger.LogInformation($"Register: {context.Message.Username}");
            var result = await UserService.CreateUser(context.Message);
            // IdentityResult is a System type — cannot be sent over MassTransit directly.
            // Extract the data we need into our own RegisterResponse wrapper.
            await context.RespondAsync(new RegisterResponse
            {
                Succeeded = result.Succeeded,
                Errors    = result.Errors.Select(e => e.Description)
            });
        }
    }
}
