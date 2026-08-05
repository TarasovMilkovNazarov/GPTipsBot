using GPTipsBot.Db;
using GPTipsBot.Models;
using Microsoft.EntityFrameworkCore;

namespace GPTipsBot.Repositories;

public class InvoiceRepository(ApplicationContext context) : GenericRepository<Invoice>(context)
{
    private readonly ApplicationContext _context = context;

    public Invoice? GetById(long id) =>
        _context.Invoices.AsNoTracking().FirstOrDefault(i => i.Id == id);

    public Invoice? GetByExternalPaymentId(string externalPaymentId) =>
        _context.Invoices.AsNoTracking()
            .FirstOrDefault(i => i.ExternalPaymentId == externalPaymentId);

    public List<Invoice> GetPendingYooKassa(TimeSpan maxAge)
    {
        var since = DateTime.UtcNow - maxAge;
        return _context.Invoices.AsNoTracking()
            .Where(i =>
                i.Provider == PaymentProvider.YooKassa &&
                i.Status == InvoiceStatus.Created &&
                i.ExternalPaymentId != null &&
                i.CreatedAt >= since)
            .OrderBy(i => i.Id)
            .Take(50)
            .ToList();
    }
}
