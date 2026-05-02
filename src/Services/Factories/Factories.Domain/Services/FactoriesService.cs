using Factories.DAL.Data;
using Factories.Domain.Helpers;
using Microsoft.EntityFrameworkCore;
using VegasShop.Infrastructure.Models.Shops;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Factories.Domain.Services
{
    public class FactoriesService : IFactoriesService
    {
        private readonly FactoriesDbContext _context;

        public FactoriesService(FactoriesDbContext context)
        {
            _context = context;
        }

        public async Task<ICollection<ProductByFactory>> CreateRequestInShops()
            => await UpdateFactoriesStage();

        // FIX: ForEachAsync с мутацией заменён на ToListAsync + foreach.
        // ForEachAsync не подходит для изменения данных — нет гарантии порядка
        // и изменения могут потеряться при SaveChanges.
        private async Task<ICollection<ProductByFactory>> UpdateFactoriesStage()
        {
            var result = new List<ProductByFactory>();

            var factories = await _context.Factories
                .Include(item => item.Products)
                .ToListAsync();

            foreach (var factory in factories)
            {
                foreach (var product in factory.Products)
                {
                    product.Count += product.PartsOfCreate;
                    if (product.Count < 1) continue;
                    result.Add(product.ToProductByFactoryDto());
                    product.Count -= (int)product.Count;
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }
    }
}
