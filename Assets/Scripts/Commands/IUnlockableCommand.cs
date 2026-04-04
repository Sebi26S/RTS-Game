using RTS.TechTree;
using RTS.Units;

namespace RTS.Commands
{
    public interface IUnlockableCommand
    {
        public UnlockableSO[] GetUnmetDependencies(Owner owner);
    }
}
