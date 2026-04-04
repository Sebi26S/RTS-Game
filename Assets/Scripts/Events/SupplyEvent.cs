using RTS.Environment;
using RTS.EventBus;
using RTS.Units;

namespace RTS.Events
{
    public struct SupplyEvent : IEvent
    {
        public int Amount { get; private set; }
        public SupplySO Supply { get; private set; }
        public Owner Owner { get; private set; }

        public SupplyEvent(Owner owner, int amount, SupplySO supply)
        {
            Amount = amount;
            Supply = supply;
            Owner = owner;
        }
    }
}
