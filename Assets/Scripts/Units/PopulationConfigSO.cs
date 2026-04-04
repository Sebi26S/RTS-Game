using UnityEngine;

namespace RTS.Units
{
    [CreateAssetMenu(fileName = "Population Config", menuName = "Units/Population Config", order = 1)]
    public class PopulationConfigSO : ScriptableObject
    {
        [field: SerializeField] public int PopulationCost { get; private set; }
        [field: SerializeField] public int PopulationSupply { get; private set; }
    }
}
