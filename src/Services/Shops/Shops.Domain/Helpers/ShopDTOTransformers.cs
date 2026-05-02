using System.Linq;
using VegasShop.Infrastructure.Models.Shops;
using Shops.DAL.ContextModels;

namespace Shops.Domain.Helpers
{
    public static class ShopDtoTransformers
    {
        public static Shop ToShopDto(this ShopContext model)
        {
            if (model is null)
                return null;
            return new Shop
            {
                Id          = model.Id,
                Name        = model.Name,
                Address     = model.Address,
                PhoneNumber = model.PhoneNumber,
                Products    = model.Products?.Select(ToProductDto).ToList()
            };
        }

        public static Product ToProductDto(this ProductContext model)
        {
            if (model is null)
                return null;
            return new Product
            {
                Cost      = model.Cost,
                ProductId = model.Id,
                Count     = model.Count,
                Name      = model.Name,
                Category  = model.Category
            };
        }
    }
}
