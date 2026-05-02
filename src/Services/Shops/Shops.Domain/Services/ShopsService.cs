using Microsoft.EntityFrameworkCore;
using VegasShop.Infrastructure.Exceptions;
using VegasShop.Infrastructure.MassTransit;
using VegasShop.Infrastructure.Models.Purchases;
using VegasShop.Infrastructure.Models.Shops;
using Shops.DAL.Data;
using Shops.Domain.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shops.DAL.ContextModels;

namespace Shops.Domain.Services
{
    public class ShopsService : IShopsService
    {
        private const int MaxProductRequestCount = 10;
        private readonly ShopsDbContext _context;

        public ShopsService(ShopsDbContext context)
        {
            _context = context;
        }

        public ICollection<Shop> GetAllShops()
        {
            return _context.Shops
                .AsNoTracking()
                .Select(item => item.ToShopDto())
                .ToList();
        }

        public async Task<ICollection<Product>> GetProductsByShop(int shopId)
        {
            var shop = await _context.Shops
                .Include(item => item.Products)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == shopId);

            if (shop is null)
                throw new NotFoundException("Shop not found");

            return shop.Products
                .Select(item => item.ToProductDto())
                .ToList();
        }

        public async Task<ICollection<Product>> GetProductsByCategory(int shopId, string categoryName)
        {
            var shop = await _context.Shops
                .Include(item => item.Products)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == shopId);

            if (shop is null)
                throw new NotFoundException("Shop not found");

            return shop.Products
                .Where(item => item.Category == categoryName)
                .Select(item => item.ToProductDto())
                .ToList();
        }

        public async Task<ICollection<Product>> BuyProducts(int shopId, ICollection<Product> products)
        {
            if (products == null || products.Count < 1)
                throw new BadRequestException($"Please, select products, max count is {MaxProductRequestCount}");

            if (products.Count > MaxProductRequestCount)
                throw new BadRequestException($"Too many products, max count is {MaxProductRequestCount}");

            // Загружаем БЕЗ AsNoTracking — нужно обновить Count в БД
            var shop = await _context.Shops
                .Include(item => item.Products)
                .FirstOrDefaultAsync(item => item.Id == shopId);

            if (shop is null)
                throw new BadRequestException("Shop not found");

            var resultProducts = new List<Product>();

            foreach (var product in products)
            {
                if (product.ProductId <= 0)
                    throw new BadRequestException("ProductId must be greater than 0");

                if (product.Count <= 0)
                    throw new BadRequestException("Product count must be greater than 0");

                var item = shop.Products.FirstOrDefault(p => p.Id == product.ProductId);
                if (item is null || item.Count < product.Count)
                    throw new BadRequestException(
                        $"ProductId {product.ProductId} is either not found, or there not enough of it in the store");

                item.Count -= product.Count;
                var addProduct = item.ToProductDto();
                addProduct.Count = product.Count;
                resultProducts.Add(addProduct);
            }

            await _context.SaveChangesAsync();
            return resultProducts;
        }

        public Task<Transaction> CreateTransaction(int shopId, ICollection<Product> products)
        {
            var response = new Transaction
            {
                Products     = products.ToList(),
                Date         = DateTime.UtcNow,
                IsShopCreate = true,
                Receipt      = new Receipt
                {
                    ShopId   = shopId,
                    Cost     = products.Sum(item => item.Cost * item.Count),
                    Count    = products.Sum(item => item.Count),
                    Date     = DateTime.UtcNow,
                    Products = products.ToList()
                }
            };
            return Task.FromResult(response);
        }

        // FIX: был ForEach(async item => ...) — async void антипаттерн.
        // SaveChangesAsync вызывался до завершения всех AddProductsInShop.
        // Исправлено на foreach + await.
        public async Task AddProductsByFactory(ICollection<ProductByFactory> products)
        {
            var shopsCollection = products.GroupBy(item => item.ShopId);
            foreach (var group in shopsCollection)
            {
                await AddProductsInShop(group.Key, group.ToList());
            }
            await _context.SaveChangesAsync();
        }

        public async Task AddReceipt(Receipt receipt)
        {
            // FIX: FK в ReceiptContext называется ShopContextKey (не ShopId).
            // Ранее ShopContextKey не заполнялся — EF Core нарушал
            // NOT NULL / FK constraint и кидал исключение при SaveChangesAsync.
            // ToProductByReceiptContext() не копирует Id — IDENTITY назначает БД.
            var receiptContext = new ReceiptContext
            {
                ShopContextKey = receipt.ShopId,
                FullCost       = (decimal)receipt.Products.Sum(item => item.Cost * item.Count),
                Count          = receipt.Products.Sum(item => item.Count),
                Products       = receipt.Products
                                     .Select(item => item.ToProductByReceiptContext())
                                     .ToList()
            };

            await _context.Receipts.AddAsync(receiptContext);
            await _context.SaveChangesAsync();
        }

        private async Task AddProductsInShop(int shopId, List<ProductByFactory> products)
        {
            var shop = await _context.Shops
                .Include(item => item.Products)
                .FirstOrDefaultAsync(shopContext => shopContext.Id == shopId);

            if (shop is null) return;

            foreach (var product in products)
            {
                var productContext = shop.Products.FirstOrDefault(item => item.Id == product.ProductId);
                if (productContext != null)
                    productContext.Count += product.Count;
            }
        }
    }
}
