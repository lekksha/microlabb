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
        private readonly IRequestClient<GetAllShopsRequest>        _getAllShopsClient;
        private readonly IRequestClient<GetProductsRequest>        _getProductsClient;
        private readonly IRequestClient<GetProductsByCategoryRequest> _getByCategoryClient;
        private readonly IRequestClient<BuyProductsRequest>        _buyProductsClient;

        public ShopsController(
            IRequestClient<GetAllShopsRequest>           getAllShopsClient,
            IRequestClient<GetProductsRequest>           getProductsClient,
            IRequestClient<GetProductsByCategoryRequest> getByCategoryClient,
            IRequestClient<BuyProductsRequest>           buyProductsClient)
        {
            _getAllShopsClient   = getAllShopsClient;
            _getProductsClient  = getProductsClient;
            _getByCategoryClient = getByCategoryClient;
            _buyProductsClient  = buyProductsClient;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllShops()
        {
            var response = await _getAllShopsClient.GetResponse<GetAllShopsResponse>(
                new GetAllShopsRequest());
            return Ok(ApiResult<List<Shop>>.Success200(response.Message.Shops));
        }

        [HttpGet("{shopId}")]
        public async Task<IActionResult> GetProducts(int shopId)
        {
            var response = await _getProductsClient.GetResponse<GetProductsResponse>(
                new GetProductsRequest { ShopId = shopId });
            return Ok(ApiResult<List<Product>>.Success200(response.Message.Products));
        }

        [HttpPost("{shopId}/find_by_category")]
        public async Task<IActionResult> GetProductsByCategory(int shopId, [FromBody] Category category)
        {
            if (!ModelState.IsValid) return BadRequest();
            var response = await _getByCategoryClient.GetResponse<GetProductsResponse>(
                new GetProductsByCategoryRequest
                {
                    ShopId   = shopId,
                    Category = category.CategoryName
                });
            return Ok(ApiResult<List<Product>>.Success200(response.Message.Products));
        }

        [Authorize]
        [HttpPost("{shopId}/order")]
        public async Task<IActionResult> BuyProducts(int shopId, [FromBody] ICollection<Product> products)
        {
            if (!ModelState.IsValid) return BadRequest();

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
            return Ok(ApiResult<List<Product>>.Success200(response.Message.Products));
        }
    }
}
