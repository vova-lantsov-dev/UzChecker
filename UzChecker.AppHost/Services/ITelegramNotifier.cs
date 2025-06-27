using Telegram.Bot.Types;
using UzChecker.AppHost.Helpers;
using UzChecker.AppHost.Options;

namespace UzChecker.AppHost.Services;

public interface ITelegramNotifier
{
    Task<Message> SendInitialMessageAsync(IEnumerable<UzSubscription> subscriptions,
        CancellationToken cancellationToken);

    Task EditStatusMessageAsync(int statusMessageId, IEnumerable<UzSubscription> subscriptions,
        CancellationToken cancellationToken);

    Task NotifyAsync(UzSubscriptionContext context, CancellationToken stoppingToken);
}