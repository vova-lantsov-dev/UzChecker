using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using UzChecker.AppHost.Helpers;
using UzChecker.AppHost.Options;

namespace UzChecker.AppHost.Services;

internal sealed class DefaultTelegramNotifier : ITelegramNotifier
{
    private readonly ITelegramBotClient _botClient;
    private readonly TelegramOptions _telegramOptions;

    private static readonly CultureInfo Culture = new("uk-UA");
    private static readonly TimeZoneInfo UkrainianTimeZone = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");

    public DefaultTelegramNotifier(ITelegramBotClient botClient, IOptions<TelegramOptions> telegramOptions)
    {
        _botClient = botClient;
        _telegramOptions = telegramOptions.Value;
    }

    private static string BuildStatusMessage(IEnumerable<UzSubscription> subscriptions, bool includeLastUpdate = false)
    {
        var sb = new StringBuilder()
            .Append("Бот запущено\n\n")
            .Append("Підписки:\n")
            .AppendJoin('\n', subscriptions
                .SelectMany(s => s.Dates, (s, d) => new { Subscription = s, Date = d })
                .OrderBy(it => it.Date.Date)
                .Select((item, ind) =>
                {
                    var s = item.Subscription;
                    var date = DateTime.Parse(item.Date.Date);

                    return $"{ind + 1}. {date.ToString("d", Culture)} / {s.StationFrom} - {s.StationTo} / {string.Join(", ", s.TrainTypes)}";
                }));

        if (includeLastUpdate)
        {
            sb.Append("\n\n")
              .Append("Останнє оновлення: ")
              .Append(TimeZoneInfo.ConvertTime(DateTime.UtcNow, UkrainianTimeZone).ToString("G", Culture));
        }

        return sb.ToString();
    }

    public async Task<Message> SendInitialMessageAsync(IEnumerable<UzSubscription> subscriptions,
        CancellationToken cancellationToken)
    {
        var statusText = BuildStatusMessage(subscriptions);

        var statusMessage = await _botClient.SendMessage(
            _telegramOptions.RecipientId,
            statusText,
            disableNotification: true,
            cancellationToken: cancellationToken);

        try
        {
            await _botClient.PinChatMessage(_telegramOptions.RecipientId,
                statusMessage.MessageId,
                disableNotification: true,
                cancellationToken: cancellationToken);
        }
        catch
        {
            // silent
        }

        return statusMessage;
    }

    public async Task EditStatusMessageAsync(int statusMessageId, IEnumerable<UzSubscription> subscriptions, CancellationToken cancellationToken)
    {
        var statusText = BuildStatusMessage(subscriptions, includeLastUpdate: true);

        await _botClient.EditMessageText(
            _telegramOptions.RecipientId,
            statusMessageId,
            statusText,
            cancellationToken: cancellationToken
        );
    }

    public async Task NotifyAsync(UzSubscriptionContext context, CancellationToken stoppingToken)
    {
        throw new NotImplementedException();
    }
}