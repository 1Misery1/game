using Game.Data;
using UnityEngine;

namespace Game.Combat
{
    public struct DamageInfo
    {
        public float Amount;
        public DamageType Type;
        public bool IsCrit;
        public GameObject Source;
    }

    public interface IDamageable
    {
        void TakeDamage(DamageInfo info);
    }
}
