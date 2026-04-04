using RTS.EventBus;
using RTS.TechTree;
using RTS.Units;

namespace RTS.Events
{
    public struct UpgradeResearchedEvent : IEvent
    {
        public Owner Owner { get; private set; }
        public UpgradeSO Upgrade { get; private set; }
        
        public UpgradeResearchedEvent(Owner owner, UpgradeSO upgrade)
        {
            Owner = owner;
            Upgrade = upgrade;
        }
    }
}