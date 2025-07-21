using Ardalis.GuardClauses;
using GPTipsBot.Db;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using Telegram.Bot;
using Telegram.Bot.Types.Payments;
using Telegram.Bot.Types.ReplyMarkups;
using Invoice = GPTipsBot.Models.Invoice;

namespace GPTipsBot.Services;

public class MoneyService
{
    private readonly WalletRepository _walletRepository;
    private readonly UserRepository _userRepository;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly TransactionRepository _transactionRepository;
    private readonly ApplicationContext _context;
    private readonly ITelegramBotClient _botClient;
    private readonly UserService _userService;
    private event EventHandler<Wallet> UserBalanceChanged;

    public MoneyService(WalletRepository walletRepository, UserRepository userRepository,
        InvoiceRepository invoiceRepository, TransactionRepository transactionRepository,
        ApplicationContext context, ITelegramBotClient botClient, UserService userService)
    {
        _walletRepository = walletRepository;
        _userRepository = userRepository;
        _invoiceRepository = invoiceRepository;
        _transactionRepository = transactionRepository;
        _context = context;
        _botClient = botClient;
        _userService = userService;
        UserBalanceChanged += UserBalanceChangedHandler;
    }

    public async Task<bool> PayForMusic(long userId, int amount)
    {
        var wallet = _walletRepository.Get(w => w.UserId == userId).FirstOrDefault();
        if (wallet == null || wallet.Balance < amount)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(InlineKeyboardButton
                .WithCallbackData(BotResponse.AddMoneyResponse, BotMenu.DepositCommand));

            var response = string.Format(BotResponse.InsufficientBalanceForMusic, amount);

            await _botClient.SendTextMessageAsync(userId, response,
                replyMarkup: inlineKeyboard);
            return false;
        }

        wallet.Balance -= 10;
        _walletRepository.Update(wallet);

        return true;
    }

    public async Task SendInvoice(long userId, int starsCount = 100)
    {
        var invoice = new Invoice
        {
            CreatedAt = DateTime.UtcNow,
            UserId = userId,
            Amount = starsCount,
            Currency = Currency.Stars,
            Status = InvoiceStatus.Created
        };
        _invoiceRepository.Create(invoice);

        await _context.SaveChangesAsync();

        await _botClient.SendInvoiceAsync(
            chatId: userId,
            title: BotResponse.DepositTitle,
            description: BotResponse.DepositResponse,
            payload: invoice.Id.ToString(),
            providerToken: "",
            currency: Currency.Stars,
            prices: new[] { new LabeledPrice("Premium Access", starsCount) }
            ,
            startParameter: "premium_subscription"
            );

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
        await _walletRepository.UpdateAmountAsync(wallet, amount);
        UserBalanceChanged?.Invoke(this, wallet);
    }

    private void UserBalanceChangedHandler(object? sender, Wallet wallet)
    {
        var message = "#deposit" + Environment.NewLine + $"{wallet.UserId} balance: {wallet.Balance} stars";

        _botClient.SendTextMessageAsync(AppConfig.AdminIds.First(), message);
    }
}