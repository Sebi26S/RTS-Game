using RTS.EventBus;
using RTS.Units;

namespace RTS.Events
{
    public struct UnitDeathEvent : IEvent
    {
        public AbstractUnit Unit { get; private set; }

        public UnitDeathEvent(AbstractUnit unit)
        {
            Unit = unit;
        }
    }
}
