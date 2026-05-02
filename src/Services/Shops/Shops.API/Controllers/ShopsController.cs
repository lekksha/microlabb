using MassTransit;
using Microsoft.AspNetCore.Mvc;
using VegasShop.Infrastructure.Filters;
using VegasShop.Infrastructure.MassTransit.Purchases.Requests;
using VegasShop.Infrastructure.Models;
using VegasShop.Infrastructure.Models.Identity;
using VegasShop.Infrastructure.Models.Shops;
using Shops.Domain.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shops.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ShopsController : ControllerBase
    {
        private readonly IShopsService _shopsService;
        private readonly IBus          _bus;
        private readonly Uri           _purchasesQueue = new Uri("rabbitmq://rabbit/purchasesQueue");

        public ShopsController(IShopsService shopsService, IBus bus)
        {
            _shopsService = shopsService;
            _bus          = bus;
        }

        [HttpGet]
        public IActionResult GetAllShops()
        {
            var shops = _shopsService.GetAllShops();
            return Ok(ApiResult<ICollection<Shop>>.Success200(shops));
        }

        [HttpGet("{shopId}")]
        public async Task<IActionResult> GetProducts(int shopId)
        {
            var products = await _shopsService.GetProductsByShop(shopId);
            return Ok(ApiResult<ICollection<Product>>.Success200(products));
        }

        [HttpPost("{shopId}/find_by_category")]
        public async Task<IActionResult> GetProductsByCategory(
            int shopId, [FromBody] Category category)
        {
            var products = await _shopsService.GetProductsByCategory(
                shopId, category?.CategoryName);
            return Ok(ApiResult<ICollection<Product>>.Success200(products));
        }

        [Authorize]
        [HttpPost("{shopId}/order")]
        public async Task<IActionResult> BuyProducts(
            int shopId, [FromBody] ICollection<Product> products)
        {
            var user = HttpContext.Items["User"] as User;
            if (user == null)
                return Unauthorized(ApiResult<object>.Failure(401,
                    new List<string> { "Unauthorized" }));

            var order       = await _shopsService.BuyProducts(shopId, products);
            var transaction = await _shopsService.CreateTransaction(shopId, order);
            await _shopsService.AddReceipt(transaction.Receipt);

            // Publish to Purchases service via RabbitMQ
            var endpoint = await _bus.GetSendEndpoint(_purchasesQueue);
            await endpoint.Send(new AddTransactionRequest
            {
                User        = user,
                Transaction = transaction
            });

            return Ok(ApiResult<ICollection<Product>>.Success200(
                new List<Product>(order)));
        }
    }
}
