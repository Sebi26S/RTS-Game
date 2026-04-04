using RTS.Commands;
using RTS.EventBus;

namespace RTS.Events
{
    public struct CommandSelectedEvent : IEvent
    {
        public BaseCommand Command { get; }
        public CommandSelectedEvent(BaseCommand command)
        {
            Command = command;
        }
    }
}
