using RTS.Environment;
using RTS.EventBus;

namespace RTS.Events
{
    public struct SupplyDepletedEvent : IEvent
    {
        public GatherableSupply Supply { get; private set; }

        public SupplyDepletedEvent(GatherableSupply supply)
        {
            Supply = supply;
        }
    }
}
