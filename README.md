# TradingHub — Dashboard di trading simulato in tempo reale

Progetto dimostrativo che usa **SignalR** in modo non banale: non solo un
esempio di chat, ma un mini-sistema con stanze per ticker, ordini simulati,
presenza utenti e un feed di prezzi generato lato server in background.

## Struttura

```
TradingHub/
├── Server/                     # Backend ASP.NET Core
│   ├── TradingHub.csproj
│   ├── Program.cs
│   ├── Hubs/MarketHub.cs       # Hub SignalR: gruppi, invocazioni, stato connessione
│   └── Services/PriceFeedService.cs  # BackgroundService che spinge i prezzi
└── Client/
    └── index.html              # Client HTML/JS puro (nessun framework)
```

## Fondamentali di SignalR utilizzati

| Fondamentale | Dove |
|---|---|
| Hub e invocazione client → server | `PlaceOrder`, `Subscribe`, `SendRoomMessage` in `MarketHub.cs` |
| Invocazione server → client (singolo client) | `Clients.Caller.SendAsync(...)` per conferme/errori personali |
| Broadcast a tutti | `Clients.All.SendAsync("MarketSnapshot", ...)` |
| **Gruppi** (broadcast selettivo) | `Groups.AddToGroupAsync` / `Clients.Group(ticker)` — ogni ticker è una "stanza" |
| Escludere il chiamante | `Clients.Others` e `Clients.OthersInGroup(ticker)` |
| Push spontaneo dal server (non in risposta a una richiesta) | `PriceFeedService` (un `BackgroundService`) che usa `IHubContext<MarketHub>` |
| Ciclo di vita della connessione | `OnConnectedAsync` / `OnDisconnectedAsync` per gestire presenza utenti |
| Riconnessione automatica lato client | `withAutomaticReconnect([...])` + `onreconnecting` / `onreconnected` / `onclose` |
| Ripristino stato dopo riconnessione | In `onreconnected`, il client si re-iscrive al gruppo, perché una riconnessione genera un nuovo `ConnectionId` e il server perde l'appartenenza al gruppo precedente |
| Passaggio di dati custom in connessione | Query string `?user=...` letta in `OnConnectedAsync` tramite `Context.GetHttpContext()` |
| CORS con credenziali per client separato | `AllowCredentials()` in `Program.cs`, necessario perché SignalR usa cookie/negotiate |

## Tutte le comunicazioni client ↔ server

Ci sono due canali distinti che vanno tenuti mentalmente separati:

- **Invocazioni (client → server)**: il client chiama `connection.invoke("Metodo", ...)`,
  che esegue un metodo pubblico dell'hub `MarketHub`. Sono richieste esplicite.
- **Eventi (server → client)**: il server chiama `Clients.XXX.SendAsync("NomeEvento", ...)`,
  che scatena l'handler registrato con `connection.on("NomeEvento", ...)` sul client.
  Sono push, non richiesti da una risposta diretta ma "spinti" dal server quando vuole.

Un singolo `invoke` può generare **zero, uno o più** eventi di ritorno, anche verso
client diversi da chi ha invocato. Le due tabelle seguenti elencano tutto.

### A. Invocazioni: client → server

| Metodo hub | Payload inviato | Chi lo chiama e quando |
|---|---|---|
| `Subscribe(ticker)` | stringa ticker (es. `"AAPL"`) | Il client lo invoca ogni volta che l'utente seleziona un ticker dalla watchlist |
| `Unsubscribe(ticker)` | stringa ticker | Il client lo invoca automaticamente prima di iscriversi a un nuovo ticker, per lasciare quello precedente |
| `PlaceOrder(order)` | oggetto `{ ticker, side, quantity }` | Quando l'utente clicca "Compra" o "Vendi" |
| `SendRoomMessage(ticker, message)` | stringa ticker + stringa messaggio | Quando l'utente invia un messaggio in chat (Invio o click su "Invia") |

Nessuna di queste invocazioni restituisce un valore diretto (non usano `return`
sul lato server né `.then()` con dato utile sul client, a parte eventuali
eccezioni): l'esito arriva sempre tramite uno o più eventi asincroni (tabella B).

### B. Eventi: server → client

| Evento | Payload | Chi lo riceve | Generato da |
|---|---|---|---|
| `Snapshot` | dizionario `{ ticker: prezzo }` completo | solo il chiamante (`Clients.Caller`) | `OnConnectedAsync`, appena la connessione si apre |
| `UserPresence` | `(nome, online, conteggioTotale)` | tutti tranne il chiamante (`Clients.Others`) | `OnConnectedAsync` (online=true) e `OnDisconnectedAsync` (online=false) |
| `Subscribed` | `(ticker, prezzoCorrente)` | solo il chiamante | `Subscribe`, in caso di successo |
| `ErrorMessage` | stringa messaggio | solo il chiamante | `Subscribe`, se il ticker non esiste |
| `RoomActivity` | `(ticker, messaggioTestuale)` | tutti gli altri iscritti allo stesso ticker (`Clients.OthersInGroup`) | `Subscribe` (ingresso) e `Unsubscribe`/disconnessione (uscita) |
| `OrderFilled` | oggetto fill `{ Ticker, Side, Quantity, FillPrice, User, Timestamp }` | solo il chiamante | `PlaceOrder`, in caso di successo |
| `OrderRejected` | `(order, motivo)` | solo il chiamante | `PlaceOrder`, se ticker inesistente o quantità non valida |
| `TradeExecuted` | `(fill, nuovoPrezzo)` | tutti gli iscritti al ticker, incluso il chiamante (`Clients.Group`) | `PlaceOrder`, in caso di successo — è l'effetto "pubblico" dell'ordine |
| `RoomMessage` | `(ticker, autore, messaggio)` | tutti gli iscritti al ticker, incluso il chiamante (`Clients.Group`) | `SendRoomMessage` |
| `PriceTick` | `(ticker, nuovoPrezzo)` | solo gli iscritti a quel ticker (`Clients.Group`) | `PriceFeedService`, ogni 2 secondi, uno per ticker con variazione |
| `MarketSnapshot` | dizionario `{ ticker: prezzo }` completo | tutti i client connessi (`Clients.All`) | `PriceFeedService`, ogni 2 secondi, dopo aver aggiornato tutti i tick |

Da notare due asimmetrie importanti:

- `PlaceOrder` genera **due** eventi distinti per due platee diverse:
  `OrderFilled` (privato, conferma personale con dettagli di esecuzione) e
  `TradeExecuted` (pubblico al gruppo, per aggiornare gli altri iscritti allo
  stesso ticker). Il chiamante riceve entrambi.
- `PriceFeedService` non è mai innescato da un'invocazione client: parte da
  solo ogni 2 secondi ed è l'unico produttore di `PriceTick` e `MarketSnapshot`.
  È l'esempio di comunicazione "spontanea" server → client, senza alcuna
  richiesta pregressa.

### C. Ordine temporale delle comunicazioni

Ordine con cui gli eventi si presentano realisticamente, dall'apertura pagina
in poi:

1. **Apertura pagina / submit nome utente**
   Il client chiama `connection.start()` con `?user=<nome>` in query string.

2. **Handshake di connessione**
   - Il server esegue `OnConnectedAsync`.
   - Il server invia `Snapshot` al solo chiamante (prezzi correnti di tutti i ticker).
   - Il server invia `UserPresence(nome, true, count)` a tutti gli altri già connessi.
   - Il client riceve `Snapshot` → popola la watchlist con i prezzi iniziali.

2bis. **Handshake di connessione**
   - Il server esegue `OnConnectedAsync`.
   - Il server invia `Acknowledged` al solo chiamante (banalmente TRUE).
   - Il server invia `UserPresence(nome, true, count)` a tutti gli altri già connessi.
   - Il client riceve `Acknowledged` → non fa nulla di particolare.

   
3. **Iscrizione automatica al primo ticker**
   Il client, appena connesso, invoca `Subscribe(primoTicker)`.
   - Se valido: il server risponde `Subscribed` al chiamante e `RoomActivity`
     agli altri già iscritti a quel ticker.
   - Se non valido (raro in questo client, possibile solo modificando il codice):
     il server risponde `ErrorMessage` al chiamante.

3bis. **Iscrizione automatica alla ricezione di una Page**
   Il client, appena connesso, invoca `Subscribe("PAGE")`.
   - Se valido: il server risponde `Subscribed` al chiamante fornendo un APIKEY. Sarà il Token che il client deve usare per inviare una notifica di cambiamento al modello (BCA_NOTIFY_MODEL). Il server inserisce quel client nel gruppo individuato da quella API KEY.
   - Se non valido (raro in questo client, possibile solo modificando il codice):
     il server risponde `ErrorMessage` al chiamante.

4. **Ciclo continuo indipendente: il feed prezzi**
   Ogni 2 secondi, indipendentemente da qualunque azione utente:
   - Il server invia `PriceTick` a ciascun gruppo/ticker con variazione di prezzo.
   - Subito dopo invia `MarketSnapshot` a tutti i client connessi.
   Questo ciclo va avanti per tutta la durata della connessione, in parallelo
   a qualsiasi altra interazione.

4bis. **Invio Page dal server (SSE)** 
    - Il server invia il JSON "PAGE" assieme ad un GUID che la identifica a tutti i client connessi su quel gruppo.
    - Il client riceve il JSON e lo gestisce renderizzano la pagina.

5. **Cambio ticker (in qualsiasi momento, ripetibile)**
   - Il client invoca `Unsubscribe(vecchioTicker)` (nessun evento diretto verso
     il chiamante; solo `RoomActivity` agli altri iscritti al vecchio ticker).
   - Il client invoca `Subscribe(nuovoTicker)` → stessa sequenza del punto 3.

5bis. **Evento da TS sullo UIElement della Page**
   - Il client invoca `BCA_NOTIFY_MODEL` (con API KEY) e passa il JSON della modifica al server assieme al GUID della Page
   - Il server effettua la modifica sul modello e invia a tutti i client connessi al gruppo della Page l'evento `BCA_MODEL_UPDATED` con il JSON della modifica.

6. **Piazzamento di un ordine (in qualsiasi momento, ripetibile)**
   - Il client invoca `PlaceOrder({ ticker, side, quantity })`.
   - Il server valida: se non valido → `OrderRejected` al solo chiamante (fine).
   - Se valido → il server aggiorna il prezzo interno, poi invia, in questo
     ordine: `OrderFilled` al chiamante, quindi `TradeExecuted` a tutto il
     gruppo del ticker (chiamante incluso una seconda volta, ma con evento diverso).

6bis. **Evento dal C# per modifica modello**
   - Il server invia `BCA_NOTIFIED_MODEL` a tutti i client connessi al gruppo della Page
   - Il client effettua la modifica grafica.

7. **Messaggio in chat (in qualsiasi momento, ripetibile)**
   - Il client invoca `SendRoomMessage(ticker, testo)`.
   - Il server invia `RoomMessage` a tutto il gruppo, chiamante incluso.

8. **Disconnessione di un altro utente (asincrona, può avvenire in ogni momento)**
   - Il server esegue `OnDisconnectedAsync` per quella connessione.
   - Il server rimuove l'utente da tutti i gruppi interni.
   - Il server invia `UserPresence(nome, false, count)` a tutti gli altri client connessi.

9. **Interruzione della propria connessione (rete instabile, riavvio server, ecc.)**
   - Il client SignalR passa allo stato `reconnecting`: scatta `onreconnecting`
     lato client (nessun traffico applicativo, solo stato locale).
   - Se il client riesce a ristabilire il socket entro i tentativi configurati
     (`withAutomaticReconnect([0, 2000, 5000, 10000])`), il server tratta la
     nuova connessione come un **nuovo** `ConnectionId`: rieseguirà
     `OnConnectedAsync` da capo (nuovo `Snapshot`, nuovo `UserPresence` agli altri).
   - Sul client, l'handler `onreconnected` invoca di nuovo `Subscribe(currentTicker)`,
     perché il vecchio `ConnectionId` (e la sua appartenenza al gruppo) non esiste più.
   - Se tutti i tentativi falliscono, la connessione passa a `disconnected`
     e scatta `onclose`: a quel punto serve un refresh pagina o un nuovo
     `connection.start()` manuale per ripartire dal punto 2.

In sintesi, gli eventi del punto 4 (feed prezzi) sono l'unico traffico
realmente "asincrono e indipendente" che scorre in sottofondo per tutta la
sessione; tutti gli altri sono innescati da un'azione utente (propria o di un
altro client connesso) e seguono sempre lo schema invoke → uno o più eventi
di risposta.

## Come avviarlo

### 1. Backend

Richiede .NET 8 SDK.

```bash
cd Server
dotnet restore
dotnet run
```

Di default parte su `https://localhost:5001` (o la porta indicata in console
da `dotnet run` — controllala e aggiornala nel client se diversa).

Se `dotnet run` non trova un certificato HTTPS valido in locale:

```bash
dotnet dev-certs https --trust
```

### 2. Frontend

Il client è un file HTML statico, nessuna build necessaria.

1. Apri `Client/index.html` e verifica la costante `HUB_URL` in cima allo
   script: deve puntare a `https://localhost:5001/hubs/market` (o alla porta
   effettiva del backend).
2. Apri il file direttamente nel browser, oppure servilo con un server
   statico qualsiasi, ad esempio:
   ```bash
   cd Client
   npx serve .
   ```
3. Apri due o più schede/browser diversi con nomi utente diversi per vedere
   in azione: presenza utenti, broadcast di gruppo e feed prezzi condiviso.

## Cosa provare in demo

- Apri due schede, iscriviti allo stesso ticker (es. `AAPL`) in entrambe, e
  piazza un ordine da una scheda: l'altra vede subito il trade e il nuovo
  prezzo (broadcast di gruppo).
- Cambia ticker: solo chi è iscritto a quello riceve i tick di prezzo per
  quel simbolo (gruppi = filtro server-side, non solo lato client).
- Chiudi la connessione di rete (o riavvia il server) e riaprila: il client
  mostra "riconnessione in corso" e poi si re-iscrive automaticamente al
  ticker corrente.
- Scrivi un messaggio nella chat di stanza: solo chi è nella stessa stanza
  lo vede.



### Ipotesi di flusso nella BCA

1. **Avvio TS / submit nome utente**
   Il client chiama `connection.start()` con `?user=<nome>` in query string.

2. **Handshake di connessione**
   - Il server esegue `OnConnectedAsync`.
   - Il server invia `Acknowledged` al solo chiamante (banalmente TRUE).
   - Il client riceve `Acknowledged` → non fa nulla di particolare.

3. **Iscrizione automatica alla ricezione di una Page**
   Il client, appena connesso, invoca `Ready("PAGE")`.
   - Se valido: il server risponde `Subscribed` al chiamante fornendo una APIKEY e una lista OPTIONS (con DEBUG_MODE_ACTIVE). Sarà il Token che il client deve usare per inviare una notifica di cambiamento al modello (BCA_NOTIFY_MODEL). Il server inserisce quel client nel gruppo individuato dalla API KEY.
   - Se non valido (raro in questo client, possibile solo modificando il codice):
     il server risponde `ErrorMessage` al chiamante.

4. **Invio Page dal server (SSE)** 
    - Il server invia il JSON "PAGE" assieme ad un GUID che la identifica a tutti i client connessi su quel gruppo.
    - Il client riceve il JSON e lo gestisce renderizzando la pagina. Si salva il GUID per futuri eventi.

5. **Evento da TS sullo UIElement della Page**
   - Il client invoca `BCA_NOTIFY_MODEL` (con API KEY) e passa il JSON della modifica al server assieme al GUID della Page
   - Il server effettua la modifica sul modello e invia a tutti i client connessi al gruppo della Page l'evento `BCA_MODEL_UPDATED` con il JSON della modifica.

6. **Evento dal C# per modifica modello**
   - Il server invia `BCA_NOTIFIED_MODEL` a tutti i client connessi al gruppo della Page
   - Il client effettua la modifica grafica.

7. **Cambio PAGE (nuova Page)**
   - Il server invia il JSON "PAGE" assieme ad un nuovo GUID che la identifica a tutti i client connessi su quel gruppo.
   - Il client riceve il JSON e lo gestisce renderizzando la pagina. Si salva il nuovo GUID per futuri eventi cancellando il precedente.

8. **Disconnessione di un altro utente (asincrona, può avvenire in ogni momento)**
   - Il server esegue `OnDisconnectedAsync` per quella connessione.
   - Il server rimuove l'utente da tutti i gruppi interni.

9. **Interruzione della propria connessione (rete instabile, riavvio server, ecc.)**
   - Il client SignalR passa allo stato `reconnecting`: scatta `onreconnecting`
     lato client (nessun traffico applicativo, solo stato locale).
   - Se il client riesce a ristabilire il socket entro i tentativi configurati
     (`withAutomaticReconnect([0, 2000, 5000, 10000])`), il server tratta la
     nuova connessione come un **nuovo** `ConnectionId`: rieseguirà
     `OnConnectedAsync` da capo (nuovo `Acknowledged`).
   - Sul client, l'handler `onreconnected` invoca di nuovo `Ready("PAGE")`,
     perché il vecchio `ConnectionId` (e la sua appartenenza al gruppo) non esiste più.
   - Se tutti i tentativi falliscono, la connessione passa a `disconnected`
     e scatta `onclose`: a quel punto serve un refresh pagina o un nuovo
     `connection.start()` manuale per ripartire dal punto 2.

```mermaid
sequenceDiagram
    participant C as TS Client
    participant H as SignalR Hub
    participant S as Page/Model Service
    participant G as Clients in APIKEY Group

    C->>H: start() with ?user={name}
    H-->>C: Acknowledged(true)

    C->>H: Ready("PAGE")
    H->>S: Resolve page + options + apiKey
    alt Ready valid
        H->>H: Add connection to group(apiKey)
        H-->>C: Subscribed(apiKey, options)
        S-->>H: PageJson + pageGuid
        H-->>G: PAGE(PageJson, pageGuid)
    else Ready invalid
        H-->>C: ErrorMessage(reason)
    end

    C->>H: BCA_NOTIFY_MODEL(apiKey, pageGuid, patch)
    H->>S: Validate and apply patch
    alt Patch valid
        S-->>H: UpdatedPatch
        H-->>G: BCA_MODEL_UPDATED(UpdatedPatch)
    else Patch invalid
        H-->>C: ErrorMessage(reason)
    end

    S-->>H: Server-side model change
    H-->>G: BCA_NOTIFIED_MODEL(change)

    S-->>H: New PAGE available
    H-->>G: PAGE(NewPageJson, newPageGuid)

    Note over C,H: Network interruption
    C-->>C: onreconnecting
    C->>H: Reconnect (new ConnectionId)
    H-->>C: Acknowledged(true)
    C->>H: Ready("PAGE")