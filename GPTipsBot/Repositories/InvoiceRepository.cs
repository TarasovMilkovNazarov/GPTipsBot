using GPTipsBot.Db;
using GPTipsBot.Models;

namespace GPTipsBot.Repositories;

public class InvoiceRepository(ApplicationContext context) : GenericRepository<Invoice>(context)
{
    private readonly ApplicationContext _context = context;
}