using System.ComponentModel.DataAnnotations;

namespace UzChecker.AppHost.Options;

public sealed class TelegramOptions
{
    [Required]
    public string BotToken { get; set; }
    [Required]
    public long RecipientId { get; set; }
}