using GPTipsBot.Db;
using GPTipsBot.Models;

namespace GPTipsBot.Repositories;

public class TransactionRepository: GenericRepository<Transaction>
{
    private readonly ApplicationContext _context;

    public TransactionRepository(ApplicationContext context):base(context)
    {
        _context = context;
    }
}