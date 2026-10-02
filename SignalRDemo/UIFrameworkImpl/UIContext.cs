using Common.Data.Interfaces;
using UIFramework;
using UIFramework.Abstractions;

namespace SignalRDemo.UIFrameworkImpl
{
    public sealed class UIContext : UIContextBase
    {
        public UIContext(ITranslationService translator, Dictionary<string, string> directories, IFileService fileService, UIFramework.Abstractions.ILogger logger) : base(translator, directories, fileService, logger)
        {

        }
    }
}
