using GPTipsBot.Db;
using GPTipsBot.Models;
using Microsoft.EntityFrameworkCore;

namespace GPTipsBot.Repositories;

public class InvoiceRepository(ApplicationContext context) : GenericRepository<Invoice>(context)
{
    private readonly ApplicationContext _context = context;

    public Invoice? GetById(long id) =>
        _context.Invoices.AsNoTracking().FirstOrDefault(i => i.Id == id);
}
