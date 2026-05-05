using Game.AI;
using Game.Combat;
using Game.Data;
using UnityEngine;

namespace Game.Dev
{
    // 敌人工厂：创建各类敌人GameObject并配置好所有组件
    // 调用者负责：订阅OnDied事件（计数/掉落金币/Destroy）
    public static class EnemyFactory
    {
        // ── 小怪 ──────────────────────────────────────────────────

        // 骷髅怪：HP30, ATK8, DEF0, SPD4.5 | 基础追击，3金币
        public static GameObject SpawnSkeleton(Vector3 pos, Transform player, Transform parent)
        {
            var go = MakeBase("骷髅怪", pos, 0.7f, new Color(0.85f, 0.85f, 0.75f),
                hp: 30f, atk: 8f, def: 0f, spd: 4.5f, parent: parent);
            var tag = go.AddComponent<EnemyTag>(); tag.type = EnemyType.Skeleton;
            var ai  = go.AddComponent<ChaseAI>();
            ai.target          = player;
            ai.stoppingDistance = 0.85f;
            ai.attackInterval  = 1.5f;
            ai.contactDamage   = 8f;
            return go;
        }

        // 腐败小兵：HP45, ATK12, DEF2, SPD4.0 | 近战，4金币
        public static GameObject SpawnSoldier(Vector3 pos, Transform player, Transform parent)
        {
            var go = MakeBase("腐败小兵", pos, 0.75f, new Color(0.5f, 0.75f, 0.4f),
                hp: 45f, atk: 12f, def: 2f, spd: 4.0f, parent: parent);
            var tag = go.AddComponent<EnemyTag>(); tag.type = EnemyType.Soldier;
            var ai  = go.AddComponent<ChaseAI>();
            ai.target          = player;
            ai.stoppingDistance = 0.9f;
            ai.attackInterval  = 1.2f;
            ai.contactDamage   = 12f;
            return go;
        }

        // 腐败弓箭手：HP25, ATK15, DEF0, SPD3.0 | 远程，4金币
        public static GameObject SpawnArcher(Vector3 pos, Transform player, Transform parent)
        {
            var go = MakeBase("腐败弓箭手", pos, 0.65f, new Color(0.6f, 0.85f, 0.35f),
                hp: 25f, atk: 0f, def: 0f, spd: 3.0f, parent: parent);
            var tag = go.AddComponent<EnemyTag>(); tag.type = EnemyType.Archer;
            var ai  = go.AddComponent<ArcherAI>();
            ai.target           = player;
            ai.preferredDistance = 6f;
            ai.attackRange      = 9f;
            ai.attackInterval   = 2.0f;
            ai.projectileDamage = 15f;
            return go;
        }

        // 飞天蝙蝠：HP20, DEF0, SPD7.0 | 环绕冲刺，3金币
        public static GameObject SpawnBat(Vector3 pos, Transform player, Transform parent)
        {
            var go = MakeBase("飞天蝙蝠", pos, 0.55f, new Color(0.35f, 0.2f, 0.5f),
                hp: 20f, atk: 0f, def: 0f, spd: 7.0f, parent: parent);
            go.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var tag = go.AddComponent<EnemyTag>(); tag.type = EnemyType.Bat;
            var ai  = go.AddComponent<BatAI>();
            ai.target        = player;
            ai.orbitRadius   = 3.5f;
            ai.orbitSpeed    = 3.5f;
            ai.dashSpeed     = 16f;
            ai.dashDuration  = 0.35f;
            ai.dashCooldown  = 3.0f;
            ai.dashDamage    = 12f;
            return go;
        }

        // 腐败盾士：HP70, ATK14, DEF5, SPD3.5 | 盾牌减伤，6金币
        public static GameObject SpawnShieldGuard(Vector3 pos, Transform player, Transform parent)
        {
            var go = MakeBase("腐败盾士", pos, 0.8f, new Color(0.3f, 0.5f, 0.8f),
                hp: 70f, atk: 14f, def: 5f, spd: 3.5f, parent: parent);
            var tag = go.AddComponent<EnemyTag>(); tag.type = EnemyType.ShieldGuard;
            var ai  = go.AddComponent<ShieldGuardAI>();
            ai.target          = player;
            ai.attackRange     = 1.1f;
            ai.attackInterval  = 1.5f;
            ai.contactDamage   = 14f;
            ai.shieldInterval  = 8f;
            ai.shieldDuration  = 3f;
            ai.shieldReduction = 0.8f;
            return go;
        }

        // ── 精英 ──────────────────────────────────────────────────

        // 腐败士官：HP150, ATK20, DEF4, SPD3.5 | 双手剑AOE+光环，15金币
        public static GameObject SpawnCommander(Vector3 pos, Transform player, Transform parent)
        {
            var go = MakeBase("腐败士官", pos, 0.9f, new Color(0.9f, 0.6f, 0.2f),
                hp: 150f, atk: 20f, def: 4f, spd: 3.5f, parent: parent);
            var tag = go.AddComponent<EnemyTag>(); tag.type = EnemyType.Commander;
            var ai  = go.AddComponent<CommanderAI>();
            ai.target        = player;
            ai.attackRange   = 2.2f;
            ai.attackInterval = 2.0f;
            ai.attackDamage  = 20f;
            ai.auraRadius    = 8f;
            ai.auraCooldown  = 10f;
            ai.auraDuration  = 8f;
            ai.auraHpBonus   = 0.5f;
            ai.auraAtkBonus  = 0.3f;
            return go;
        }

        // 女巫：HP100, ATK18, DEF0, SPD3.0 | 法杖攻击+召唤蝙蝠，15金币
        public static GameObject SpawnWitch(Vector3 pos, Transform player, Transform parent,
            System.Func<Vector3, GameObject> spawnBatCallback)
        {
            var go = MakeBase("女巫", pos, 0.75f, new Color(0.75f, 0.3f, 0.9f),
                hp: 100f, atk: 18f, def: 0f, spd: 3.0f, parent: parent);
            var tag = go.AddComponent<EnemyTag>(); tag.type = EnemyType.Witch;
            var ai  = go.AddComponent<WitchAI>();
            ai.target              = player;
            ai.preferredDistance   = 6f;
            ai.attackRange         = 8f;
            ai.attackInterval      = 2.5f;
            ai.attackDamage        = 18f;
            ai.blastRadius         = 1.8f;
            ai.summonCooldown      = 8f;
            ai.summonCount         = 2;
            ai.SpawnBatCallback    = spawnBatCallback;
            return go;
        }

        // ── Boss ──────────────────────────────────────────────────

        // 地狱巨人：HP400, ATK35, DEF8, SPD2.5 | 岩浆+重踏，二阶段强化
        public static GameObject SpawnHellGiant(Vector3 pos, Transform player, Transform parent,
            System.Func<Vector3, float, float, float, GameObject> spawnLavaCallback)
        {
            var go = MakeBase("地狱巨人", pos, 1.2f, new Color(0.7f, 0.12f, 0.08f),
                hp: 400f, atk: 35f, def: 8f, spd: 2.5f, parent: parent);
            var tag = go.AddComponent<EnemyTag>(); tag.type = EnemyType.HellGiant;
            go.GetComponent<SpriteRenderer>().sortingOrder = 6;

            var ai = go.AddComponent<HellGiantAI>();
            ai.target             = player;
            ai.attackRange        = 2.0f;
            ai.attackInterval     = 2.0f;
            ai.attackDamage       = 35f;
            ai.lavaCooldown       = 6f;
            ai.lavaDamagePerSec   = 8f;
            ai.lavaLifetime       = 5f;
            ai.lavaRadius         = 2.5f;
            ai.lavaCount_P1       = 2;
            ai.lavaCount_P2       = 3;
            ai.slamCooldown       = 10f;
            ai.slamRadius         = 4f;
            ai.slamDamage         = 30f;
            ai.slamKnockback      = 12f;
            ai.slamStunDuration   = 0.8f;
            ai.phase2SpeedBonus   = 0.5f;
            ai.SpawnLavaCallback  = spawnLavaCallback;
            return go;
        }

        // ── 岩浆池 ────────────────────────────────────────────────

        public static GameObject SpawnLavaPool(Vector3 pos, float dps, float lifetime, float radius,
            Transform parent, GameObject owner)
        {
            var go = new GameObject("岩浆池");
            go.transform.SetParent(parent, true);
            go.transform.position   = pos;
            go.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = GetSquareSprite();
            sr.color        = new Color(1f, 0.35f, 0.05f, 0.75f);
            sr.sortingOrder = 3;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius    = 0.5f;  // localScale放大处理实际范围
            col.isTrigger = true;

            var lava         = go.AddComponent<LavaPool>();
            lava.damagePerSecond = dps;
            lava.lifetime        = lifetime;
            lava.owner           = owner;
            return go;
        }

        // ── 内部工具 ──────────────────────────────────────────────

        private static GameObject MakeBase(string name, Vector3 pos, float size, Color color,
            float hp, float atk, float def, float spd, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.position   = pos;
            go.transform.localScale = new Vector3(size, size, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = GetSquareSprite();
            sr.color        = color;
            sr.sortingOrder = 5;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType       = RigidbodyType2D.Kinematic;
            rb.freezeRotation = true;

            var col    = go.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;

            var stats = go.AddComponent<CharacterStats>();
            stats.SetBase(StatType.MaxHP,       hp);
            stats.SetBase(StatType.Attack,      atk);
            stats.SetBase(StatType.Defense,     def);
            stats.SetBase(StatType.MoveSpeed,   spd);
            stats.SetBase(StatType.AttackSpeed, 1f);

            go.AddComponent<Health>();
            return go;
        }

        private static Sprite _square;
        private static Sprite GetSquareSprite()
        {
            if (_square != null) return _square;
            const int sz = 32;
            var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var px = new Color[sz * sz];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            _square = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), sz);
            return _square;
        }
    }
}
