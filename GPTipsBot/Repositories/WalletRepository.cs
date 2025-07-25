using Ardalis.GuardClauses;
using GPTipsBot.Db;
using GPTipsBot.Models;
using Microsoft.Extensions.Logging;

namespace GPTipsBot.Repositories;

public class WalletRepository(ILogger<WalletRepository> logger, ApplicationContext context)
    : GenericRepository<Wallet>(context)
{
    private readonly ILogger<WalletRepository> _logger = logger;
    private readonly ApplicationContext _context = context;

    public async Task<bool> UpdateAmountAsync(Wallet wallet, long delta)
    {
        Guard.Against.Null(wallet);
        wallet.Balance += delta;
        Update(wallet);

        return true;
    }
}