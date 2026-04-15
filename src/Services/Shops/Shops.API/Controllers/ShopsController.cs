using MassTransit;
using Microsoft.AspNetCore.Mvc;
using RtuItLab.Infrastructure.Filters;
using RtuItLab.Infrastructure.MassTransit.Shops.Requests;
using RtuItLab.Infrastructure.Models;
using RtuItLab.Infrastructure.Models.Identity;
using RtuItLab.Infrastructure.Models.Shops;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shops.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ShopsController : ControllerBase
    {
        private readonly IBus _bus;
        private readonly Uri _rabbitMqUrl = new Uri("rabbitmq://rabbit/shopsQueue");

        // BUG FIX: was IBusControl. IBus is the correct interface for sending
        // messages from application code; IBusControl is for lifecycle management.
        public ShopsController(IBus bus)
        {
            _bus = bus;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllShops()
        {
            var response = await GetResponseRabbitTask<GetAllShopsRequest, ICollection<Shop>>(
                new GetAllShopsRequest());
            return Ok(ApiResult<ICollection<Shop>>.Success200(response));
        }

        [HttpGet("{shopId}")]
        public async Task<IActionResult> GetProducts(int shopId)
        {
            var response = await GetResponseRabbitTask<GetProductsRequest, ICollection<Product>>(
                new GetProductsRequest { ShopId = shopId });
            return Ok(ApiResult<ICollection<Product>>.Success200(response));
        }

        [HttpPost("{shopId}/find_by_category")]
        public async Task<IActionResult> GetProductsByCategory(int shopId, [FromBody] Category category)
        {
            if (!ModelState.IsValid) return BadRequest();
            var response = await GetResponseRabbitTask<GetProductsByCategoryRequest, ICollection<Product>>(
                new GetProductsByCategoryRequest
                {
                    ShopId   = shopId,
                    Category = category.CategoryName
                });
            return Ok(ApiResult<ICollection<Product>>.Success200(response));
        }

        [Authorize]
        [HttpPost("{shopId}/order")]
        public async Task<IActionResult> BuyProducts(int shopId, [FromBody] ICollection<Product> products)
        {
            if (!ModelState.IsValid) return BadRequest();

            // BUG FIX: JwtMiddleware sets User only when token is valid.
            // Without this check a missing/invalid token causes NullReferenceException
            // inside the BuyProducts consumer instead of returning 401.
            var user = HttpContext.Items["User"] as User;
            if (user == null)
                return Unauthorized(ApiResult<object>.Failure(401,
                    new System.Collections.Generic.List<string> { "Unauthorized" }));

            var productsResponse = await GetResponseRabbitTask<BuyProductsRequest, ICollection<Product>>(
                new BuyProductsRequest
                {
                    User     = user,
                    ShopId   = shopId,
                    Products = products
                });
            return Ok(ApiResult<ICollection<Product>>.Success200(productsResponse));
        }

        private async Task<TOut> GetResponseRabbitTask<TIn, TOut>(TIn request)
            where TIn : class
            where TOut : class
        {
            var client   = _bus.CreateRequestClient<TIn>(_rabbitMqUrl);
            var response = await client.GetResponse<TOut>(request);
            return response.Message;
        }
    }
}
