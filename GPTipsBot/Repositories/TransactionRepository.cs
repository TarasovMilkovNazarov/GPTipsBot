using GPTipsBot.Db;
using GPTipsBot.Models;

namespace GPTipsBot.Repositories;

public class TransactionRepository
{
    private readonly ApplicationContext _context;

    public TransactionRepository(ApplicationContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Transaction transaction)
    {
        await _context.Transactions.AddAsync(transaction);
    }
}