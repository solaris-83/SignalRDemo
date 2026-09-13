using SignalRDemo;

var builder = WebApplication.CreateBuilder(args);

// Stato di mercato condiviso, singleton per tutta l'app.
builder.Services.AddSingleton<MarketState>();

builder.Services.AddSignalR();

// Il client è una pagina HTML/JS servita separatamente (o con file://),
// quindi serve CORS esplicito con credenziali abilitate per SignalR.
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientPolicy", policy =>
    {
        policy
            .SetIsOriginAllowed(_ => true) // demo: in produzione whitelistare i domini
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddHostedService<PriceFeedService>();

var app = builder.Build();

app.UseCors("ClientPolicy");

app.MapGet("/", () => "TradingHub server attivo. Endpoint SignalR: /hubs/market");

app.MapHub<MarketHub>("/hubs/market");

app.Run();