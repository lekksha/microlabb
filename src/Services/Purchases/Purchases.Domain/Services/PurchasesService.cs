using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Purchases.DAL.ContextModels;
using Purchases.DAL.Data;
using Purchases.Domain.Helpers;
using VegasShop.Infrastructure.Exceptions;
using VegasShop.Infrastructure.MassTransit;
using VegasShop.Infrastructure.Models.Identity;
using VegasShop.Infrastructure.Models.Purchases;

namespace Purchases.Domain.Services
{
    public class PurchasesService : IPurchasesService
    {
        private readonly PurchasesDbContext _context;

        public PurchasesService(PurchasesDbContext context)
        {
            _context = context;
        }

        public async Task<Transaction> GetTransactionById(User user, int id)
        {
            await EnsureCustomerExists(user);
            var customer = await _context.Customers
                .Include(item => item.Transactions).ThenInclude(item => item.Products)
                .Include(item => item.Transactions).ThenInclude(item => item.Receipt)
                .FirstOrDefaultAsync(item => item.CustomerId == user.Id);

            var transaction = customer?.Transactions.FirstOrDefault(item => item.Id == id);
            if (transaction is null)
                throw new NotFoundException("Transaction not found!");

            return transaction.ToTransactionDto();
        }

        public async Task<BaseResponseMassTransit> AddTransaction(User user, Transaction transaction)
        {
            await EnsureCustomerExists(user);
            var customer = await _context.Customers
                .Include(c => c.Transactions)
                .FirstOrDefaultAsync(item => item.CustomerId == user.Id);

            var ctx = transaction.ToTransactionContext();
            ctx.IsShopCreate = false;
            customer.Transactions.Add(ctx);
            await _context.SaveChangesAsync();

            return new BaseResponseMassTransit();
        }

        public async Task<BaseResponseMassTransit> AddShopTransaction(User user, Transaction transaction)
        {
            await EnsureCustomerExists(user);
            var customer = await _context.Customers
                .Include(c => c.Transactions)
                .FirstOrDefaultAsync(item => item.CustomerId == user.Id);

            var ctx = transaction.ToTransactionContext();
            ctx.IsShopCreate = true;
            customer.Transactions.Add(ctx);
            await _context.SaveChangesAsync();

            return new BaseResponseMassTransit();
        }

        public async Task<BaseResponseMassTransit> UpdateTransaction(User user, UpdateTransaction transaction)
        {
            await EnsureCustomerExists(user);

            var customer = await _context.Customers
                .Include(item => item.Transactions).ThenInclude(item => item.Products)
                .FirstOrDefaultAsync(item => item.CustomerId == user.Id);

            var currentTransaction = customer?.Transactions.FirstOrDefault(item => item.Id == transaction.Id);
            if (currentTransaction is null)
                throw new NotFoundException("Transaction that is being updated was not found");

            if (currentTransaction.IsShopCreate)
                throw new BadRequestException("You can't update a shop-created transaction!");

            await UpdateUserTransaction(currentTransaction, transaction);

            return new BaseResponseMassTransit();
        }

        public async Task<ICollection<Transaction>> GetTransactions(User user)
        {
            await EnsureCustomerExists(user);
            var customer = await _context.Customers
                .Include(item => item.Transactions).ThenInclude(t => t.Products)
                .Include(item => item.Transactions).ThenInclude(t => t.Receipt)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.CustomerId == user.Id);

            // Manual transactions first (IsShopCreate=false), then shop ones, ordered by id
            return customer.Transactions
                .OrderBy(t => t.IsShopCreate)
                .ThenBy(t => t.Id)
                .Select(item => item.ToTransactionDto())
                .ToList();
        }

        private async Task EnsureCustomerExists(User user)
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(item => item.CustomerId == user.Id);
            if (customer is null)
            {
                await _context.Customers.AddAsync(new CustomerContext { CustomerId = user.Id });
                await _context.SaveChangesAsync();
            }
        }

        private async Task UpdateUserTransaction(TransactionContext transactionContext,
            UpdateTransaction updateTransaction)
        {
            transactionContext.TransactionType = updateTransaction.TransactionType;

            if (updateTransaction.Products != null)
                transactionContext.Products =
                    updateTransaction.Products.Select(item => item.ToProductContext()).ToList();

            if (updateTransaction.Date != new DateTime())
                transactionContext.Date = updateTransaction.Date;

            await _context.SaveChangesAsync();
        }
    }
}
