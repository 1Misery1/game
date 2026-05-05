using UnityEngine;

namespace Game.AI
{
    public enum EnemyType
    {
        // ── 普通小怪 ──────────────────────────────────────────────
        Skeleton,       // 骷髅怪      — 基础追击
        Soldier,        // 腐败小兵    — 均衡近战
        Archer,         // 腐败弓箭手  — 远程射击
        Bat,            // 飞天蝙蝠    — 环绕冲刺
        ShieldGuard,    // 腐败盾士    — 盾牌减伤
        PoisonSpider,   // 毒蜘蛛      — 接触毒素DoT，死后留毒池
        ShadowAssassin, // 暗影刺客    — 瞬移爆发
        ExplosiveDemon, // 爆炎恶魔    — 近身/死亡爆炸

        // ── 精英 ──────────────────────────────────────────────────
        Commander,      // 腐败士官    — AOE + 战斗光环（联动盾卫/小兵）
        Witch,          // 女巫        — 法术AOE + 召唤蝙蝠
        PoisonShaman,   // 毒蛇祭司    — 毒素光线 + 强化毒蜘蛛
        Necromancer,    // 死灵术士    — 灵魂汲取回血 + 召唤骷髅

        // ── Boss ──────────────────────────────────────────────────
        HellGiant,      // 地狱巨人    — 第一层Boss
        FrostLich,      // 霜魂巫妖    — 第二层Boss
        ChaosLord,      // 混沌领主    — 第三层Boss
    }

    public class EnemyTag : MonoBehaviour
    {
        public EnemyType type;

        // 可被指挥官光环强化（+MaxHP, +AttackSpeed）
        public bool IsCommanderAuraTarget =>
            type == EnemyType.Soldier     ||
            type == EnemyType.Archer      ||
            type == EnemyType.ShieldGuard ||
            type == EnemyType.ExplosiveDemon;

        // 可被毒蛇祭司光环强化（+ATK, +SPD）
        public bool IsShamanAuraTarget => type == EnemyType.PoisonSpider;

        // 兼容旧命名（CommanderAI 使用）
        public bool IsAuraTarget => IsCommanderAuraTarget;
    }
}
