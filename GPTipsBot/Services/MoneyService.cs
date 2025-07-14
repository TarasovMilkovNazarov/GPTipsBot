using System.Data;
using Ardalis.GuardClauses;
using GPTipsBot.Db;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GPTipsBot.Services;

public class MoneyService
{
    private readonly WalletRepository _walletRepository;
    private readonly UserRepository _userRepository;
    private readonly TransactionRepository _transactionRepository;
    private readonly ApplicationContext _context;

    public MoneyService(WalletRepository walletRepository, UserRepository userRepository,
        TransactionRepository transactionRepository, ApplicationContext context)
    {
        _walletRepository = walletRepository;
        _userRepository = userRepository;
        _transactionRepository = transactionRepository;
        _context = context;
    }

    public async Task AddMoneyAsync(long userId, int amount, string currency,  CancellationToken cancellationToken)
    {
        var wallet = _walletRepository.Get(w => w.UserId == userId).SingleOrDefault();

        if (wallet == null)
        {
            var user = _userRepository.Get(userId);
            Guard.Against.Null(user);

            wallet = new Wallet
            {
                CreatedAt = DateTime.UtcNow,
                UserId = userId,
                Currency = currency
            };
            await _context.Wallets.AddAsync(wallet, cancellationToken);
            user.Wallet = wallet;
            _context.Users.Update(user);
        }

        await _transactionRepository.AddAsync(new Transaction
        {
            CreatedAt = DateTime.UtcNow,
            WalletId = wallet.Id,
            Amount = amount
        });
        await _walletRepository.UpdateAmountAsync(wallet.Id, amount);
    }
}