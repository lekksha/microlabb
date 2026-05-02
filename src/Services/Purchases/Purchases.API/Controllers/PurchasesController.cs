using Microsoft.AspNetCore.Mvc;
using Purchases.Domain.Services;
using VegasShop.Infrastructure.Exceptions;
using VegasShop.Infrastructure.Filters;
using VegasShop.Infrastructure.Models;
using VegasShop.Infrastructure.Models.Identity;
using VegasShop.Infrastructure.Models.Purchases;
using System.Threading.Tasks;

namespace Purchases.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PurchasesController : ControllerBase
    {
        private readonly IPurchasesService _purchasesService;

        public PurchasesController(IPurchasesService purchasesService)
        {
            _purchasesService = purchasesService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllHistory()
        {
            var user = HttpContext.Items["User"] as User;
            var result = await _purchasesService.GetTransactions(user);
            return Ok(ApiResult<System.Collections.Generic.ICollection<Transaction>>.Success200(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetHistory(int id)
        {
            var user = HttpContext.Items["User"] as User;
            var result = await _purchasesService.GetTransactionById(user, id);
            return Ok(ApiResult<Transaction>.Success200(result));
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddTransaction([FromBody] Transaction transaction)
        {
            if (transaction.IsShopCreate)
                throw new BadRequestException("You can't add shops' transaction");
            if (!ModelState.IsValid)
                throw new BadRequestException("Invalid request");
            var user = HttpContext.Items["User"] as User;
            await _purchasesService.AddTransaction(user, transaction);
            return Ok(ApiResult<int>.Success200(transaction.Id));
        }

        [HttpPut("update")]
        public async Task<IActionResult> UpdateTransaction([FromBody] UpdateTransaction updateTransaction)
        {
            if (!ModelState.IsValid) return BadRequest("Invalid request");
            var user = HttpContext.Items["User"] as User;
            await _purchasesService.UpdateTransaction(user, updateTransaction);
            return Ok(ApiResult<int>.Success200(updateTransaction.Id));
        }
    }
}
