using ScriptLibraries.Data.Interfaces;

namespace SignalRDemo.UIFrameworkImpl
{
    public class TranslationService : ITranslationService
    {
        public Dictionary<string, ITranslations> AllTranslations => new Dictionary<string, ITranslations> { { "SharedTranslations", new Translation() } };

        public string Translate(string key) => AllTranslations.TryGetValue("SharedTranslations", out var translations) ? translations.GetLocalOrDefault(key) : string.Empty;
    }

    public class Translation : ITranslations
    {
        private readonly Dictionary<string, string> _dict = new Dictionary<string, string>();
        public Translation()
        {
            _dict.Add("MSG_TITLE_TAB", "Tab Title");
            _dict.Add("STOP", "Button stop");
            _dict.Add("Par1", "Paragrafo 1");
            _dict.Add("Par2", "Paragrafo 2");
            _dict.Add("Bullet1", "Bulleted item 1");
            _dict.Add("Bullet2", "Bulleted item 2");
            _dict.Add("Item1", "Item 1");
            _dict.Add("Item2", "Item 2");
            _dict.Add("Item3", "Item 3");
            _dict.Add("Par1-Updated", "Paragrafo 1 updated");
            _dict.Add("Par2-Updated", "Paragrafo 2 aggiornato");
            _dict.Add("MSG_PARAGRAPH", "This is a special app");
            _dict.Add("MSG_TITLE_PAGE", "Titolo della pagina");
            _dict.Add("PARAGRAPH_1", "Sono il paragrafo del tab 1");
            _dict.Add("PARAGRAPH_2", "Sono il paragrafo del tab 2");
            _dict.Add("PARAGRAPH_3", "Sono il paragrafo del tab 3");
            _dict.Add("TITLE_COUNTDOWN_PAGE", "Sono la pagina di countdown");
            _dict.Add("EXIT_WITHOUT_REPORT", "Esci senza report");
            _dict.Add("MSG_WAITING_TIME", "Wait for 10 minutes");
            _dict.Add("BCA_SECONDS", "secondi");
            _dict.Add("BCA_MINUTES", "minuti");
            _dict.Add("Charts", "Grafici");
            _dict.Add("MSG_X_AXIS", "Asse x");
            _dict.Add("MSG_FIRST_X_AXIS", "Primo asse x");
            _dict.Add("MSG_SIGNAL_TITLE_RPM", "RPM segnale");
            _dict.Add("MSG_SIGNAL_SUBTITLE_RPM", "RPM");
            _dict.Add("Message 1", "Primo messaggio");
            _dict.Add("Message 10", "Decimo messaggio");
            _dict.Add("TRANSL_MODAL_OK", "OK");
            _dict.Add("TRANSL_MODAL_CANCEL", "Cancella");
            _dict.Add("TRANSL_MODAL_YES", "SI");
            _dict.Add("TRANSL_MODAL_NO", "NO");
            _dict.Add("TRANSL_MODAL_RETRY", "Riprova");
            _dict.Add("MSG_HEADER_MB", "Avviso");
            _dict.Add("MSG_BODY_MB_1", "Prima label messagebox");
            _dict.Add("MSG_BODY_MB_2", "Seconda label messagebox");
            _dict.Add("TRANSL_MODAL_WAIT", "Attendere prego ...");
            _dict.Add("MSG_UPDATE_WAIT", "Attendere ancora qualche istante...");
            _dict.Add("CLICK ON ME!!", "Clicca su di me!!");
            _dict.Add("Paragraph 3 after ShowAndWait", "Paragrafo 3 dopo ShowAndWait");
            _dict.Add("Paragraph 3 - BIS -  after ShowAndWait", "Paragrafo 3 - BIS - dopo ShowAndWait");
            _dict.Add("PRINT_REPORT", "Stampa report");
            _dict.Add("Meters", "Gauges and thermometers");
            _dict.Add("Check ASK", "Controllo ASK");
            _dict.Add("FAULTS_MEMORY_READING", "Lettura guasti");
            _dict.Add("MSG_NEW_CHECKBOX", "Nuova checkbox");
            _dict.Add("MSG_ACTIVATE_INJ", "Activate_Injectors");
            _dict.Add("MSG_ACTIVATE_IGNITION", "Activate_Ignition_Coil");
            _dict.Add("MSG_DEACTIVATE_INJ", "Deactivate_Injectors");
            _dict.Add("MSG_PHASE_NUMBER", "Phase number");
            _dict.Add("MSG_TITLE_UISTATUS", "Titolo dello UIStatus");
            _dict.Add("MSG_SUBTITLE_UISTATUS", "Sottotitolo dello UIStatus");
            _dict.Add("MSG_UPDATE_TEXT_UISTATUS", "Nuovo text dello UIStatus");
            _dict.Add("MSG_PARAMETERS", "Parametri");
            _dict.Add("MSG_EXPECTED_VALUES", "Valori attesi");
            _dict.Add("MSG_RESULTS", "Esiti");
            _dict.Add("MSG_READ_VALUES", "Valori letti");
            _dict.Add("MSG_ENTER_NAME", "Enter your name");
            _dict.Add("APPSTR_11", "Guida riparazione");
            _dict.Add("APPSTR_12", "Error code UDST");
            _dict.Add("APPSTR_13", "Descrizione errore");
            _dict.Add("MSG_SYSTEM_OK", "Sistema OK");
            _dict.Add("APPSTR_14", "Possibile errore");
            _dict.Add("APPSTR_15", "Nessun errore");
            _dict.Add("APPSTR_16", "Azioni");
            _dict.Add("APPSTR_17", "Commenti");
            _dict.Add("APPSTR_18", "Nessuno");
            _dict.Add("APPSTR_19", "Sistema OK");
        }

        public List<string> Keys => _dict.Keys.ToList();

        public string GetId(string idStrApp)
        {
            return idStrApp;
        }

        public string GetIntl(string idStrApp)
        {
            throw new NotImplementedException();
        }

        public string GetIntlOrDefault(string idStrApp)
        {
            throw new NotImplementedException();
        }

        public string GetLocal(string idStrApp)
        {
            _dict.TryGetValue(idStrApp, out var val);
            return val;
        }

        public string GetLocalOrDefault(string idStrApp)
        {
            _dict.TryGetValue(idStrApp, out var val);
            return val ?? idStrApp;
        }
    }
}
