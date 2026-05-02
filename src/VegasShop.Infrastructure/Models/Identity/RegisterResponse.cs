using System.Collections.Generic;

namespace VegasShop.Infrastructure.Models.Identity
{
    /// <summary>
    /// MassTransit response wrapper for user registration.
    /// IdentityResult is a BCL/ASP.NET type and cannot be used directly
    /// as a MassTransit message payload.
    /// </summary>
    public class RegisterResponse
    {
        public bool Succeeded { get; set; }
        public IEnumerable<string> Errors { get; set; }
    }
}
