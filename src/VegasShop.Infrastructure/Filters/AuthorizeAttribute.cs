using System;
using Microsoft.AspNetCore.Mvc.Filters;
using VegasShop.Infrastructure.Exceptions;
using VegasShop.Infrastructure.Models.Identity;

namespace VegasShop.Infrastructure.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (!(context.HttpContext.Items["User"] is User))
                throw new UnauthorizedException("User unauthorized!");
        }
    }
}
