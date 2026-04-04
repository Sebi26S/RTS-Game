using RTS.Environment;
using RTS.TechTree;
using UnityEngine;

namespace RTS.Units
{
    [CreateAssetMenu(fileName = "Resource Conversion", menuName = "Economy/Resource Conversion")]
    public class ResourceConversionSO : UnlockableSO
    {
        [field: Header("Conversion Result")]
        [field: SerializeField] public int MineralsGained { get; private set; } = 100;

        [field: SerializeField] public SupplySO MineralsSO { get; private set; }
    }
}