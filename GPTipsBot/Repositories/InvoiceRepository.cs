using GPTipsBot.Db;
using GPTipsBot.Models;

namespace GPTipsBot.Repositories;

public class InvoiceRepository: GenericRepository<Invoice>
{
    private readonly ApplicationContext _context;

    public InvoiceRepository(ApplicationContext context) : base(context)
    {
        _context = context;
    }
}