using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Telegram.Bot;
using UzChecker.AppHost.Options;
using UzChecker.AppHost.Services;
using UzChecker.Data;

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Headless = true
});

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<TelegramOptions>()
    .BindConfiguration("TelegramOptions")
    .ValidateDataAnnotations();

builder.Services.AddOptions<UzOptions>()
    .BindConfiguration("UzOptions")
    .ValidateDataAnnotations();

builder.Services.AddOptions<WorkerOptions>()
    .BindConfiguration("WorkerOptions")
    .ValidateDataAnnotations();

builder.Services.AddDbContext<UzCheckerContext>(opts => opts.UseInMemoryDatabase("UzCheckerDb"));

builder.Services.AddSingleton<ITelegramBotClient>(provider =>
{
    var telegramOptions = provider.GetRequiredService<IOptions<TelegramOptions>>().Value;
    return new TelegramBotClient(telegramOptions.BotToken);
});

builder.Services.AddLogging();

builder.Services.AddSingleton(playwright);
builder.Services.AddSingleton(browser);

builder.Services.AddHostedService<WorkerService>();

var app = builder.Build();

await app.RunAsync();