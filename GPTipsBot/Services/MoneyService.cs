using Ardalis.GuardClauses;
using System.Globalization;
using GPTipsBot.Config;
using GPTipsBot.Db;
using GPTipsBot.Models;
using GPTipsBot.Repositories;
using GPTipsBot.Resources;
using GPTipsBot.Services.LavaTop;
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

public sealed record YooKassaCheckoutResult(
    long InvoiceId,
    string ConfirmationUrl,
    int Gems,
    string RubAmount,
    string ExternalPaymentId);

public sealed record LavaTopCheckoutResult(
    long InvoiceId,
    string PaymentUrl,
    int Gems,
    string Amount,
    string Currency,
    string ExternalPaymentId);

public static class PaymentCallbacks
{
    public const string StarsPrefix = "pay_stars_";
    public const string YooKassaPrefix = "pay_yk_";
    public const string LavaTopPrefix = "pay_lt_";
    public const string YooKassaCheckPrefix = "yk_check_";
    public const string LavaTopCheckPrefix = "lt_check_";
    public const string MethodChoicePrefix = "dep_method_";
    /// <summary>Goes back to the method-choice screen without touching the saved preference.</summary>
    public const string BackToMethodChoice = "dep_back_method";

    /// <summary>Parses a payment-method pick on the /deposit method-choice screen.</summary>
    public static bool TryParseMethodChoice(string? data, out PaymentProvider provider)
    {
        provider = default;
        return !string.IsNullOrWhiteSpace(data) &&
               data.StartsWith(MethodChoicePrefix, StringComparison.Ordinal) &&
               Enum.TryParse(data[MethodChoicePrefix.Length..], out provider);
    }

    public static bool TryParseCheck(string? data, out long invoiceId)
    {
        invoiceId = 0;
        return !string.IsNullOrWhiteSpace(data) &&
               data.StartsWith(YooKassaCheckPrefix, StringComparison.Ordinal) &&
               long.TryParse(data[YooKassaCheckPrefix.Length..], out invoiceId) &&
               invoiceId > 0;
    }

    public static bool TryParseLavaTopCheck(string? data, out long invoiceId)
    {
        invoiceId = 0;
        return !string.IsNullOrWhiteSpace(data) &&
               data.StartsWith(LavaTopCheckPrefix, StringComparison.Ordinal) &&
               long.TryParse(data[LavaTopCheckPrefix.Length..], out invoiceId) &&
               invoiceId > 0;
    }

    public static bool TryParse(string? data, out string provider, out int gemsCount)
    {
        provider = string.Empty;
        gemsCount = 0;

        if (string.IsNullOrWhiteSpace(data))
        {
            return false;
        }

        // Stars has its own floor (effectively "at least 1 XTR" — TelegramStarsConfig.GemsToXtr rounds
        // up), never the RUB-derived PaymentConfig.MinRechargeGems: real Stars packages start at 10⭐,
        // well under that 1000-gem floor.
        if (data.StartsWith(StarsPrefix, StringComparison.Ordinal) &&
            int.TryParse(data[StarsPrefix.Length..], out gemsCount) &&
            gemsCount > 0 &&
            gemsCount <= TelegramStarsConfig.MaxGemsPerInvoice)
        {
            provider = StarsPrefix;
            return true;
        }

        if (data.StartsWith(YooKassaPrefix, StringComparison.Ordinal) &&
            int.TryParse(data[YooKassaPrefix.Length..], out gemsCount) &&
            gemsCount >= PaymentConfig.MinRechargeGems &&
            MoneyService.ToKopecks(gemsCount) >= PaymentConfig.MinRechargeRub * 100L)
        {
            provider = YooKassaPrefix;
            return true;
        }

        if (data.StartsWith(LavaTopPrefix, StringComparison.Ordinal) &&
            int.TryParse(data[LavaTopPrefix.Length..], out gemsCount) &&
            gemsCount > 0 &&
            MoneyService.IsLavaTopAmountAllowed(gemsCount))
        {
            provider = LavaTopPrefix;
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
    private readonly LavaTopClient _lavaTopClient;
    private readonly ILogger<MoneyService> _logger;

    public MoneyService(WalletRepository walletRepository, UserRepository userRepository,
        InvoiceRepository invoiceRepository, TransactionRepository transactionRepository,
        ApplicationContext context, ITelegramBotClient botClient, UserService userService,
        YooKassaClient yooKassaClient, LavaTopClient lavaTopClient, ILogger<MoneyService> logger)
    {
        _walletRepository = walletRepository;
        _userRepository = userRepository;
        _invoiceRepository = invoiceRepository;
        _transactionRepository = transactionRepository;
        _context = context;
        _botClient = botClient;
        _userService = userService;
        _yooKassaClient = yooKassaClient;
        _lavaTopClient = lavaTopClient;
        _logger = logger;
    }

    public async Task<bool> TryPay(long userId, int amount)
    {
        var wallet = _walletRepository.Get(w => w.UserId == userId).FirstOrDefault();
        if (wallet == null || wallet.Balance < amount)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(InlineKeyboardButton
                .WithCallbackData(BotResponse.AddMoneyResponse, BotMenu.DepositCommand));

            var response = string.Format(BotResponse.InsufficientBalance, amount);
            var chatId = ResolveTelegramChatId(userId);
            if (chatId is long telegramChatId)
            {
                await _botClient.SendMessage(telegramChatId, response,
                    replyMarkup: inlineKeyboard);
            }

            return false;
        }

        wallet.Balance -= amount;
        _walletRepository.Update(wallet);

        return true;
    }

    /// <summary>
    /// /deposit's first screen: pick a rail (Stars / RUB card / international card) before ever talking
    /// about an amount. RUB and international minimums differ by an order of magnitude (50 ₽ vs $5), so
    /// a single amount-first list could never cleanly serve both — see the /deposit refactor discussion.
    /// </summary>
    public InlineKeyboardMarkup BuildPaymentMethodChoiceKeyboard()
    {
        var rows = new List<IEnumerable<InlineKeyboardButton>>
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    BotResponse.PaymentMethodStarsOption,
                    $"{PaymentCallbacks.MethodChoicePrefix}{PaymentProvider.TelegramStars}")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    BotResponse.PaymentMethodYooKassaOption,
                    $"{PaymentCallbacks.MethodChoicePrefix}{PaymentProvider.YooKassa}")
            }
        };

        if (LavaTopConfig.IsEnabled)
        {
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    BotResponse.PaymentMethodLavaTopOption,
                    $"{PaymentCallbacks.MethodChoicePrefix}{PaymentProvider.LavaTop}")
            });
        }

        rows.Add(new[] { TelegramBotUiService.BackToProfileButton });
        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>
    /// /deposit's second screen, once a rail is chosen (or remembered from <see cref="BotSettings"/>):
    /// packages priced natively for that rail, each button already carrying the create-payment callback
    /// (<see cref="PaymentCallbacks.TryParse"/>) — no more "which method for this amount" step needed.
    /// </summary>
    public InlineKeyboardMarkup BuildDepositAmountKeyboard(PaymentProvider provider)
    {
        var rows = new List<IEnumerable<InlineKeyboardButton>>();

        var (packages, prefix, formatPrice) = provider switch
        {
            PaymentProvider.TelegramStars => (
                TelegramStarsConfig.DepositPackages,
                PaymentCallbacks.StarsPrefix,
                (Func<int, string>)(gems => $"{TelegramStarsConfig.GemsToXtr(gems)}⭐")),
            PaymentProvider.LavaTop => (
                LavaTopConfig.DepositPackages,
                PaymentCallbacks.LavaTopPrefix,
                (Func<int, string>)(gems => $"{FormatLavaTopAmount(gems)} {LavaTopConfig.Currency}")),
            _ => (
                PaymentConfig.DepositGemPackages,
                PaymentCallbacks.YooKassaPrefix,
                (Func<int, string>)(gems => $"{FormatRubAmount(gems)} ₽")),
        };

        foreach (var gems in packages)
        {
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    string.Format(BotResponse.DepositPackageButtonGeneric, gems, formatPrice(gems)),
                    $"{prefix}{gems}")
            });
        }

        rows.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData(
                BotResponse.BackToPaymentMethodButton, PaymentCallbacks.BackToMethodChoice)
        });

        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>Header for <see cref="BuildDepositAmountKeyboard"/>, stating that rail's own gem rate.</summary>
    public static string GetDepositAmountHeader(PaymentProvider provider) => provider switch
    {
        PaymentProvider.TelegramStars => string.Format(
            BotResponse.DepositAmountHeaderStars, TelegramStarsConfig.GemsPerXtr),
        PaymentProvider.LavaTop => string.Format(
            BotResponse.DepositAmountHeaderLavaTop, LavaTopConfig.GemsPerUnit, LavaTopConfig.Currency),
        _ => string.Format(BotResponse.DepositAmountHeaderYooKassa, YooKassaConfig.GemsPerRub),
    };

    public async Task SendInvoice(long userId, long telegramChatId, int gemsCount = 100)
    {
        if (gemsCount < 1 || gemsCount > TelegramStarsConfig.MaxGemsPerInvoice)
        {
            throw new ArgumentOutOfRangeException(
                nameof(gemsCount),
                gemsCount,
                $"A Telegram invoice tops out at {TelegramStarsConfig.MaxGemsPerInvoice} gems");
        }

        // Amount is what lands in the wallet; Telegram is billed the matching number of stars.
        var xtrPrice = TelegramStarsConfig.GemsToXtr(gemsCount);
        var invoice = new Invoice
        {
            CreatedAt = DateTime.UtcNow,
            UserId = userId,
            Amount = gemsCount,
            Currency = Currency.Stars,
            Status = InvoiceStatus.Created,
            Provider = PaymentProvider.TelegramStars
        };
        _invoiceRepository.Create(invoice);

        await _context.SaveChangesAsync();

        await _botClient.SendInvoice(
            chatId: telegramChatId,
            title: BotResponse.InvoiceTitle,
            description: BotResponse.InvoiceText,
            payload: invoice.Id.ToString(),
            providerToken: "",
            currency: Currency.Stars,
            prices: new[] { new LabeledPrice("Premium Access", xtrPrice) }
            ,
            startParameter: "premium_subscription"
        );

    }

    /// <summary>
    /// A donation is never credited to the wallet, so <paramref name="xtrCount"/> is Telegram Stars as
    /// typed by the user and is not converted through <see cref="TelegramStarsConfig.GemsPerXtr"/>.
    /// </summary>
    public async Task SendDonateInvoice(long userId, long telegramChatId, int xtrCount = 100)
    {
        var invoice = new Invoice
        {
            CreatedAt = DateTime.UtcNow,
            UserId = userId,
            Amount = xtrCount,
            Currency = Currency.Stars,
            Status = InvoiceStatus.Created,
            Provider = PaymentProvider.TelegramStars
        };
        _invoiceRepository.Create(invoice);

        await _context.SaveChangesAsync();

        await _botClient.SendInvoice(
            chatId: telegramChatId,
            title: string.Format(BotResponse.DonateTitle, xtrCount),
            description: BotResponse.DonateText,
            payload: $"{DonatePayloadPrefix}{invoice.Id}",
            providerToken: "",
            currency: Currency.Stars,
            prices: new[] { new LabeledPrice("Donate", xtrCount) }
        );

    }

    public async Task CreateYooKassaPaymentAsync(
        long userId,
        long telegramChatId,
        int gemsCount,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "CreateYooKassa: start userId={UserId} gems={Gems} enabled={Enabled} rubPerGem={RubPerGem}",
            userId,
            gemsCount,
            YooKassaConfig.IsEnabled,
            YooKassaConfig.GemsPerRub);

        if (gemsCount < PaymentConfig.MinRechargeGems ||
            ToKopecks(gemsCount) < PaymentConfig.MinRechargeRub * 100L)
        {
            throw new InvalidOperationException(
                $"Minimum YooKassa top-up is {PaymentConfig.MinRechargeRub} RUB");
        }

        // Without shop keys — UI stub for YooKassa moderation screenshots.
        if (!YooKassaConfig.IsEnabled)
        {
            _logger.LogInformation("CreateYooKassa: keys missing → sending UI stub");
            await SendYooKassaPaymentStubAsync(telegramChatId, gemsCount, cancellationToken);
            return;
        }

        var checkout = await CreateYooKassaCheckoutAsync(userId, gemsCount, returnUrl: null, cancellationToken);
        await SendYooKassaPaymentLinkAsync(
            telegramChatId,
            gemsCount,
            checkout.RubAmount,
            checkout.ConfirmationUrl,
            checkout.InvoiceId,
            cancellationToken);
        _logger.LogInformation(
            "CreateYooKassa: link sent to userId={UserId} invoiceId={InvoiceId} paymentId={PaymentId}",
            userId,
            checkout.InvoiceId,
            checkout.ExternalPaymentId);
    }

    /// <summary>
    /// Creates a YooKassa redirect payment and returns the confirmation URL (shared by bot + web).
    /// </summary>
    public async Task<YooKassaCheckoutResult> CreateYooKassaCheckoutAsync(
        long userId,
        int gemsCount,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!YooKassaConfig.IsEnabled)
        {
            throw new InvalidOperationException("YooKassa is not configured");
        }

        if (gemsCount < PaymentConfig.MinRechargeGems ||
            ToKopecks(gemsCount) < PaymentConfig.MinRechargeRub * 100L)
        {
            throw new InvalidOperationException(
                $"Minimum YooKassa top-up is {PaymentConfig.MinRechargeRub} RUB");
        }

        var fiatKopecks = ToKopecks(gemsCount);
        var invoice = new Invoice
        {
            CreatedAt = DateTime.UtcNow,
            UserId = userId,
            Amount = gemsCount,
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
        var effectiveReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
            ? YooKassaConfig.ReturnUrl
            : returnUrl.Trim();
        _logger.LogInformation(
            "CreateYooKassa: calling API amount={Amount} returnUrl={ReturnUrl} idempotence=invoice-{InvoiceId}",
            rubValue,
            effectiveReturnUrl,
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
                    ReturnUrl = effectiveReturnUrl
                },
                Description = string.Format(BotResponse.YooKassaPaymentDescription, gemsCount),
                Metadata = new Dictionary<string, string>
                {
                    ["invoice_id"] = invoice.Id.ToString(CultureInfo.InvariantCulture),
                    ["user_id"] = userId.ToString(CultureInfo.InvariantCulture),
                    ["gems"] = gemsCount.ToString(CultureInfo.InvariantCulture)
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

        return new YooKassaCheckoutResult(
            invoice.Id,
            payment.Confirmation.ConfirmationUrl,
            gemsCount,
            rubValue,
            payment.Id);
    }

    private async Task SendYooKassaPaymentStubAsync(
        long telegramChatId, int gemsCount, CancellationToken cancellationToken)
    {
        var rubValue = FormatRubAmount(gemsCount);
        // Placeholder URL so the "Pay" button renders for screenshots.
        var stubUrl = string.IsNullOrWhiteSpace(AppConfig.BotName)
            ? "https://yookassa.ru/"
            : $"https://t.me/{AppConfig.BotName.TrimStart('@')}";

        await SendYooKassaPaymentLinkAsync(telegramChatId, gemsCount, rubValue, stubUrl, invoiceId: null, cancellationToken);
    }

    private async Task SendYooKassaPaymentLinkAsync(
        long telegramChatId,
        int gemsCount,
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
            telegramChatId,
            string.Format(BotResponse.YooKassaPaymentLinkResponse, gemsCount, rubValue),
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

        var pending = _invoiceRepository.GetPending(PaymentProvider.YooKassa, TimeSpan.FromHours(24));
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

    public async Task CreateLavaTopPaymentAsync(
        long userId, long telegramChatId, int gemsCount, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "CreateLavaTop: start userId={UserId} gems={Gems} enabled={Enabled}",
            userId, gemsCount, LavaTopConfig.IsEnabled);

        if (!LavaTopConfig.IsEnabled)
        {
            _logger.LogWarning("CreateLavaTop: not configured, ignoring");
            return;
        }

        var checkout = await CreateLavaTopCheckoutAsync(userId, gemsCount, returnUrl: null, cancellationToken);
        await SendLavaTopPaymentLinkAsync(
            telegramChatId,
            gemsCount,
            checkout.Amount,
            checkout.Currency,
            checkout.PaymentUrl,
            checkout.InvoiceId,
            cancellationToken);
        _logger.LogInformation(
            "CreateLavaTop: link sent to userId={UserId} invoiceId={InvoiceId} contractId={ContractId}",
            userId,
            checkout.InvoiceId,
            checkout.ExternalPaymentId);
    }

    /// <summary>
    /// Creates a lava.top invoice against the single dynamic-price offer and returns the payment widget
    /// URL (shared by bot + web).
    /// </summary>
    public async Task<LavaTopCheckoutResult> CreateLavaTopCheckoutAsync(
        long userId, int gemsCount, string? returnUrl, CancellationToken cancellationToken)
    {
        if (!LavaTopConfig.IsEnabled)
        {
            throw new InvalidOperationException("lava.top is not configured");
        }

        var amount = ToLavaTopAmount(gemsCount);
        if (gemsCount <= 0 || !IsLavaTopAmountAllowed(gemsCount))
        {
            throw new InvalidOperationException(
                $"lava.top top-up must be between {LavaTopConfig.MinRechargeAmount} and " +
                $"{LavaTopConfig.MaxRechargeAmount} {LavaTopConfig.Currency}");
        }

        var currency = LavaTopConfig.Currency;
        var user = _userRepository.Get(userId);
        Guard.Against.Null(user);
        var email = ResolveLavaTopEmail(user);

        var invoice = new Invoice
        {
            CreatedAt = DateTime.UtcNow,
            UserId = userId,
            Amount = gemsCount,
            Currency = currency,
            Status = InvoiceStatus.Created,
            Provider = PaymentProvider.LavaTop,
            FiatAmountKopecks = (long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero)
        };
        _invoiceRepository.Create(invoice);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "CreateLavaTop: invoice created id={InvoiceId} amount={Amount} {Currency}",
            invoice.Id,
            amount,
            currency);

        var effectiveReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? LavaTopConfig.ReturnUrl : returnUrl.Trim();

        var payment = await _lavaTopClient.CreateInvoiceAsync(
            new CreateLavaTopInvoiceRequest
            {
                Email = email,
                OfferId = LavaTopConfig.OfferId!,
                Currency = currency,
                Amount = amount,
                SuccessfulReturnUrl = effectiveReturnUrl,
                FailureReturnUrl = effectiveReturnUrl,
                CancelReturnUrl = effectiveReturnUrl
            },
            cancellationToken);

        _logger.LogInformation(
            "CreateLavaTop: API contract id={ContractId} status={Status} paymentUrl={HasUrl}",
            payment.Id,
            payment.Status,
            !string.IsNullOrWhiteSpace(payment.PaymentUrl));

        if (string.IsNullOrWhiteSpace(payment.PaymentUrl))
        {
            invoice.Status = InvoiceStatus.Failed;
            await _context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("lava.top did not return a paymentUrl");
        }

        invoice.ExternalPaymentId = payment.Id;
        await _context.Invoices
            .Where(i => i.Id == invoice.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(i => i.ExternalPaymentId, payment.Id),
                cancellationToken);

        return new LavaTopCheckoutResult(
            invoice.Id,
            payment.PaymentUrl,
            gemsCount,
            FormatLavaTopAmount(gemsCount),
            currency,
            payment.Id);
    }

    private async Task SendLavaTopPaymentLinkAsync(
        long telegramChatId,
        int gemsCount,
        string amount,
        string currency,
        string paymentUrl,
        long invoiceId,
        CancellationToken cancellationToken)
    {
        var rows = new List<IEnumerable<InlineKeyboardButton>>
        {
            new[]
            {
                InlineKeyboardButton.WithUrl(BotResponse.OpenLavaTopPaymentButton, paymentUrl)
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    BotResponse.CheckLavaTopPaymentButton,
                    $"{PaymentCallbacks.LavaTopCheckPrefix}{invoiceId}")
            }
        };

        await _botClient.SendMessage(
            telegramChatId,
            string.Format(BotResponse.LavaTopPaymentLinkResponse, gemsCount, amount, currency),
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: cancellationToken);
    }

    /// <summary>Poll lava.top for an invoice (manual button / background sync when the webhook is missed).</summary>
    public async Task<PaymentConfirmResult> SyncLavaTopInvoiceAsync(
        long invoiceId, long userId, CancellationToken cancellationToken)
    {
        var invoice = _invoiceRepository.GetById(invoiceId);
        if (invoice == null || invoice.Provider != PaymentProvider.LavaTop || invoice.UserId != userId)
        {
            return PaymentConfirmResult.Ignored;
        }

        if (invoice.Status == InvoiceStatus.Paid)
        {
            return PaymentConfirmResult.DepositCredited;
        }

        if (string.IsNullOrWhiteSpace(invoice.ExternalPaymentId))
        {
            return PaymentConfirmResult.Ignored;
        }

        return await ConfirmLavaTopPaymentAsync(invoice.ExternalPaymentId, cancellationToken);
    }

    /// <summary>Background catch-up for Created lava.top invoices with a known contract id.</summary>
    public async Task<int> SyncPendingLavaTopPaymentsAsync(CancellationToken cancellationToken)
    {
        if (!LavaTopConfig.IsEnabled)
        {
            return 0;
        }

        var pending = _invoiceRepository.GetPending(PaymentProvider.LavaTop, TimeSpan.FromHours(24));
        var credited = 0;
        foreach (var invoice in pending)
        {
            if (string.IsNullOrWhiteSpace(invoice.ExternalPaymentId))
            {
                continue;
            }

            try
            {
                var result = await ConfirmLavaTopPaymentAsync(invoice.ExternalPaymentId, cancellationToken);
                if (result == PaymentConfirmResult.DepositCredited)
                {
                    credited++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "SyncLavaTop: failed for invoice {InvoiceId} contract {ContractId}",
                    invoice.Id,
                    invoice.ExternalPaymentId);
            }
        }

        return credited;
    }

    /// <summary>
    /// Re-fetches the contract from lava.top — never trusts a caller-supplied amount/status, since
    /// lava.top's own docs warn the return-URL query string is forgeable — and on a genuinely completed
    /// payment atomically credits the wallet exactly once.
    /// </summary>
    public async Task<PaymentConfirmResult> ConfirmLavaTopPaymentAsync(
        string contractId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(contractId))
        {
            return PaymentConfirmResult.Ignored;
        }

        var details = await _lavaTopClient.GetInvoiceAsync(contractId, cancellationToken);
        if (!string.Equals(details.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "ConfirmLavaTop: contract {ContractId} not completed (status={Status})",
                contractId,
                details.Status);
            return PaymentConfirmResult.Ignored;
        }

        var invoice = _invoiceRepository.GetByExternalPaymentId(contractId);
        if (invoice == null || invoice.Provider != PaymentProvider.LavaTop)
        {
            _logger.LogWarning("ConfirmLavaTop: no matching invoice for contract {ContractId}", contractId);
            return PaymentConfirmResult.Ignored;
        }

        if (invoice.Status == InvoiceStatus.Paid)
        {
            return PaymentConfirmResult.DepositCredited;
        }

        if (invoice.Status != InvoiceStatus.Created)
        {
            return PaymentConfirmResult.Ignored;
        }

        if (invoice.FiatAmountKopecks is long expectedMinorUnits)
        {
            var paidMinorUnits = (long)decimal.Round(
                (details.Receipt?.Amount ?? 0m) * 100m, 0, MidpointRounding.AwayFromZero);
            var paidCurrency = details.Receipt?.Currency;

            if (paidMinorUnits != expectedMinorUnits ||
                !string.Equals(paidCurrency, invoice.Currency, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "ConfirmLavaTop: amount mismatch contract={ContractId} invoice={InvoiceId} expected={Expected} paid={Paid} {Currency}",
                    contractId,
                    invoice.Id,
                    expectedMinorUnits,
                    paidMinorUnits,
                    paidCurrency);
                return PaymentConfirmResult.Ignored;
            }
        }

        var claimed = await _context.Invoices
            .Where(i => i.Id == invoice.Id && i.Status == InvoiceStatus.Created)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(i => i.Status, InvoiceStatus.Paid),
                cancellationToken);

        if (claimed == 0)
        {
            return PaymentConfirmResult.DepositCredited;
        }

        await AddMoneyAsync(invoice.UserId, (int)invoice.Amount, PaymentProvider.LavaTop, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var profile = await _userService.GetUserProfile(invoice.UserId);
            var reply = profile.Render();
            var replyMarkup = new InlineKeyboardMarkup(InlineKeyboardButton
                .WithCallbackData(BotResponse.AddMoneyResponse, BotMenu.DepositCommand));

            var telegramChatId = ResolveTelegramChatId(invoice.UserId);
            if (telegramChatId is not null)
            {
                await _botClient.SendMessage(telegramChatId.Value,
                    BotResponse.LavaTopPaymentSucceeded + Environment.NewLine + Environment.NewLine + reply,
                    replyMarkup: replyMarkup,
                    cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify user {UserId} about lava.top payment", invoice.UserId);
        }

        return PaymentConfirmResult.DepositCredited;
    }

    /// <summary>Dispatches to the right rail's sync — backs the one generic web /sync route.</summary>
    public async Task<PaymentConfirmResult> SyncInvoiceAsync(
        long invoiceId, long userId, CancellationToken cancellationToken)
    {
        var invoice = _invoiceRepository.GetById(invoiceId);
        if (invoice == null || invoice.UserId != userId)
        {
            return PaymentConfirmResult.Ignored;
        }

        return invoice.Provider switch
        {
            PaymentProvider.YooKassa => await SyncYooKassaInvoiceAsync(invoiceId, userId, cancellationToken),
            PaymentProvider.LavaTop => await SyncLavaTopInvoiceAsync(invoiceId, userId, cancellationToken),
            _ => PaymentConfirmResult.Ignored
        };
    }

    /// <summary>
    /// lava.top requires an email for the receipt. Telegram users rarely have one on file, so a stable
    /// per-user placeholder under the reserved .invalid TLD (RFC 2606 — guaranteed to never resolve or
    /// receive mail) stands in; a real confirmed address is preferred when the account has one.
    /// </summary>
    private static string ResolveLavaTopEmail(User user) =>
        !string.IsNullOrWhiteSpace(user.Email) ? user.Email! : $"u{user.Id}@telegram.invalid";

    public bool TryValidatePreCheckout(PreCheckoutQuery query, out string? errorMessage)
    {
        errorMessage = null;

        if (!TryParseInvoicePayload(query.InvoicePayload, out var invoiceId, out var isDonate))
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
            var payer = _userRepository.GetByTelegramId(query.From.Id);
            if (payer is null || invoice.UserId != payer.Id)
            {
                errorMessage = "Invoice user mismatch";
                return false;
            }
        }

        if (ExpectedXtr(invoice, isDonate) != query.TotalAmount || query.Currency != Currency.Stars)
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
            ExpectedXtr(invoice, isDonate) != payment.TotalAmount ||
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

        // payment.TotalAmount is XTR; the wallet is credited the gems the invoice was raised for.
        await AddMoneyAsync(userId, (int)invoice.Amount, PaymentProvider.TelegramStars, cancellationToken);
        return PaymentConfirmResult.DepositCredited;
    }

    private Invoice? FindInvoiceForPayment(string paymentId, YooKassaPayment payment)
    {
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

        return invoice;
    }

    /// <summary>
    /// Moves a still-Created invoice to Failed once YooKassa reports the payment as canceled,
    /// so the background poller stops asking about it.
    /// </summary>
    private async Task CloseCanceledYooKassaInvoiceAsync(
        string paymentId,
        YooKassaPayment payment,
        CancellationToken cancellationToken)
    {
        var invoice = FindInvoiceForPayment(paymentId, payment);
        if (invoice == null || invoice.Provider != PaymentProvider.YooKassa)
        {
            _logger.LogInformation(
                "ConfirmYooKassa: payment {PaymentId} canceled, no matching YooKassa invoice → Ignored",
                paymentId);
            return;
        }

        if (invoice.Status != InvoiceStatus.Created)
        {
            return;
        }

        await _context.Invoices
            .Where(i => i.Id == invoice.Id && i.Status == InvoiceStatus.Created)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(i => i.Status, InvoiceStatus.Failed),
                cancellationToken);

        _logger.LogInformation(
            "ConfirmYooKassa: payment {PaymentId} canceled → invoice {InvoiceId} Created→Failed",
            paymentId,
            invoice.Id);
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
            // "canceled" is terminal on YooKassa's side. Unless the invoice is closed locally too,
            // SyncPendingYooKassaPaymentsAsync keeps re-polling it on every tick for the next 24 hours.
            if (string.Equals(payment.Status, "canceled", StringComparison.OrdinalIgnoreCase))
            {
                await CloseCanceledYooKassaInvoiceAsync(paymentId, payment, cancellationToken);
            }
            else
            {
                _logger.LogInformation(
                    "ConfirmYooKassa: payment not succeeded/paid → Ignored (status={Status}, paid={Paid})",
                    payment.Status,
                    payment.Paid);
            }

            return PaymentConfirmResult.Ignored;
        }

        var invoice = FindInvoiceForPayment(paymentId, payment);

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
            "ConfirmYooKassa: crediting userId={UserId} gems={Gems}",
            invoice.UserId,
            invoice.Amount);
        await AddMoneyAsync(invoice.UserId, (int)invoice.Amount, PaymentProvider.YooKassa, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("ConfirmYooKassa: SaveChanges done for invoice {InvoiceId}", invoice.Id);

        try
        {
            var profile = await _userService.GetUserProfile(invoice.UserId);
            var reply = profile.Render();
            var replyMarkup = new InlineKeyboardMarkup(InlineKeyboardButton
                .WithCallbackData(BotResponse.AddMoneyResponse, BotMenu.DepositCommand));

            var telegramChatId = ResolveTelegramChatId(invoice.UserId);
            if (telegramChatId is null)
            {
                _logger.LogWarning(
                    "ConfirmYooKassa: no TelegramId for user {UserId}, skip notify",
                    invoice.UserId);
            }
            else
            {
                await _botClient.SendMessage(telegramChatId.Value,
                    BotResponse.YooKassaPaymentSucceeded + Environment.NewLine + Environment.NewLine + reply,
                    replyMarkup: replyMarkup,
                    cancellationToken: cancellationToken);
                _logger.LogInformation("ConfirmYooKassa: user {UserId} notified", invoice.UserId);
            }
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

    /// <summary>Credits <paramref name="gems"/> to the wallet, whichever rail the payer used.</summary>
    public async Task AddMoneyAsync(long userId, int gems, PaymentProvider provider, CancellationToken cancellationToken)
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
                Currency = CurrencyCode.Gem
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
            Amount = gems
        });
        await _walletRepository.UpdateAmountAsync(wallet, gems);
        NotifyAdminAboutDeposit(wallet, provider);
    }

    /// <summary>
    /// XTR Telegram is billed for an invoice. A deposit stores gems in
    /// <see cref="Invoice.Amount"/> and is charged through <see cref="TelegramStarsConfig.GemsToXtr"/>;
    /// a donation stores XTR directly.
    /// </summary>
    private static long ExpectedXtr(Invoice invoice, bool isDonate) =>
        isDonate ? invoice.Amount : TelegramStarsConfig.GemsToXtr(invoice.Amount);

    /// <summary>Fiat price of a gem amount, in kopecks. At the default rate a gem is one kopeck.</summary>
    public static long ToKopecks(int gemsCount)
    {
        var rub = gemsCount / (decimal)YooKassaConfig.GemsPerRub;
        return (long)decimal.Round(rub * 100m, 0, MidpointRounding.AwayFromZero);
    }

    public static string FormatRubAmount(int gemsCount) =>
        (ToKopecks(gemsCount) / 100m).ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// Price of a gem amount in <see cref="LavaTopConfig.Currency"/> units. Returns
    /// <see cref="decimal.MaxValue"/> when the rate isn't configured, so an unpriced rail never looks
    /// affordable to a min-amount check instead of throwing mid-comparison.
    /// </summary>
    public static decimal ToLavaTopAmount(int gemsCount)
    {
        var perUnit = LavaTopConfig.GemsPerUnit;
        return perUnit > 0
            ? decimal.Round(gemsCount / (decimal)perUnit, 2, MidpointRounding.AwayFromZero)
            : decimal.MaxValue;
    }

    public static string FormatLavaTopAmount(int gemsCount) =>
        ToLavaTopAmount(gemsCount).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>
    /// Both bounds lava.top itself enforces server-side on <c>amount</c> (confirmed live: a USD invoice
    /// below $5 or above $10000 is rejected by their API, not just a convention of ours) — checked here
    /// once so every caller (button gating, callback parsing, checkout) fails the same clean way instead
    /// of surfacing lava.top's raw error.
    /// </summary>
    public static bool IsLavaTopAmountAllowed(int gemsCount)
    {
        var amount = ToLavaTopAmount(gemsCount);
        return amount >= LavaTopConfig.MinRechargeAmount && amount <= LavaTopConfig.MaxRechargeAmount;
    }

    private long? ResolveTelegramChatId(long userId) =>
        _userRepository.Get(userId)?.TelegramId;

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

    private void NotifyAdminAboutDeposit(Wallet wallet, PaymentProvider provider)
    {
        var method = FormatPaymentMethod(provider);
        var message = "#deposit" + Environment.NewLine
            + $"{wallet.UserId} balance: {wallet.Balance} gems" + Environment.NewLine
            + $"method: {method}";

        _botClient.SendMessage(AppConfig.AdminIds.First(), message);
    }

    private static string FormatPaymentMethod(PaymentProvider provider) =>
        provider switch
        {
            PaymentProvider.YooKassa => "YooKassa (карта / СБП / SberPay)",
            PaymentProvider.LavaTop => "lava.top (международная карта / PayPal)",
            PaymentProvider.TelegramStars => "Telegram Stars",
            _ => provider.ToString()
        };
}
