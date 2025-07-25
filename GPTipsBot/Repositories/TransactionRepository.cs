using GPTipsBot.Db;
using GPTipsBot.Models;

namespace GPTipsBot.Repositories;

public class TransactionRepository(ApplicationContext context) : GenericRepository<Transaction>(context)
{
    private readonly ApplicationContext _context = context;
}