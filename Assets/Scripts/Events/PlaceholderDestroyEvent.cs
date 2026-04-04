using RTS.EventBus;
using RTS.Player;

namespace RTS.Events
{
    public struct PlaceholderDestroyEvent : IEvent
    {
        public Placeholder Placeholder { get; private set; }

        public PlaceholderDestroyEvent(Placeholder placeholder)
        {
            Placeholder = placeholder;
        }
    }
}
