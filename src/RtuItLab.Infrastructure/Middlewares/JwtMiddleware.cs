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
    /// Previously this middleware called identityQueue via RabbitMQ on every request,
    /// which caused a cross-service timeout on Shops and Purchases:
    ///   - Those services have no identityQueue consumer
    ///   - The request timed out silently after 30 s
    ///   - User was never set → [Authorize] threw 403 on every authenticated call
    ///
    /// Local validation is correct: the JWT is self-contained and signed with a
    /// shared secret that all services already receive via their Secret env var.
    /// </summary>
    public class JwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _secret;

        public JwtMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next   = next;
            // All services receive Secret via environment variable (docker-compose).
            // IConfiguration resolves it automatically from env vars.
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

                var jwt    = (JwtSecurityToken)validatedToken;
                var userId = jwt.Claims.First(c => c.Type == "id").Value;

                // Minimal User — only Id is encoded in the token.
                // Username is not included to keep tokens small.
                context.Items["User"] = new User { Id = userId };
            }
            catch
            {
                // Invalid / expired token — leave User unset.
                // [Authorize] will reject the request with 401.
            }
        }
    }
}
