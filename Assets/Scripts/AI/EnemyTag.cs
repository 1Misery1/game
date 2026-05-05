using UnityEngine;

namespace Game.AI
{
    public enum EnemyType
    {
        Skeleton,       // 骷髅怪
        Soldier,        // 腐败小兵
        Archer,         // 腐败弓箭手
        Bat,            // 飞天蝙蝠
        ShieldGuard,    // 腐败盾士
        Commander,      // 腐败士官 (精英)
        Witch,          // 女巫 (精英)
        HellGiant,      // 地狱巨人 (Boss)
    }

    public class EnemyTag : MonoBehaviour
    {
        public EnemyType type;

        // 可被精英光环强化的小怪类型
        public bool IsAuraTarget =>
            type == EnemyType.Soldier  ||
            type == EnemyType.Archer   ||
            type == EnemyType.ShieldGuard;
    }
}
