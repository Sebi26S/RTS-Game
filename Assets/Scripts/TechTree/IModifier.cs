using RTS.Units;

namespace RTS.TechTree
{
    public interface IModifier
    {
        public string PropertyPath { get; }
        public void Apply(AbstractUnitSO unit);
    }
}
