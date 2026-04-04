using UnityEngine;

namespace RTS.Units
{
    public interface IHealer
    {
        public Transform Transform { get; }
        public void HealTarget(IDamageable target);
    }
}