using RTS.Commands;
using RTS.EventBus;

namespace RTS.Events
{
    public struct CommandIssuedEvent : IEvent
    {
        public BaseCommand Command { get; }

        public CommandIssuedEvent(BaseCommand command)
        {
            Command = command;
        }
    }
}
