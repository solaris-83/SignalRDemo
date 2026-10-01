using ScriptLibraries.Data.Interfaces;
using UIFramework.Interfaces;

namespace SignalRDemo.UIFrameworkImpl
{
    public sealed class UIContext : UIContextBase
    {
        public UIContext(ITranslationService translator, Dictionary<string, string> directories, IFileService fileService, UIFramework.Interfaces.ILogger logger) : base(translator, directories, fileService, logger)
        {

        }
    }
}
