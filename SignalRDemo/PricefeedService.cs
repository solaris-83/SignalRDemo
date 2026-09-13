using Microsoft.AspNetCore.SignalR;

namespace SignalRDemo
{
    /// <summary>
    /// Servizio in background che, indipendentemente da qualunque richiesta client,
    /// aggiorna periodicamente i prezzi e li spinge (push) ai client tramite IHubContext.
    /// Questo dimostra il fondamentale "server-initiated broadcast": SignalR non serve
    /// solo per rispondere a chiamate client, ma anche per inviare eventi spontanei.
    /// </summary>
    public class PriceFeedService : BackgroundService
    {
        private readonly IHubContext<MarketHub> _hubContext;
        private readonly MarketState _state;
        private static readonly Random _rng = new();

        public PriceFeedService(IHubContext<MarketHub> hubContext, MarketState state)
        {
            _hubContext = hubContext;
            _state = state;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

                foreach (var ticker in _state.Prices.Keys.ToList())
                {
                    var current = _state.Prices[ticker];
                    // Random walk semplice: +/- fino allo 0.4%
                    var change = current * (decimal)(_rng.NextDouble() * 0.008 - 0.004);
                    var updated = Math.Max(0.01m, current + change);
                    _state.Prices[ticker] = updated;

                    // Push solo a chi è iscritto a QUEL ticker: broadcast selettivo via gruppo.
                    await _hubContext.Clients.Group(ticker).SendAsync("PriceTick", ticker, updated, stoppingToken);
                }

                // Snapshot completo a tutti (anche a chi non è iscritto a nessun gruppo),
                // utile per una ticker-tape generale in home page.
                await _hubContext.Clients.All.SendAsync("MarketSnapshot", _state.Prices, stoppingToken);
            }
        }
    }
}
