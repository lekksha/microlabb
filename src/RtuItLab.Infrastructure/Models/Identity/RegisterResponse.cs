using System.Collections.Generic;

namespace VegasShop.Infrastructure.Models.Identity
{
    public class RegisterResponse
    {
        public bool Succeeded { get; set; }
        public IEnumerable<string> Errors { get; set; }
    }
}
