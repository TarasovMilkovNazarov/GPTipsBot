using Ardalis.GuardClauses;
using System.Globalization;
using GPTipsBot.Config;
using GPTipsBot.Db;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services.YooKassa;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

public static class PaymentCallbacks
{
    public const string StarsPrefix = "pay_stars_";
    public const string YooKassaPrefix = "pay_yk_";
    public const string PackagePrefix = "dep_";
    public const string YooKassaCheckPrefix = "yk_check_";

    public static bool TryParsePackage(string? data, out int starsCount)
    {
        starsCount = 0;
        if (string.IsNullOrWhiteSpace(data) ||
            !data.StartsWith(PackagePrefix, StringComparison.Ordinal) ||
            !int.TryParse(data[PackagePrefix.Length..], out starsCount))
        {
            return false;
        }

        return starsCount >= PaymentConfig.MinRechargeStars &&
               MoneyService.ToKopecks(starsCount) >= PaymentConfig.MinRechargeRub * 100L;
    }

    public static bool TryParseCheck(string? data, out long invoiceId)
    {
        invoiceId = 0;
        return !string.IsNullOrWhiteSpace(data) &&
               data.StartsWith(YooKassaCheckPrefix, StringComparison.Ordinal) &&
               long.TryParse(data[YooKassaCheckPrefix.Length..], out invoiceId) &&
               invoiceId > 0;
    }

    public static bool TryParse(string? data, out string provider, out int starsCount)
    {
        provider = string.Empty;
        starsCount = 0;

        if (string.IsNullOrWhiteSpace(data))
        {
            return false;
        }

        if (data.StartsWith(StarsPrefix, StringComparison.Ordinal) &&
            int.TryParse(data[StarsPrefix.Length..], out starsCount) &&
            starsCount >= PaymentConfig.MinRechargeStars)
        {
            provider = StarsPrefix;
            return true;
        }

        if (data.StartsWith(YooKassaPrefix, StringComparison.Ordinal) &&
            int.TryParse(data[YooKassaPrefix.Length..], out starsCount) &&
            starsCount >= PaymentConfig.MinRechargeStars &&
            MoneyService.ToKopecks(starsCount) >= PaymentConfig.MinRechargeRub * 100L)
        {
            provider = YooKassaPrefix;
            return true;
        }

        return false;
    }
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
    private readonly YooKassaClient _yooKassaClient;
    private readonly ILogger<MoneyService> _logger;
    private event EventHandler<Wallet> UserBalanceChanged;

    public MoneyService(WalletRepository walletRepository, UserRepository userRepository,
        InvoiceRepository invoiceRepository, TransactionRepository transactionRepository,
        ApplicationContext context, ITelegramBotClient botClient, UserService userService,
        YooKassaClient yooKassaClient, ILogger<MoneyService> logger)
    {
        _walletRepository = walletRepository;
        _userRepository = userRepository;
        _invoiceRepository = invoiceRepository;
        _transactionRepository = transactionRepository;
        _context = context;
        _botClient = botClient;
        _userService = userService;
        _yooKassaClient = yooKassaClient;
        _logger = logger;
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

    public InlineKeyboardMarkup BuildDepositPackagesKeyboard()
    {
        var rows = new List<IEnumerable<InlineKeyboardButton>>();
        foreach (var stars in PaymentConfig.DepositStarPackages)
        {
            if (stars < PaymentConfig.MinRechargeStars ||
                ToKopecks(stars) < PaymentConfig.MinRechargeRub * 100L)
            {
                continue;
            }

            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    string.Format(BotResponse.DepositPackageButton, stars, FormatRubAmount(stars)),
                    $"{PaymentCallbacks.PackagePrefix}{stars}")
            });
        }

        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData(BotUI.CancelButton, BotMenu.CancelCommand)
        });
        return new InlineKeyboardMarkup(rows);
    }

    public InlineKeyboardMarkup BuildPaymentMethodKeyboard(int starsCount)
    {
        var rubAmount = FormatRubAmount(starsCount);
        var rows = new List<IEnumerable<InlineKeyboardButton>>
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    BotResponse.PayWithStarsButton,
                    $"{PaymentCallbacks.StarsPrefix}{starsCount}")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    string.Format(BotResponse.PayWithYooKassaButton, rubAmount),
                    $"{PaymentCallbacks.YooKassaPrefix}{starsCount}")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(BotUI.CancelButton, BotMenu.CancelCommand)
            }
        };

        return new InlineKeyboardMarkup(rows);
    }

    public async Task SendInvoice(long userId, int starsCount = 100)
    {
        var invoice = new Invoice
        {
            CreatedAt = DateTime.UtcNow,
            UserId = userId,
            Amount = starsCount,
            Currency = Currency.Stars,
            Status = InvoiceStatus.Created,
            Provider = PaymentProvider.TelegramStars
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
            Status = InvoiceStatus.Created,
            Provider = PaymentProvider.TelegramStars
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

    public async Task CreateYooKassaPaymentAsync(long userId, int starsCount, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "CreateYooKassa: start userId={UserId} stars={Stars} enabled={Enabled} rubPerStar={RubPerStar}",
            userId,
            starsCount,
            YooKassaConfig.IsEnabled,
            YooKassaConfig.RubPerStar);

        if (starsCount < PaymentConfig.MinRechargeStars ||
            ToKopecks(starsCount) < PaymentConfig.MinRechargeRub * 100L)
        {
            throw new InvalidOperationException(
                $"Minimum YooKassa top-up is {PaymentConfig.MinRechargeRub} RUB");
        }

        // Without shop keys — UI stub for YooKassa moderation screenshots.
        if (!YooKassaConfig.IsEnabled)
        {
            _logger.LogInformation("CreateYooKassa: keys missing → sending UI stub");
            await SendYooKassaPaymentStubAsync(userId, starsCount, cancellationToken);
            return;
        }

        var fiatKopecks = ToKopecks(starsCount);
        var invoice = new Invoice
        {
            CreatedAt = DateTime.UtcNow,
            UserId = userId,
            Amount = starsCount,
            Currency = Currency.Stars,
            Status = InvoiceStatus.Created,
            Provider = PaymentProvider.YooKassa,
            FiatAmountKopecks = fiatKopecks
        };
        _invoiceRepository.Create(invoice);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "CreateYooKassa: invoice created id={InvoiceId} fiatKopecks={FiatKopecks}",
            invoice.Id,
            fiatKopecks);

        var rubValue = (fiatKopecks / 100m).ToString("0.00", CultureInfo.InvariantCulture);
        _logger.LogInformation(
            "CreateYooKassa: calling API amount={Amount} returnUrl={ReturnUrl} idempotence=invoice-{InvoiceId}",
            rubValue,
            YooKassaConfig.ReturnUrl,
            invoice.Id);

        var payment = await _yooKassaClient.CreatePaymentAsync(
            new CreateYooKassaPaymentRequest
            {
                Amount = new YooKassaAmount
                {
                    Value = rubValue,
                    Currency = CurrencyCode.Rub
                },
                Capture = true,
                Confirmation = new YooKassaConfirmationRequest
                {
                    Type = "redirect",
                    ReturnUrl = YooKassaConfig.ReturnUrl
                },
                Description = string.Format(BotResponse.YooKassaPaymentDescription, starsCount),
                Metadata = new Dictionary<string, string>
                {
                    ["invoice_id"] = invoice.Id.ToString(CultureInfo.InvariantCulture),
                    ["user_id"] = userId.ToString(CultureInfo.InvariantCulture),
                    ["stars"] = starsCount.ToString(CultureInfo.InvariantCulture)
                }
            },
            idempotenceKey: $"invoice-{invoice.Id}",
            cancellationToken);

        _logger.LogInformation(
            "CreateYooKassa: API payment id={PaymentId} status={Status} confirmationUrl={HasUrl}",
            payment.Id,
            payment.Status,
            !string.IsNullOrWhiteSpace(payment.Confirmation?.ConfirmationUrl));

        if (string.IsNullOrWhiteSpace(payment.Confirmation?.ConfirmationUrl))
        {
            invoice.Status = InvoiceStatus.Failed;
            await _context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("YooKassa did not return confirmation_url");
        }

        invoice.ExternalPaymentId = payment.Id;
        await _context.Invoices
            .Where(i => i.Id == invoice.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(i => i.ExternalPaymentId, payment.Id),
                cancellationToken);

        await SendYooKassaPaymentLinkAsync(
            userId, starsCount, rubValue, payment.Confirmation.ConfirmationUrl, invoice.Id, cancellationToken);
        _logger.LogInformation(
            "CreateYooKassa: link sent to userId={UserId} invoiceId={InvoiceId} paymentId={PaymentId}",
            userId,
            invoice.Id,
            payment.Id);
    }

    private async Task SendYooKassaPaymentStubAsync(
        long userId, int starsCount, CancellationToken cancellationToken)
    {
        var rubValue = FormatRubAmount(starsCount);
        // Placeholder URL so the "Pay" button renders for screenshots.
        var stubUrl = string.IsNullOrWhiteSpace(AppConfig.BotName)
            ? "https://yookassa.ru/"
            : $"https://t.me/{AppConfig.BotName.TrimStart('@')}";

        await SendYooKassaPaymentLinkAsync(userId, starsCount, rubValue, stubUrl, invoiceId: null, cancellationToken);
    }

    private async Task SendYooKassaPaymentLinkAsync(
        long userId,
        int starsCount,
        string rubValue,
        string paymentUrl,
        long? invoiceId,
        CancellationToken cancellationToken)
    {
        var rows = new List<IEnumerable<InlineKeyboardButton>>
        {
            new[]
            {
                InlineKeyboardButton.WithUrl(BotResponse.OpenYooKassaPaymentButton, paymentUrl)
            }
        };

        if (invoiceId is > 0)
        {
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    BotResponse.CheckYooKassaPaymentButton,
                    $"{PaymentCallbacks.YooKassaCheckPrefix}{invoiceId}")
            });
        }

        await _botClient.SendMessage(
            userId,
            string.Format(BotResponse.YooKassaPaymentLinkResponse, starsCount, rubValue),
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Poll YooKassa API for an invoice (manual button / background sync when webhook is missed).
    /// </summary>
    public async Task<PaymentConfirmResult> SyncYooKassaInvoiceAsync(
        long invoiceId,
        long userId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "SyncYooKassa: manual/user sync invoiceId={InvoiceId} userId={UserId}",
            invoiceId,
            userId);

        var invoice = _invoiceRepository.GetById(invoiceId);
        if (invoice == null ||
            invoice.Provider != PaymentProvider.YooKassa ||
            invoice.UserId != userId)
        {
            _logger.LogWarning(
                "SyncYooKassa: invoice not found or user mismatch invoiceId={InvoiceId}",
                invoiceId);
            return PaymentConfirmResult.Ignored;
        }

        if (invoice.Status == InvoiceStatus.Paid)
        {
            return PaymentConfirmResult.DepositCredited;
        }

        if (string.IsNullOrWhiteSpace(invoice.ExternalPaymentId))
        {
            _logger.LogWarning("SyncYooKassa: invoice {InvoiceId} has no ExternalPaymentId", invoiceId);
            return PaymentConfirmResult.Ignored;
        }

        return await ConfirmYooKassaPaymentAsync(invoice.ExternalPaymentId, cancellationToken);
    }

    /// <summary>
    /// Background catch-up for Created YooKassa invoices with a known payment id.
    /// </summary>
    public async Task<int> SyncPendingYooKassaPaymentsAsync(CancellationToken cancellationToken)
    {
        if (!YooKassaConfig.IsEnabled)
        {
            return 0;
        }

        var pending = _invoiceRepository.GetPendingYooKassa(TimeSpan.FromHours(24));
        _logger.LogInformation("SyncYooKassa: pending invoices to poll={Count}", pending.Count);

        var credited = 0;
        foreach (var invoice in pending)
        {
            if (string.IsNullOrWhiteSpace(invoice.ExternalPaymentId))
            {
                continue;
            }

            try
            {
                var result = await ConfirmYooKassaPaymentAsync(invoice.ExternalPaymentId, cancellationToken);
                if (result == PaymentConfirmResult.DepositCredited)
                {
                    credited++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "SyncYooKassa: failed for invoice {InvoiceId} payment {PaymentId}",
                    invoice.Id,
                    invoice.ExternalPaymentId);
            }
        }

        _logger.LogInformation("SyncYooKassa: poll finished creditedOrConfirmed={Credited}", credited);
        return credited;
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
        if (invoice == null ||
            invoice.Status != InvoiceStatus.Created ||
            invoice.Provider != PaymentProvider.TelegramStars)
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
        if (invoice == null || invoice.Provider != PaymentProvider.TelegramStars)
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

    public async Task<PaymentConfirmResult> ConfirmYooKassaPaymentAsync(
        string paymentId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("ConfirmYooKassa: start paymentId={PaymentId}", paymentId);

        if (string.IsNullOrWhiteSpace(paymentId))
        {
            _logger.LogWarning("ConfirmYooKassa: empty paymentId → Ignored");
            return PaymentConfirmResult.Ignored;
        }

        _logger.LogInformation("ConfirmYooKassa: fetching payment from API {PaymentId}", paymentId);
        var payment = await _yooKassaClient.GetPaymentAsync(paymentId, cancellationToken);
        _logger.LogInformation(
            "ConfirmYooKassa: API payment status={Status} paid={Paid} amount={Amount} {Currency} metadata={Metadata}",
            payment.Status,
            payment.Paid,
            payment.Amount?.Value,
            payment.Amount?.Currency,
            payment.Metadata == null
                ? "(null)"
                : string.Join(", ", payment.Metadata.Select(kv => $"{kv.Key}={kv.Value}")));

        if (!string.Equals(payment.Status, "succeeded", StringComparison.OrdinalIgnoreCase) || !payment.Paid)
        {
            _logger.LogWarning(
                "ConfirmYooKassa: payment not succeeded/paid → Ignored (status={Status}, paid={Paid})",
                payment.Status,
                payment.Paid);
            return PaymentConfirmResult.Ignored;
        }

        var invoice = _invoiceRepository.GetByExternalPaymentId(paymentId);
        _logger.LogInformation(
            "ConfirmYooKassa: lookup by ExternalPaymentId → {Found}",
            invoice == null ? "not found" : $"invoiceId={invoice.Id} status={invoice.Status} userId={invoice.UserId}");

        if (invoice == null &&
            payment.Metadata != null &&
            payment.Metadata.TryGetValue("invoice_id", out var invoiceIdRaw) &&
            long.TryParse(invoiceIdRaw, out var invoiceId))
        {
            invoice = _invoiceRepository.GetById(invoiceId);
            _logger.LogInformation(
                "ConfirmYooKassa: lookup by metadata.invoice_id={InvoiceId} → {Found}",
                invoiceId,
                invoice == null ? "not found" : $"status={invoice.Status} userId={invoice.UserId}");
        }

        if (invoice == null || invoice.Provider != PaymentProvider.YooKassa)
        {
            _logger.LogWarning(
                "ConfirmYooKassa: no matching YooKassa invoice for payment {PaymentId} (invoiceNull={InvoiceNull}, provider={Provider})",
                paymentId,
                invoice == null,
                invoice?.Provider);
            return PaymentConfirmResult.Ignored;
        }

        if (invoice.Status == InvoiceStatus.Paid)
        {
            _logger.LogInformation(
                "ConfirmYooKassa: invoice {InvoiceId} already Paid → DepositCredited (idempotent)",
                invoice.Id);
            return PaymentConfirmResult.DepositCredited;
        }

        if (invoice.Status != InvoiceStatus.Created)
        {
            _logger.LogWarning(
                "ConfirmYooKassa: invoice {InvoiceId} unexpected status={Status} → Ignored",
                invoice.Id,
                invoice.Status);
            return PaymentConfirmResult.Ignored;
        }

        if (invoice.FiatAmountKopecks is long expectedKopecks)
        {
            if (!TryParseRubToKopecks(payment.Amount?.Value, out var paidKopecks) ||
                paidKopecks != expectedKopecks ||
                !string.Equals(payment.Amount?.Currency, CurrencyCode.Rub, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "ConfirmYooKassa: amount mismatch paymentId={PaymentId} invoiceId={InvoiceId} expectedKopecks={Expected} paidValue={PaidValue} paidCurrency={PaidCurrency} paidKopecks={PaidKopecks}",
                    paymentId,
                    invoice.Id,
                    expectedKopecks,
                    payment.Amount?.Value,
                    payment.Amount?.Currency,
                    paidKopecks);
                return PaymentConfirmResult.Ignored;
            }

            _logger.LogInformation(
                "ConfirmYooKassa: amount OK invoiceId={InvoiceId} kopecks={Kopecks}",
                invoice.Id,
                expectedKopecks);
        }

        if (string.IsNullOrWhiteSpace(invoice.ExternalPaymentId))
        {
            _logger.LogInformation(
                "ConfirmYooKassa: backfilling ExternalPaymentId on invoice {InvoiceId}",
                invoice.Id);
            await _context.Invoices
                .Where(i => i.Id == invoice.Id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(i => i.ExternalPaymentId, paymentId),
                    cancellationToken);
        }

        _logger.LogInformation(
            "ConfirmYooKassa: claiming invoice {InvoiceId} Created→Paid",
            invoice.Id);
        var claimed = await _context.Invoices
            .Where(i => i.Id == invoice.Id && i.Status == InvoiceStatus.Created)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(i => i.Status, InvoiceStatus.Paid),
                cancellationToken);

        if (claimed == 0)
        {
            _logger.LogInformation(
                "ConfirmYooKassa: claim lost race for invoice {InvoiceId} → DepositCredited (idempotent)",
                invoice.Id);
            return PaymentConfirmResult.DepositCredited;
        }

        _logger.LogInformation(
            "ConfirmYooKassa: crediting userId={UserId} stars={Stars}",
            invoice.UserId,
            invoice.Amount);
        await AddMoneyAsync(invoice.UserId, (int)invoice.Amount, Currency.Stars, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("ConfirmYooKassa: SaveChanges done for invoice {InvoiceId}", invoice.Id);

        try
        {
            var profile = await _userService.GetUserProfile(invoice.UserId);
            var reply = string.Format(BotResponse.ProfileResponse, profile.FirstName,
                profile.LastName, profile.Stars, profile.GptRequests, profile.Images, profile.ImageTexts);
            var replyMarkup = new InlineKeyboardMarkup(InlineKeyboardButton
                .WithCallbackData(BotResponse.AddMoneyResponse, BotMenu.DepositCommand));

            await _botClient.SendMessage(invoice.UserId,
                BotResponse.YooKassaPaymentSucceeded + Environment.NewLine + Environment.NewLine + reply,
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);
            _logger.LogInformation("ConfirmYooKassa: user {UserId} notified", invoice.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify user {UserId} about YooKassa payment", invoice.UserId);
        }

        _logger.LogInformation(
            "ConfirmYooKassa: success paymentId={PaymentId} invoiceId={InvoiceId} userId={UserId}",
            paymentId,
            invoice.Id,
            invoice.UserId);
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

    public static long ToKopecks(int starsCount)
    {
        var rub = starsCount * YooKassaConfig.RubPerStar;
        return (long)decimal.Round(rub * 100m, 0, MidpointRounding.AwayFromZero);
    }

    public static string FormatRubAmount(int starsCount) =>
        (ToKopecks(starsCount) / 100m).ToString("0.##", CultureInfo.InvariantCulture);

    private static bool TryParseRubToKopecks(string? value, out long kopecks)
    {
        kopecks = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var rub))
        {
            return false;
        }

        kopecks = (long)decimal.Round(rub * 100m, 0, MidpointRounding.AwayFromZero);
        return true;
    }

    private void UserBalanceChangedHandler(object? sender, Wallet wallet)
    {
        var message = "#deposit" + Environment.NewLine + $"{wallet.UserId} balance: {wallet.Balance} stars";

        _botClient.SendMessage(AppConfig.AdminIds.First(), message);
    }
}
