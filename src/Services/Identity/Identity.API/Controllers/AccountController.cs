using Identity.Domain.Services;
using Microsoft.AspNetCore.Mvc;
using RtuItLab.Infrastructure.Filters;
using RtuItLab.Infrastructure.Models;
using RtuItLab.Infrastructure.Models.Identity;
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

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest model)
        {
            var identityResult = await _userService.CreateUser(model);
            var response = new RegisterResponse
            {
                Succeeded = identityResult.Succeeded,
                Errors    = identityResult.Errors.Select(e => e.Description)
            };
            return Ok(ApiResult<RegisterResponse>.Success200(response));
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
