namespace VegasShop.Infrastructure.Exceptions
{
    public class UnauthorizedException : System.Exception
    {
        public UnauthorizedException(string message) : base(message) { }
    }
}
