using System.Collections.Generic;

namespace VegasShop.Infrastructure.Models.Shops
{
    public class Shop
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<Product> Products { get; set; }
    }
}
