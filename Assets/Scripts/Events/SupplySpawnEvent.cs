using RTS.Environment;
using RTS.EventBus;

namespace RTS.Events
{
    public struct SupplySpawnEvent : IEvent
    {
        public GatherableSupply Supply { get; private set; }

        public SupplySpawnEvent(GatherableSupply supply)
        {
            Supply = supply;
        }
    }
}