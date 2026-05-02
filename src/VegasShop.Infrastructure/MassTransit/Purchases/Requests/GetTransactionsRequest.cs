using VegasShop.Infrastructure.Models.Identity;

namespace VegasShop.Infrastructure.MassTransit.Purchases.Requests
{
    /// <summary>
    /// Request-контракт для получения списка транзакций пользователя.
    /// Заменяет ошибочный IConsumer&lt;User&gt; — доменная модель не должна
    /// использоваться как тип сообщения MassTransit.
    /// </summary>
    public class GetTransactionsRequest
    {
        public User User { get; set; }
    }
}
