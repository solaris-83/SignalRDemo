using Newtonsoft.Json;
using ScriptLibraries.Data.Interfaces;
using SignalRDemo.UIFrameworkImpl;
using UIFramework;
using UIFramework.Interfaces;
using UIFramework.SpecializedPages;

namespace SignalRDemo
{
    public class UIBuilderService : IUiEventService
    {
        private readonly ITranslationService _translatorService;
        private readonly UIFramework.Interfaces.ILogger _consoleLogger;
        private readonly IUIContext _uicontext;
        private readonly Action<string> _callBackSendToUI;
        //private readonly IHubContext<MarketHub> _hubContext;
        private readonly MarketState _state;
        private IUICommunicationDispatcher _communicationDispatcher;
        public UIBuilderService(MarketState state)
        {
            _state = state;
            _translatorService = new TranslationService();
            _consoleLogger = new ConsoleLogger();
            _uicontext = new UIContext(translator: _translatorService, directories: null, fileService: null, logger: _consoleLogger);

            _callBackSendToUI = (message) =>
            {
                Console.WriteLine($"=> Message sent to UI: {message}");
            };
        }

        public void HandleUIEvent(UIEventList events)
        {
            if (_communicationDispatcher is null)
            {
                throw new InvalidOperationException("UI non inizializzata. Chiamare CreateStandardPage() prima di HandleUIEvent.");
            }

            _communicationDispatcher.HandleUIEvents(events);
        }

        public void CreateStandardPage()
        {
            var page = (Page)UIElementFactory.CreatePage(_uicontext);
            var btn = page.AddButtonExit();

            var tab = page.AddTab("tab", 1, 1);
            var section = UIElementFactory.CreateSection(1, 1, _uicontext);
            var table = UIElementFactory.CreateTable();
            table.AddColumn("UserId", "UserId");
            table.AddColumn("Id", "Id");
            table.AddColumn("Title", "Title");
            table.AddColumn("Body", "Body");
            section.Add(table, 0, 0);
            tab.Add(section, 0, 0);

            page.OnAfterButtonClicked = async () =>
            {
                table.ClearRows();
                table.AddRow(await GetPostsFromApi());
            };
            // In teoria dovrebbe funzionare anche con click (multicast)
            //page.Click = async () =>
            //{
            //    table.ClearRows();
            //    table.AddRow(await GetPostsFromApi());
            //};

            page.DataChanged += (sender, e) =>
            {
                _communicationDispatcher.SendToUI("BCA_MODEL_UPDATED", e, _callBackSendToUI);
            };

            _communicationDispatcher = new UICommunicationDispatcher(page, _consoleLogger);
            _communicationDispatcher.SendToUI("PAGE", page, _callBackSendToUI);
        }


        private async Task<List<Post>> GetPostsFromApi()
        {

            // Create a new HttpClient instance
            using HttpClient client = new();
            List<Post> posts = new();
            try
            {
                // Send GET request to the API endpoint
                HttpResponseMessage response = await client.GetAsync("https://jsonplaceholder.typicode.com/posts");

                // Ensure the response was successful (status code 200-299)
                response.EnsureSuccessStatusCode();

                // Read the response content as a string
                string content = await response.Content.ReadAsStringAsync();

                // Deserialize JSON string into a list of Post objects
                posts = JsonConvert.DeserializeObject<List<Post>>(content);
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Request error: {e.Message}");
            }
            return posts;
        }
    }

    // Define a Post class matching the JSON structure
    public class Post
    {
        public int UserId { get; set; }
        public int Id { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
    }
}