using Identity.Domain.Services;
using Microsoft.AspNetCore.Mvc;
using VegasShop.Infrastructure.Filters;
using VegasShop.Infrastructure.Models;
using VegasShop.Infrastructure.Models.Identity;
using System.Linq;
using System.Threading.Tasks;

namespace Identity.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IUserService _userService;

        public AccountController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] AuthenticateRequest model)
        {
            var result = await _userService.Authenticate(model);
            return Ok(ApiResult<AuthenticateResponse>.Success200(result));
        }

        /// <summary>
        /// Returns 200 on success, 400 if username is already taken or
        /// password does not meet complexity requirements.
        /// Previously always returned 200 even when IdentityResult.Succeeded == false.
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest model)
        {
            var identityResult = await _userService.CreateUser(model);
            if (!identityResult.Succeeded)
                return BadRequest(ApiResult<object>.Failure(400,
                    identityResult.Errors.Select(e => e.Description).ToList()));

            return Ok(ApiResult<RegisterResponse>.Success200(new RegisterResponse
            {
                Succeeded = true,
                Errors    = System.Array.Empty<string>()
            }));
        }

        [HttpGet("user")]
        [Authorize]
        public IActionResult GetUser()
        {
            var user = HttpContext.Items["User"] as User;
            return Ok(ApiResult<User>.Success200(user));
        }
    }
}
