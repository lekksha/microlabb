using MassTransit;
using Microsoft.AspNetCore.Mvc;
using RtuItLab.Infrastructure.Filters;
using RtuItLab.Infrastructure.MassTransit.Shops.Requests;
using RtuItLab.Infrastructure.MassTransit.Shops.Responses;
using RtuItLab.Infrastructure.Models;
using RtuItLab.Infrastructure.Models.Identity;
using RtuItLab.Infrastructure.Models.Shops;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shops.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ShopsController : ControllerBase
    {
        private readonly IRequestClient<GetAllShopsRequest>           _getAllShopsClient;
        private readonly IRequestClient<GetProductsRequest>           _getProductsClient;
        private readonly IRequestClient<GetProductsByCategoryRequest> _getCategoryClient;
        private readonly IRequestClient<BuyProductsRequest>           _buyProductsClient;

        public ShopsController(
            IRequestClient<GetAllShopsRequest>           getAllShopsClient,
            IRequestClient<GetProductsRequest>           getProductsClient,
            IRequestClient<GetProductsByCategoryRequest> getCategoryClient,
            IRequestClient<BuyProductsRequest>           buyProductsClient)
        {
            _getAllShopsClient  = getAllShopsClient;
            _getProductsClient = getProductsClient;
            _getCategoryClient = getCategoryClient;
            _buyProductsClient = buyProductsClient;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllShops()
        {
            var response = await _getAllShopsClient.GetResponse<GetAllShopsResponse>(
                new GetAllShopsRequest());
            return Ok(ApiResult<ICollection<Shop>>.Success200(response.Message.Shops));
        }

        [HttpGet("{shopId}")]
        public async Task<IActionResult> GetProducts(int shopId)
        {
            var response = await _getProductsClient.GetResponse<GetProductsResponse>(
                new GetProductsRequest { ShopId = shopId });
            return Ok(ApiResult<ICollection<Product>>.Success200(response.Message.Products));
        }

        [HttpPost("{shopId}/find_by_category")]
        public async Task<IActionResult> GetProductsByCategory(
            int shopId, [FromBody] Category category)
        {
            var response = await _getCategoryClient.GetResponse<GetProductsResponse>(
                new GetProductsByCategoryRequest
                {
                    ShopId       = shopId,
                    CategoryName = category?.CategoryName
                });
            return Ok(ApiResult<ICollection<Product>>.Success200(response.Message.Products));
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

            var response = await _buyProductsClient.GetResponse<GetProductsResponse>(
                new BuyProductsRequest
                {
                    User     = user,
                    ShopId   = shopId,
                    Products = products
                });

            if (!response.Message.Success)
                return BadRequest(ApiResult<object>.Failure(400,
                    new List<string> { "Order failed" }));

            return Ok(ApiResult<ICollection<Product>>.Success200(response.Message.Products));
        }
    }
}
