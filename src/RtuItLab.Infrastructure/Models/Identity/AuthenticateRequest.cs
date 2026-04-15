using System.ComponentModel.DataAnnotations;

namespace VegasShop.Infrastructure.Models.Identity
{
    public class AuthenticateRequest
    {
        [Required]
        [MaxLength(30)]
        public string Username { get; set; }
        [Required]
        [MaxLength(64)]
        public string Password { get; set; }
    }
}
