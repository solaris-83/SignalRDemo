using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace SignalRDemo
{
    /// <summary>
    /// Rappresenta un ordine simulato inviato da un client.
    /// </summary>
    public record OrderRequest(string Ticker, string Side, int Quantity);

    /// <summary>
    /// Stato condiviso in-memory: prezzi correnti e utenti connessi per ticker.
    /// In un progetto reale andrebbe in un servizio separato con persistenza,
    /// ma qui resta semplice per concentrarsi sui fondamentali di SignalR.
    /// </summary>
    public class MarketState
    {
        public ConcurrentDictionary<string, decimal> Prices { get; } = new()
        {
            ["AAPL"] = 227.50m,
            ["MSFT"] = 418.20m,
            ["GOOG"] = 175.90m,
            ["TSLA"] = 245.10m,
        };

        // ticker -> insieme di (connectionId, userName)
        public ConcurrentDictionary<string, ConcurrentDictionary<string, string>> Subscribers { get; } = new();

        // connectionId -> userName, per sapere chi è connesso globalmente
        public ConcurrentDictionary<string, string> ConnectedUsers { get; } = new();
    }

    public class MarketHub : Hub
    {
        private readonly MarketState _state;
        private static readonly Random _rng = new();

        public MarketHub(MarketState state)
        {
            _state = state;
        }

        // ---------- Ciclo di vita della connessione ----------

        public override async Task OnConnectedAsync()
        {
            var userName = Context.GetHttpContext()?.Request.Query["user"].ToString();
            if (string.IsNullOrWhiteSpace(userName))
                userName = $"Guest-{Context.ConnectionId[..6]}";

            _state.ConnectedUsers[Context.ConnectionId] = userName;

            // Invia lo snapshot iniziale SOLO al chiamante appena connesso.
            await Clients.Caller.SendAsync("Snapshot", _state.Prices);

            // Notifica tutti gli altri che un nuovo utente è online.
            await Clients.Others.SendAsync("UserPresence", userName, true, _state.ConnectedUsers.Count);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _state.ConnectedUsers.TryRemove(Context.ConnectionId, out var userName);

            // Rimuovi l'utente da tutti i gruppi ticker a cui era iscritto.
            foreach (var kvp in _state.Subscribers)
            {
                kvp.Value.TryRemove(Context.ConnectionId, out _);
            }

            if (userName != null)
            {
                await Clients.Others.SendAsync("UserPresence", userName, false, _state.ConnectedUsers.Count);
            }

            await base.OnDisconnectedAsync(exception);
        }

        // ---------- Iscrizione a gruppi (uno per ticker) ----------

        public async Task Subscribe(string ticker)
        {
            ticker = ticker.ToUpperInvariant();
            if (!_state.Prices.ContainsKey(ticker))
            {
                // Invocazione mirata al solo chiamante per segnalare un errore applicativo.
                await Clients.Caller.SendAsync("ErrorMessage", $"Ticker sconosciuto: {ticker}");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, ticker);

            var userName = _state.ConnectedUsers.GetValueOrDefault(Context.ConnectionId, "??");
            var group = _state.Subscribers.GetOrAdd(ticker, _ => new ConcurrentDictionary<string, string>());
            group[Context.ConnectionId] = userName;

            // Conferma solo al chiamante...
            await Clients.Caller.SendAsync("Subscribed", ticker, _state.Prices[ticker]);

            // ...e avvisa tutti gli altri iscritti allo stesso ticker (Groups = broadcast selettivo).
            await Clients.OthersInGroup(ticker).SendAsync("RoomActivity", ticker, $"{userName} si è unito alla stanza {ticker}");
        }

        public async Task Unsubscribe(string ticker)
        {
            ticker = ticker.ToUpperInvariant();
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, ticker);

            if (_state.Subscribers.TryGetValue(ticker, out var group))
            {
                group.TryRemove(Context.ConnectionId, out var userName);
                await Clients.Group(ticker).SendAsync("RoomActivity", ticker, $"{userName ?? "Un utente"} ha lasciato la stanza {ticker}");
            }
        }

        // ---------- Invocazione client -> server: piazzare un ordine ----------

        public async Task PlaceOrder(OrderRequest order)
        {
            var ticker = order.Ticker.ToUpperInvariant();

            if (!_state.Prices.TryGetValue(ticker, out var price))
            {
                await Clients.Caller.SendAsync("OrderRejected", order, "Ticker inesistente");
                return;
            }

            if (order.Quantity <= 0)
            {
                await Clients.Caller.SendAsync("OrderRejected", order, "Quantità non valida");
                return;
            }

            var userName = _state.ConnectedUsers.GetValueOrDefault(Context.ConnectionId, "??");

            // "Esecuzione" simulata: l'ordine muove leggermente il prezzo.
            var impact = (order.Side.Equals("BUY", StringComparison.OrdinalIgnoreCase) ? 1 : -1)
                         * order.Quantity * 0.01m;
            var newPrice = Math.Max(0.01m, price + impact);
            _state.Prices[ticker] = newPrice;

            var fill = new
            {
                order.Ticker,
                order.Side,
                order.Quantity,
                FillPrice = price,
                User = userName,
                Timestamp = DateTimeOffset.UtcNow
            };

            // Conferma al chiamante che l'ordine è stato eseguito.
            await Clients.Caller.SendAsync("OrderFilled", fill);

            // Tutti gli iscritti a quel ticker vedono il trade e il nuovo prezzo (broadcast di gruppo).
            await Clients.Group(ticker).SendAsync("TradeExecuted", fill, newPrice);
        }

        // ---------- Chat rapida per stanza (invocazione + broadcast di gruppo) ----------

        public async Task SendRoomMessage(string ticker, string message)
        {
            ticker = ticker.ToUpperInvariant();
            var userName = _state.ConnectedUsers.GetValueOrDefault(Context.ConnectionId, "??");
            await Clients.Group(ticker).SendAsync("RoomMessage", ticker, userName, message);
        }
    }
}
