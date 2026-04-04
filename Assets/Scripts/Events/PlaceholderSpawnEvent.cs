using RTS.EventBus;
using RTS.Player;

namespace RTS.Events
{
    public struct PlaceholderSpawnEvent : IEvent
    {
        public Placeholder Placeholder { get; private set; }

        public PlaceholderSpawnEvent(Placeholder placeholder)
        {
            Placeholder = placeholder;
        }
    }
}
