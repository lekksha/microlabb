using System.Collections.Generic;
using RtuItLab.Infrastructure.Models.Shops;

namespace RtuItLab.Infrastructure.MassTransit.Shops.Responses
{
    public class GetProductsResponse : BaseResponseMassTransit
    {
        public List<Product> Products { get; set; }
    }
}
