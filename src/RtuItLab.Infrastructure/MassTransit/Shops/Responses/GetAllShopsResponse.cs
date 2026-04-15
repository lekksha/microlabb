using System.Collections.Generic;
using RtuItLab.Infrastructure.Models.Shops;

namespace RtuItLab.Infrastructure.MassTransit.Shops.Responses
{
    public class GetAllShopsResponse : BaseResponseMassTransit
    {
        public List<Shop> Shops { get; set; }
    }
}
