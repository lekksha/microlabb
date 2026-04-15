using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using RtuItLab.Infrastructure.Models.Identity;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RtuItLab.Infrastructure.Middlewares
{
    /// <summary>
    /// Validates the Bearer JWT locally using the shared Secret from configuration.
    /// Extracts both 'id' and 'username' claims and attaches them to HttpContext.Items["User"].
    /// </summary>
    public class JwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _secret;

        public JwtMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next   = next;
            _secret = configuration["Secret"]
                ?? throw new InvalidOperationException(
                    "JWT Secret is not configured. Set the 'Secret' environment variable.");
        }

        public async Task Invoke(HttpContext context)
        {
            var token = context.Request.Headers["Authorization"]
                .FirstOrDefault()?.Split(" ").Last();

            if (token != null)
                AttachUserToContext(context, token);

            await _next(context);
        }

        private void AttachUserToContext(HttpContext context, string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key          = Encoding.ASCII.GetBytes(_secret);

                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = new SymmetricSecurityKey(key),
                    ValidateIssuer           = false,
                    ValidateAudience         = false,
                    ClockSkew                = TimeSpan.Zero
                }, out var validatedToken);

                var jwt      = (JwtSecurityToken)validatedToken;
                var userId   = jwt.Claims.First(c => c.Type == "id").Value;
                var username = jwt.Claims.FirstOrDefault(c => c.Type == "username")?.Value;

                context.Items["User"] = new User { Id = userId, Username = username };
            }
            catch
            {
                // Invalid / expired token — leave User unset.
                // [Authorize] will reject the request with 401.
            }
        }
    }
}
