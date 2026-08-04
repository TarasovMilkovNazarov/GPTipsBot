using Ardalis.GuardClauses;
using GPTipsBot.Config;
using GPTipsBot.Db;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types.Payments;
using Telegram.Bot.Types.ReplyMarkups;
using Invoice = GPTipsBot.Models.Invoice;

namespace GPTipsBot.Services;

public enum PaymentConfirmResult
{
    Ignored,
    DepositCredited,
    DonateConfirmed
}

public class MoneyService
{
    private const string DonatePayloadPrefix = "donate_";

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

    public async Task<bool> TryPay(long userId, int amount)
    {
        var wallet = _walletRepository.Get(w => w.UserId == userId).FirstOrDefault();
        if (wallet == null || wallet.Balance < amount)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(InlineKeyboardButton
                .WithCallbackData(BotResponse.AddMoneyResponse, BotMenu.DepositCommand));

            var response = string.Format(BotResponse.InsufficientBalance, amount);

            await _botClient.SendMessage(userId, response,
                replyMarkup: inlineKeyboard);
            return false;
        }

        wallet.Balance -= amount;
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

        await _botClient.SendInvoice(
            chatId: userId,
            title: BotResponse.InvoiceTitle,
            description: BotResponse.InvoiceText,
            payload: invoice.Id.ToString(),
            providerToken: "",
            currency: Currency.Stars,
            prices: new[] { new LabeledPrice("Premium Access", starsCount) }
            ,
            startParameter: "premium_subscription"
        );

    }

    public async Task SendDonateInvoice(long userId, int starsCount = 100)
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

        await _botClient.SendInvoice(
            chatId: userId,
            title: string.Format(BotResponse.DonateTitle, starsCount),
            description: BotResponse.DonateText,
            payload: $"{DonatePayloadPrefix}{invoice.Id}",
            providerToken: "",
            currency: Currency.Stars,
            prices: new[] { new LabeledPrice("Donate", starsCount) }
        );

    }

    public bool TryValidatePreCheckout(PreCheckoutQuery query, out string? errorMessage)
    {
        errorMessage = null;

        if (!TryParseInvoicePayload(query.InvoicePayload, out var invoiceId, out _))
        {
            errorMessage = "Invalid invoice";
            return false;
        }

        var invoice = _invoiceRepository.GetById(invoiceId);
        if (invoice == null || invoice.Status != InvoiceStatus.Created)
        {
            errorMessage = "Invoice not found";
            return false;
        }

        if (invoice.UserId != query.From.Id)
        {
            errorMessage = "Invoice user mismatch";
            return false;
        }

        if (invoice.Amount != query.TotalAmount || query.Currency != Currency.Stars)
        {
            errorMessage = "Invoice amount mismatch";
            return false;
        }

        return true;
    }

    public async Task<PaymentConfirmResult> ConfirmSuccessfulPaymentAsync(
        SuccessfulPayment payment,
        long userId,
        CancellationToken cancellationToken)
    {
        if (!TryParseInvoicePayload(payment.InvoicePayload, out var invoiceId, out var isDonate))
        {
            return PaymentConfirmResult.Ignored;
        }

        var invoice = _invoiceRepository.GetById(invoiceId);
        if (invoice == null)
        {
            return PaymentConfirmResult.Ignored;
        }

        if (invoice.UserId != userId ||
            invoice.Amount != payment.TotalAmount ||
            payment.Currency != Currency.Stars)
        {
            return PaymentConfirmResult.Ignored;
        }

        if (invoice.Status == InvoiceStatus.Paid)
        {
            return isDonate ? PaymentConfirmResult.DonateConfirmed : PaymentConfirmResult.DepositCredited;
        }

        if (invoice.Status != InvoiceStatus.Created)
        {
            return PaymentConfirmResult.Ignored;
        }

        // Atomic claim: only one concurrent confirm can transition Created -> Paid.
        var claimed = await _context.Invoices
            .Where(i => i.Id == invoiceId && i.Status == InvoiceStatus.Created)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(i => i.Status, InvoiceStatus.Paid),
                cancellationToken);

        if (claimed == 0)
        {
            return isDonate ? PaymentConfirmResult.DonateConfirmed : PaymentConfirmResult.DepositCredited;
        }

        if (isDonate)
        {
            return PaymentConfirmResult.DonateConfirmed;
        }

        await AddMoneyAsync(userId, (int)payment.TotalAmount, Currency.Stars, cancellationToken);
        return PaymentConfirmResult.DepositCredited;
    }

    public static bool TryParseInvoicePayload(string? payload, out long invoiceId, out bool isDonate)
    {
        invoiceId = 0;
        isDonate = false;

        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        if (payload.StartsWith(DonatePayloadPrefix, StringComparison.Ordinal))
        {
            isDonate = true;
            return long.TryParse(payload[DonatePayloadPrefix.Length..], out invoiceId);
        }

        return long.TryParse(payload, out invoiceId);
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
            _walletRepository.Create(wallet);
            await _context.SaveChangesAsync(cancellationToken);
            user.Wallet = wallet;
            _context.Users.Update(user);
        }

        _transactionRepository.Create(new Transaction
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

        _botClient.SendMessage(AppConfig.AdminIds.First(), message);
    }
}