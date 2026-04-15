using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using VegasShop.Infrastructure.Exceptions;
using VegasShop.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace VegasShop.Infrastructure.Middlewares
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next   = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleException(context, ex);
            }
        }

        private Task HandleException(HttpContext context, Exception ex)
        {
            _logger.LogError(ex, ex.Message);

            HttpStatusCode code;
            List<string> errors;

            switch (ex)
            {
                case RequestFaultException faultEx:
                    code = HttpStatusCode.InternalServerError;
                    var faultMessage = faultEx.Fault?.Exceptions
                        ?.FirstOrDefault()?.Message ?? faultEx.Message;
                    errors = new List<string> { $"Service error: {faultMessage}" };
                    break;

                case OperationCanceledException _:
                case TimeoutException _:
                    code   = HttpStatusCode.GatewayTimeout;
                    errors = new List<string>
                        { "Service timeout: downstream service did not respond in time." };
                    break;

                case NotFoundException _:
                    code   = HttpStatusCode.NotFound;
                    errors = new List<string> { ex.Message };
                    break;

                case BadRequestException _:
                    code   = HttpStatusCode.BadRequest;
                    errors = new List<string> { ex.Message };
                    break;

                case UnauthorizedException _:
                    code   = HttpStatusCode.Unauthorized;
                    errors = new List<string> { ex.Message };
                    break;

                case ForbiddenException _:
                    code   = HttpStatusCode.Forbidden;
                    errors = new List<string> { ex.Message };
                    break;

                default:
                    code   = HttpStatusCode.InternalServerError;
                    errors = new List<string> { ex.Message };
                    break;
            }

            var result = JsonSerializer.Serialize(
                ApiResult<string>.Failure((int)code, errors),
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

            context.Response.ContentType = "application/json";
            context.Response.StatusCode  = (int)code;

            return context.Response.WriteAsync(result);
        }
    }
}
