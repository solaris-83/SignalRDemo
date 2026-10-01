using UIFramework;

namespace SignalRDemo
{
    public interface IUiEventService
    {
        void HandleUIEvent(UIEventList events);
    }
}