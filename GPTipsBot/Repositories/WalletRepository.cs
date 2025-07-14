using Ardalis.GuardClauses;
using GPTipsBot.Db;
using GPTipsBot.Models;
using Microsoft.Extensions.Logging;

namespace GPTipsBot.Repositories;

public class WalletRepository: GenericRepository<Wallet>
{
    private readonly ILogger<WalletRepository> _logger;
    private readonly ApplicationContext _context;

    public WalletRepository(ILogger<WalletRepository> logger, ApplicationContext context): base(context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<bool> UpdateAmountAsync(long id, long delta)
    {
        var wallet = await _context.Wallets.FindAsync(id);
        Guard.Against.Null(wallet);

        wallet.Amount += delta;
        _context.Wallets.Update(wallet);
        await _context.SaveChangesAsync();

        return true;
    }
}