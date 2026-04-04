using UnityEngine;

namespace RTS.Environment
{
    public enum SupplyType
    {
        Minerals,
        Wood,
        Stone
    }

    [CreateAssetMenu(menuName = "Supply", fileName = "Supply", order = 5)]
    public class SupplySO : ScriptableObject
    {
        [field: SerializeField] public SupplyType Type { get; private set; }

        [field: SerializeField] public int MaxAmount { get; private set; } = 1500;

        [field: SerializeField] public int AmountPerGather { get; private set; } = 8;

        [field: SerializeField] public float BaseGatherTime { get; private set; } = 1.5f;

        [field: SerializeField] public int MaxStoredAmount { get; private set; } = 2000;
    }
}



