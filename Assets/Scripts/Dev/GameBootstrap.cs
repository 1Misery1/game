using System.Collections.Generic;
using Game.AI;
using Game.Combat;
using Game.Core;
using Game.Data;
using Game.Dungeon;
using Game.Player;
using Game.Systems;
using UnityEngine;

namespace Game.Dev
{
    /// Single-scene state machine: Menu -> Playing -> FloorComplete -> Playing... -> Victory/Death -> Menu.
    /// Supports multi-floor runs with enemy scaling and weapon drops.
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private float arenaHalfWidth  = 8f;
        [SerializeField] private float arenaHalfHeight = 4.5f;
        [SerializeField] private int   clearReward     = 50;
        [SerializeField] private int   nonBossRoomCount = 4;
        [SerializeField] private int   maxFloor        = 3;

        private enum State { Menu, Playing, FloorComplete, Victory, Death }

        // 战斗房间权重（Shop 单独固定插入，Boss 固定最后）
        private static readonly (string type, float weight)[] RoomPool =
        {
            ("Monster", 5.0f),
            ("Coin",    2.0f),
            ("Talent",  2.0f),
        };

        public int RunCoins      { get; private set; }
        public int CurrentFloor  { get; private set; } = 1;

        // Enemy stats multiply by this each floor
        private float FloorScale => 1f + (CurrentFloor - 1) * 0.3f;

        private State _state = State.Menu;
        private PersistentState _persistent;
        private HeroData[] _heroes;
        private int _selectedHeroIndex = 0;

        private GameObject _arenaRoot;
        private GameObject _player;
        private Health     _playerHealth;
        private GameObject _currentRoomRoot;
        private List<string> _floorRooms = new List<string>();
        private int _currentRoomIndex;

        private DamageNumbers _damageNumbers;
        private string _bannerMessage;
        private float  _bannerUntil;

        private Health _bossHealth;
        private string _bossName;
        private int    _enemiesKilled;
        private float  _totalDamageDealt;

        // Talent tracking
        private sealed class ActiveTalent
        {
            public TalentData Data;
            public object     Source   = new object();
            public int        RoomsLeft; // -1 = permanent
            public bool       IsPermanent => RoomsLeft < 0;
        }
        private readonly List<ActiveTalent> _activeTalents = new List<ActiveTalent>();
        private TalentData _pendingTalent;
        private const int  MaxTalents = 2;

        // --------------------------------------------------------------------
        //  Startup
        // --------------------------------------------------------------------

        private void Start()
        {
            _persistent    = PersistentState.Load();
            BuildHeroPool();
            EnsureStarterUnlocked();
            EnsureCamera();
            _damageNumbers = gameObject.AddComponent<DamageNumbers>();
            EnterMenu();
        }

        private void BuildHeroPool()
        {
            _heroes = new[]
            {
                MakeHero("Warrior", "坚韧近战战士，擅长正面对抗。",
                    120f, 12f, 3f, 5f,   1.0f, 0,   new Color(0.4f, 0.85f, 1f),
                    HeroSkillType.WarCry,        10f, "战吼",
                    HeroPassiveType.BattlefieldWill,  "战场意志"),

                MakeHero("Ranger", "敏捷游侠，连击可叠加攻击加成。",
                    85f,  11f, 0f, 7f,   1.3f, 30,  new Color(0.8f, 1f,   0.5f),
                    HeroSkillType.ShadowStep,    6f,  "影步",
                    HeroPassiveType.ComboStrike,      "连击"),

                MakeHero("Mage", "玻璃炮，技能后下次普攻伤害翻倍。",
                    70f,  20f, 0f, 4.5f, 0.8f, 60,  new Color(1f,   0.6f, 1f),
                    HeroSkillType.ArcaneSurge,   8f,  "奥术迸发",
                    HeroPassiveType.ManaAmplification,"魔力增幅"),

                MakeHero("Paladin", "圣骑士，击杀敌人回复5HP。",
                    130f, 10f, 5f, 4.5f, 0.9f, 100, new Color(1f,   0.9f, 0.4f),
                    HeroSkillType.HolyLight,     12f, "神圣之光",
                    HeroPassiveType.SacredOath,       "神圣誓约"),

                MakeHero("Hunter", "猎人，永久爆击率+20%、爆伤+30%。",
                    80f,  15f, 0f, 6f,   1.1f, 150, new Color(1f,   0.55f, 0.3f),
                    HeroSkillType.PrecisionShot, 7f,  "精准射击",
                    HeroPassiveType.EagleEye,         "鹰眼"),
            };
        }

        private HeroData MakeHero(
            string name, string desc,
            float hp, float atk, float def, float ms, float asp,
            int cost, Color tint,
            HeroSkillType skillType, float skillCd, string skillName,
            HeroPassiveType passiveType, string passiveName)
        {
            var h = ScriptableObject.CreateInstance<HeroData>();
            h.heroName          = name;
            h.description       = desc;
            h.baseMaxHP         = hp;
            h.baseAttack        = atk;
            h.baseDefense       = def;
            h.baseMoveSpeed     = ms;
            h.baseAttackSpeed   = asp;
            h.unlockCost        = cost;
            h.tintColor         = tint;
            h.unlockedByDefault = cost == 0;
            h.heroSkillType     = skillType;
            h.heroSkillCooldown = skillCd;
            h.heroSkillName     = skillName;
            h.heroPassiveType   = passiveType;
            h.heroPassiveName   = passiveName;
            return h;
        }

        private void EnsureStarterUnlocked()
        {
            if (_heroes.Length == 0) return;
            var starter = _heroes[0].heroName;
            if (!_persistent.IsHeroUnlocked(starter))
            {
                _persistent.UnlockedHeroIds.Add(starter);
                _persistent.Save();
            }
        }

        // --------------------------------------------------------------------
        //  State transitions
        // --------------------------------------------------------------------

        private void EnterMenu()
        {
            CleanupRun();
            _state = State.Menu;
        }

        private void StartRun()
        {
            if (_selectedHeroIndex < 0 || _selectedHeroIndex >= _heroes.Length) return;
            var hero = _heroes[_selectedHeroIndex];
            if (!_persistent.IsHeroUnlocked(hero.heroName)) return;

            CleanupRun();
            _state       = State.Playing;
            RunCoins     = 0;
            CurrentFloor = 1;
            _floorRooms  = GenerateFloor();
            BuildArena();
            SpawnPlayer(hero);
            LoadRoom(0);
        }

        private void TriggerVictory()
        {
            _state = State.Victory;
            _persistent.AddCurrency(clearReward);
            _persistent.Save();
        }

        private void TriggerFloorComplete()
        {
            _state = State.FloorComplete;
            _persistent.AddCurrency(clearReward);
            _persistent.Save();
        }

        private void AdvanceFloor()
        {
            CurrentFloor++;
            _floorRooms = GenerateFloor();
            if (_currentRoomRoot != null) { Destroy(_currentRoomRoot); _currentRoomRoot = null; }
            if (_player != null) _player.transform.position = new Vector3(-arenaHalfWidth + 0.8f, 0f, 0f);
            LoadRoom(0);
            _state = State.Playing;
            ShowBanner($"第 {CurrentFloor} 层 — 难度 ×{FloorScale:0.0}");
        }

        private void TriggerDeath()
        {
            _state = State.Death;
        }

        private void CleanupRun()
        {
            if (_currentRoomRoot != null) Destroy(_currentRoomRoot);
            if (_arenaRoot != null)       Destroy(_arenaRoot);
            if (_player != null)          Destroy(_player);
            _playerHealth     = null;
            _currentRoomIndex = 0;
            _bannerMessage    = null;
            _bannerUntil      = 0f;
            _bossHealth       = null;
            _bossName         = null;
            _enemiesKilled    = 0;
            _totalDamageDealt = 0f;
            _activeTalents.Clear();
            _pendingTalent    = null;
        }

        // --------------------------------------------------------------------
        //  Floor generation
        // --------------------------------------------------------------------

        private List<string> GenerateFloor()
        {
            float total      = 0f;
            foreach (var e in RoomPool) total += e.weight;

            // 战斗房间数随层数递增：Floor1=4, Floor2=5, Floor3=6
            int combatCount  = nonBossRoomCount + (CurrentFloor - 1);
            var rooms        = new List<string>();
            for (int i = 0; i < combatCount; i++)
            {
                float roll = Random.value * total;
                float acc  = 0f;
                foreach (var e in RoomPool)
                {
                    acc += e.weight;
                    if (roll <= acc) { rooms.Add(e.type); break; }
                }
            }

            // 商店随机插入（不放在第一个和最后一个位置）
            int shopPos = rooms.Count > 1 ? Random.Range(1, rooms.Count) : 0;
            rooms.Insert(shopPos, "Shop");

            rooms.Add("Boss");
            return rooms;
        }

        private void LoadRoom(int index)
        {
            if (_currentRoomRoot != null) Destroy(_currentRoomRoot);
            _currentRoomIndex = index;
            // 玩家重置到左侧起点
            if (_player != null)
                _player.transform.position = new Vector3(-arenaHalfWidth + 0.8f, 0f, 0f);

            if (index >= _floorRooms.Count)
            {
                TriggerVictory();
                return;
            }

            OnNewRoomEntered(); // 限时天赋倒计时

            var type = _floorRooms[index];
            _currentRoomRoot = new GameObject($"Room_{index}_{type}");
            switch (type)
            {
                case "Monster": BuildMonsterRoom(); break;
                case "Talent":  BuildTalentRoom();  break;
                case "Coin":    BuildCoinRoom();    break;
                case "Shop":    BuildShopRoom();    break;
                case "Boss":    BuildBossRoom();    break;
            }
        }

        // --------------------------------------------------------------------
        //  Room builders
        // --------------------------------------------------------------------

        private GameObject SpawnRandomNormalEnemy(Vector3 pos, System.Action onDied)
        {
            if (_player == null) return null;
            int pick = Random.Range(0, 8);
            int coins;
            GameObject enemy;
            var p    = _player.transform;
            var root = _currentRoomRoot.transform;
            switch (pick)
            {
                case 0:  enemy = EnemyFactory.SpawnSkeleton(pos, p, root);        coins = 3; break;
                case 1:  enemy = EnemyFactory.SpawnSoldier(pos, p, root);         coins = 4; break;
                case 2:  enemy = EnemyFactory.SpawnArcher(pos, p, root);          coins = 4; break;
                case 3:  enemy = EnemyFactory.SpawnBat(pos, p, root);             coins = 3; break;
                case 4:  enemy = EnemyFactory.SpawnShieldGuard(pos, p, root);     coins = 6; break;
                case 5:  enemy = EnemyFactory.SpawnPoisonSpider(pos, p, root);    coins = 3; break;
                case 6:  enemy = EnemyFactory.SpawnShadowAssassin(pos, p, root);  coins = 5; break;
                default: enemy = EnemyFactory.SpawnExplosiveDemon(pos, p, root);  coins = 4; break;
            }
            RegisterEnemy(enemy, coins, onDied);
            return enemy;
        }

        // 通用：挂载视觉回调 + 特殊死亡效果 + 金币/死亡事件
        private void RegisterEnemy(GameObject enemy, int baseCoins, System.Action onDied)
        {
            ScaleEnemyStats(enemy, FloorScale);
            AttachVisualCallbacks(enemy);
            AttachSpecialDeathEffect(enemy);
            int c  = Mathf.RoundToInt(baseCoins * FloorScale);
            var hp = enemy.GetComponent<Health>();
            hp.OnDamaged += dmg => { _totalDamageDealt += dmg.Amount; };
            hp.OnDied += () => { RunCoins += c; PlayerPassiveEvents.RaisePlayerKilledEnemy(); _enemiesKilled++; };
            hp.OnDied += () => Destroy(enemy);
            hp.OnDied += onDied;
        }

        // 受击：白色闪光 + 浮动伤害数字
        private void AttachVisualCallbacks(GameObject enemy)
        {
            var sr = enemy.GetComponent<SpriteRenderer>();
            var hp = enemy.GetComponent<Health>();
            var tr = enemy.transform;
            if (sr == null) return;
            hp.OnDamaged += dmg =>
            {
                StartCoroutine(FlashRoutine(sr, Color.white, 0.06f));
                if (tr != null) DamageNumbers.Instance?.Show(tr.position, dmg.Amount, dmg.IsCrit);
            };
        }

        // 特殊死亡效果（在 Destroy 之前触发，可安全读取 transform）
        private void AttachSpecialDeathEffect(GameObject enemy)
        {
            var tag = enemy.GetComponent<EnemyTag>();
            var hp  = enemy.GetComponent<Health>();
            if (tag == null) return;

            switch (tag.type)
            {
                case EnemyType.PoisonSpider:
                    var spiderRoot = _currentRoomRoot;
                    hp.OnDied += () =>
                    {
                        if (enemy == null) return;
                        var parent = spiderRoot != null ? spiderRoot.transform : null;
                        EnemyFactory.SpawnPoisonPool(enemy.transform.position, 4f, 3f, 1f, parent, null);
                    };
                    break;

                case EnemyType.ExplosiveDemon:
                    var demonAI = enemy.GetComponent<ExplosiveDemonAI>();
                    hp.OnDied += () =>
                    {
                        if (demonAI != null && demonAI.HasExploded) return;
                        if (enemy != null) DoExplosionAt(enemy.transform.position);
                    };
                    break;
            }
        }

        private void DoExplosionAt(Vector3 pos)
        {
            foreach (var col in Physics2D.OverlapCircleAll(pos, 3f))
            {
                if (col.GetComponent<EnemyTag>() != null) continue; // 不伤害其他敌人
                col.GetComponent<IDamageable>()?.TakeDamage(new DamageInfo
                {
                    Amount = 40f, Type = DamageType.True, Source = null
                });
            }
            DamageNumbers.Instance?.Show(pos, 40f, false);
        }

        private void BuildMonsterRoom()
        {
            ShowBanner("消灭所有敌人 → 获得随机武器");
            MaybeAddAltar();
            int count = GetRoomEnemyCount();
            SpawnRoomWave(count, () =>
            {
                var offers = GetRandomWeaponOffers(1);
                if (offers.Length > 0)
                    SpawnWeaponPedestal(new Vector3(5f, 0f, 0f), offers[0]);
                ShowBanner("战斗胜利！武器奖励已出现！");
                OpenRightDoor();
            });
        }


        private void BuildTalentRoom()
        {
            ShowBanner("消灭所有敌人 → 选择一个天赋");
            MaybeAddAltar();
            int count = GetRoomEnemyCount();
            SpawnRoomWave(count, DropTalentChoices);
        }

        // (name, desc, stat, op, value, color, roomDuration)  -1 = permanent
        private static readonly (string name, string desc, StatType stat, ModifierOp op, float value, Color color, int rooms)[] TalentPool =
        {
            // ── 永久天赋 ────────────────────────────────────────────
            ("强力",   "+20% 攻击力",         StatType.Attack,            ModifierOp.PercentMul, 0.20f, new Color(1f,   0.40f, 0.40f), -1),
            ("疾风",   "+25% 移动速度",       StatType.MoveSpeed,         ModifierOp.PercentMul, 0.25f, new Color(0.4f, 0.90f, 1f  ), -1),
            ("活力",   "+50 最大生命",        StatType.MaxHP,             ModifierOp.Flat,       50f,   new Color(1f,   0.90f, 0.30f), -1),
            ("守护",   "+5 防御",             StatType.Defense,           ModifierOp.Flat,        5f,   new Color(0.4f, 0.70f, 1f  ), -1),
            ("鹰眼",   "+15% 暴击率",         StatType.CritRate,          ModifierOp.Flat,       0.15f, new Color(1f,   0.85f, 0.20f), -1),
            ("致命",   "+25% 暴击伤害",       StatType.CritDamage,        ModifierOp.Flat,       0.25f, new Color(1f,   0.50f, 0.10f), -1),
            ("狂战",   "+15% 攻击速度",       StatType.AttackSpeed,       ModifierOp.PercentMul, 0.15f, new Color(1f,   0.30f, 0.60f), -1),
            ("奥能",   "+30% 技能强度",       StatType.SkillPower,        ModifierOp.PercentMul, 0.30f, new Color(0.7f, 0.40f, 1f  ), -1),
            ("敏捷",   "+10% 冷却缩减",       StatType.CooldownReduction, ModifierOp.Flat,       0.10f, new Color(0.5f, 1f,   0.90f), -1),
            ("财富",   "+30% 金币获取",       StatType.CoinGain,          ModifierOp.PercentMul, 0.30f, new Color(1f,   0.85f, 0.10f), -1),
            ("铁壁",   "+10 防御",            StatType.Defense,           ModifierOp.Flat,       10f,   new Color(0.3f, 0.60f, 1f  ), -1),
            ("泰坦",   "+100 最大生命",       StatType.MaxHP,             ModifierOp.Flat,      100f,   new Color(0.9f, 0.40f, 0.40f), -1),
            ("冲劲",   "+20% 移动速度",       StatType.MoveSpeed,         ModifierOp.PercentMul, 0.20f, new Color(0.3f, 0.95f, 0.50f), -1),
            ("猛力",   "+30% 攻击力",         StatType.Attack,            ModifierOp.PercentMul, 0.30f, new Color(1f,   0.20f, 0.20f), -1),
            // ── 限时天赋（持续若干个房间后消失）──────────────────────
            ("爆发",   "+80% 攻击力 (3房)",   StatType.Attack,            ModifierOp.PercentMul, 0.80f, new Color(1f,   0.05f, 0.05f),  3),
            ("急速",   "+50% 攻速 (3房)",     StatType.AttackSpeed,       ModifierOp.PercentMul, 0.50f, new Color(0.9f, 0.55f, 1f  ),  3),
            ("护甲",   "+25 防御 (4房)",      StatType.Defense,           ModifierOp.Flat,       25f,   new Color(0.5f, 0.80f, 1f  ),  4),
            ("暴走",   "+35% 暴击率 (3房)",   StatType.CritRate,          ModifierOp.Flat,       0.35f, new Color(1f,   0.95f, 0.05f),  3),
        };

        private (string name, string desc, StatType stat, ModifierOp op, float value, Color color, int rooms)[]
            PickRandomTalents(int count)
        {
            var indices = new List<int>();
            for (int i = 0; i < TalentPool.Length; i++) indices.Add(i);
            var result  = new (string, string, StatType, ModifierOp, float, Color, int)[Mathf.Min(count, TalentPool.Length)];
            for (int i = 0; i < result.Length; i++)
            {
                int ri    = Random.Range(0, indices.Count);
                result[i] = TalentPool[indices[ri]];
                indices.RemoveAt(ri);
            }
            return result;
        }

        private void DropTalentChoices()
        {
            var picks   = PickRandomTalents(3);
            var pickups = new List<TalentPickup>();
            foreach (var def in picks)
            {
                var talent            = ScriptableObject.CreateInstance<TalentData>();
                talent.talentName     = def.name;
                talent.description    = def.desc;
                talent.roomDuration   = def.rooms;
                talent.modifiers.Add(new StatModifierEntry { stat = def.stat, op = def.op, value = def.value });
                pickups.Add(SpawnTalentOrb(talent, def.color));
            }
            for (int i = 0; i < pickups.Count; i++)
                pickups[i].transform.position = new Vector3(-3.5f + 3.5f * i, 0f, 0f);

            var snapshot = new List<TalentPickup>(pickups);
            foreach (var p in snapshot)
            {
                var self = p;
                self.OnPicked += chosen =>
                {
                    ApplyTalentToPlayer(chosen);
                    foreach (var other in snapshot)
                        if (other != null && other != self) Destroy(other.gameObject);
                    OpenRightDoor();
                };
            }
        }

        private void BuildCoinRoom()
        {
            int reward = 30 + CurrentFloor * 10;
            ShowBanner($"消灭所有敌人 → 获得 {reward} 金币");
            MaybeAddAltar();
            int count = GetRoomEnemyCount();
            SpawnRoomWave(count, () =>
            {
                RunCoins += reward;
                ShowBanner($"战斗胜利！获得 {reward} 金币！");
                OpenRightDoor();
            });
        }

        // 按稀有度分组的武器工厂（用于商店品质分层）
        private static readonly System.Func<WeaponInstance>[][] WeaponsByRarity =
        {
            new System.Func<WeaponInstance>[] { WeaponLibrary.IronDagger,   WeaponLibrary.IronSword,         WeaponLibrary.IronGreatsword, WeaponLibrary.WoodenBow,    WeaponLibrary.WoodStaff    },
            new System.Func<WeaponInstance>[] { WeaponLibrary.SteelDagger,  WeaponLibrary.KnightSword,       WeaponLibrary.WarriorGreatsword, WeaponLibrary.HunterBow, WeaponLibrary.MagicStaff   },
            new System.Func<WeaponInstance>[] { WeaponLibrary.VenomFang,    WeaponLibrary.HolyBlade,         WeaponLibrary.ArmorBreaker,   WeaponLibrary.CloudPiercer, WeaponLibrary.FrostStaff   },
            new System.Func<WeaponInstance>[] { WeaponLibrary.PhantomBlade, WeaponLibrary.DragonAbyssSword,  WeaponLibrary.DoomBlade,      WeaponLibrary.CelestialBow, WeaponLibrary.ChaosWand    },
        };

        private WeaponInstance GetWeaponOfRarity(int rarityIndex)
        {
            var pool = WeaponsByRarity[Mathf.Clamp(rarityIndex, 0, WeaponsByRarity.Length - 1)];
            return pool[Random.Range(0, pool.Length)]();
        }

        private static readonly int[][] ShopRarityTable =
        {
            new[] { 0, 0, 1, 1, 2, 3 }, // Floor 1: WW GG B P
            new[] { 0, 1, 1, 2, 2, 3 }, // Floor 2: W GG BB P
            new[] { 1, 1, 2, 2, 3, 3 }, // Floor 3: GG BB PP
        };

        private static readonly int[] WeaponBasePrice = { 15, 25, 40, 65 };

        private void BuildShopRoom()
        {
            ShowBanner("商店 — 靠近后按 E 购买 (可略过)");
            OpenRightDoor(); // 购物可选，直接开右侧门

            int floorIdx   = Mathf.Clamp(CurrentFloor - 1, 0, ShopRarityTable.Length - 1);
            int[] rarities = ShopRarityTable[floorIdx];
            float priceScale = 1f + (CurrentFloor - 1) * 0.3f;

            // 6 把武器分两排排列
            for (int i = 0; i < rarities.Length; i++)
            {
                float x   = -5.5f + i * 2.2f;
                float y   = 1.2f;
                int   ri  = rarities[i];
                int   price = Mathf.RoundToInt(WeaponBasePrice[ri] * priceScale);
                var   weapon = GetWeaponOfRarity(ri);
                SpawnShopWeaponPedestal(new Vector3(x, y, 0f), weapon, price);
            }

            // 天赋抽取台（固定在中下方）
            int talentDrawPrice = Mathf.RoundToInt(30 * priceScale);
            SpawnTalentDrawPedestal(new Vector3(0f, -1.5f, 0f), talentDrawPrice);
        }

        private void SpawnShopWeaponPedestal(Vector3 pos, WeaponInstance weapon, int price)
        {
            var go = new GameObject("ShopWeapon_" + weapon.Data.weaponName);
            go.transform.SetParent(_currentRoomRoot.transform, true);
            go.transform.position   = pos;
            go.transform.localScale = new Vector3(0.65f, 0.65f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = MakeUnitSquareSprite();
            sr.color        = WeaponData.GetRarityColor(weapon.Data.rarity);
            sr.sortingOrder = 7;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius    = 0.9f;
            col.isTrigger = true;

            var pedestal       = go.AddComponent<WeaponPedestal>();
            pedestal.Weapon    = weapon;
            pedestal.OnEquipped = w =>
            {
                if (_player == null) return;
                var handler = _player.GetComponent<PlayerWeaponHandler>();
                if (handler == null) return;
                if (RunCoins < price) { ShowBanner($"金币不足！需要 {price} 金币"); return; }
                RunCoins -= price;
                handler.EquipWeapon(w, handler.ActiveSlotIndex);
                ShowBanner($"已购买并装备: {w.Data.weaponName}  (-{price}金币)");
                Destroy(go);
            };
        }

        private void SpawnTalentDrawPedestal(Vector3 pos, int price)
        {
            var go = new GameObject("TalentDraw");
            go.transform.SetParent(_currentRoomRoot.transform, true);
            go.transform.position   = pos;
            go.transform.localScale = new Vector3(0.75f, 0.75f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = MakeUnitSquareSprite();
            sr.color        = new Color(0.75f, 0.3f, 0.95f);
            sr.sortingOrder = 7;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius    = 0.9f;
            col.isTrigger = true;

            var shop         = go.AddComponent<ShopPedestal>();
            var drawn        = GenerateRandomTalent();
            shop.talent      = drawn;
            shop.price       = price;
            shop.GetCoins    = () => RunCoins;
            shop.SpendCoins  = amt => RunCoins -= amt;
            shop.OnPurchased = t =>
            {
                ApplyTalentToPlayer(t);
                ShowBanner($"抽取到天赋：{t.talentName}！");
                Destroy(go);
            };
        }


        private TalentData GenerateRandomTalent()
        {
            var d             = TalentPool[Random.Range(0, TalentPool.Length)];
            var t             = ScriptableObject.CreateInstance<TalentData>();
            t.talentName      = d.name;
            t.description     = d.desc;
            t.roomDuration    = d.rooms;
            t.modifiers.Add(new StatModifierEntry { stat = d.stat, op = d.op, value = d.value });
            return t;
        }


        private void BuildBossRoom()
        {
            if (_player == null) return;
            switch (CurrentFloor)
            {
                case 1:  BuildFloor1Boss(); break;
                case 2:  BuildFloor2Boss(); break;
                default: BuildFloor3Boss(); break;
            }
        }

        // 第一层：地狱巨人 — 岩浆 + 重踏
        private void BuildFloor1Boss()
        {
            ShowBanner("BOSS — 地狱巨人降临！");
            var boss   = EnemyFactory.SpawnHellGiant(new Vector3(0f, 2.5f, 0f),
                             _player.transform, _currentRoomRoot.transform, null);
            var bossAI = boss.GetComponent<HellGiantAI>();
            var roomTr = _currentRoomRoot.transform;
            bossAI.SpawnLavaCallback = (pos, dps, lt, r) =>
                EnemyFactory.SpawnLavaPool(pos, dps, lt, r, roomTr, boss);
            ScaleEnemyStats(boss, FloorScale);
            RegisterBossEvents(boss);
        }

        // 第二层：霜魂巫妖 — 冰霜新星 + 冰锥齐射
        private void BuildFloor2Boss()
        {
            ShowBanner("BOSS — 霜魂巫妖出现！当心冰霜！");
            var boss = EnemyFactory.SpawnFrostLich(new Vector3(0f, 2.5f, 0f),
                           _player.transform, _currentRoomRoot.transform);
            ScaleEnemyStats(boss, FloorScale);
            RegisterBossEvents(boss);
        }

        // 第三层：混沌领主 — 混沌爆发 + 召唤军团
        private void BuildFloor3Boss()
        {
            ShowBanner("BOSS — 混沌领主现身！终局之战！");
            var boss   = EnemyFactory.SpawnChaosLord(new Vector3(0f, 2.5f, 0f),
                             _player.transform, _currentRoomRoot.transform);
            var bossAI = boss.GetComponent<ChaosLordAI>();
            bossAI.SpawnMinionCallback = pos => SpawnRandomNormalEnemy(pos, () => { });
            ScaleEnemyStats(boss, FloorScale);
            RegisterBossEvents(boss);
        }

        // Boss通用事件：受击闪烁、浮动伤害、死亡结算
        private void RegisterBossEvents(GameObject boss)
        {
            var bossSr = boss.GetComponent<SpriteRenderer>();
            var bossHp = boss.GetComponent<Health>();
            var bossTr = boss.transform;
            _bossHealth = bossHp;
            _bossName   = boss.name;
            if (bossSr != null)
                bossHp.OnDamaged += dmg =>
                {
                    _totalDamageDealt += dmg.Amount;
                    StartCoroutine(FlashRoutine(bossSr, Color.white, 0.08f));
                    if (bossTr != null) DamageNumbers.Instance?.Show(bossTr.position, dmg.Amount, dmg.IsCrit);
                };
            bossHp.OnDied += () =>
            {
                _enemiesKilled++;
                _bossHealth = null;
                _bossName   = null;
                PlayerPassiveEvents.RaisePlayerKilledEnemy();
                Destroy(boss);
                if (CurrentFloor >= maxFloor) TriggerVictory();
                else                          TriggerFloorComplete();
            };
        }

        private void ApplyTalentToPlayer(TalentData talent)
        {
            if (_player == null || talent == null) return;

            if (_activeTalents.Count >= MaxTalents)
            {
                _pendingTalent = talent;
                ShowBanner("天赋已满（上限2个）！请在左下角选择替换");
                return;
            }

            var stats = _player.GetComponent<CharacterStats>();
            var at    = new ActiveTalent { Data = talent, RoomsLeft = talent.roomDuration };
            foreach (var entry in talent.modifiers)
                stats.AddModifier(new StatModifier(entry.stat, entry.op, entry.value, at.Source));

            _activeTalents.Add(at);

            var hp = _player.GetComponent<Health>();
            hp?.Heal(9999f);

            string dur = talent.roomDuration > 0 ? $" ({talent.roomDuration}房)" : "";
            ShowBanner($"获得天赋：{talent.talentName}{dur}");
        }

        private void ReplaceTalentAt(int index)
        {
            if (index < 0 || index >= _activeTalents.Count || _pendingTalent == null) return;
            var old   = _activeTalents[index];
            var stats = _player?.GetComponent<CharacterStats>();
            stats?.RemoveModifiersFrom(old.Source);
            _activeTalents.RemoveAt(index);
            var pending = _pendingTalent;
            _pendingTalent = null;
            ApplyTalentToPlayer(pending);
        }

        // ── 战斗房间公共逻辑 ──────────────────────────────────────

        private int GetRoomEnemyCount()
        {
            int base_ = 3 + CurrentFloor; // Floor1=4, Floor2=5, Floor3=6
            return Random.Range(base_, base_ + 2);
        }

        // 将 count 只敌人横向铺开，混入精英；全部死亡后调用 onAllDead
        private void SpawnRoomWave(int count, System.Action onAllDead)
        {
            if (_player == null) return;
            float eliteChance = 0.15f + (CurrentFloor - 1) * 0.10f; // 15/25/35%

            int remaining      = count;
            System.Action dec  = () => { remaining--; if (remaining <= 0) onAllDead(); };

            bool spawnedElite  = false;
            for (int i = 0; i < count; i++)
            {
                float x   = Random.Range(-4f, 6f);
                float y   = Random.Range(-2.5f, 2.5f);
                var   pos = new Vector3(x, y, 0f);

                if (!spawnedElite && Random.value < eliteChance)
                {
                    spawnedElite = true;
                    SpawnEliteEnemy(pos, dec);
                }
                else
                {
                    SpawnRandomNormalEnemy(pos, dec);
                }
            }
        }

        // 在战斗波次中随机生成一种精英怪
        private void SpawnEliteEnemy(Vector3 pos, System.Action onDied)
        {
            if (_player == null) return;
            var p    = _player.transform;
            var root = _currentRoomRoot.transform;
            GameObject elite;
            switch (Random.Range(0, 4))
            {
                case 0:
                    elite = EnemyFactory.SpawnCommander(pos, p, root);
                    break;
                case 1:
                    elite = EnemyFactory.SpawnWitch(pos, p, root, sp =>
                    {
                        var bat = EnemyFactory.SpawnBat(sp, p, root);
                        RegisterEnemy(bat, 3, () => { });
                        return bat;
                    });
                    break;
                case 2:
                    var shaman = EnemyFactory.SpawnPoisonShaman(pos, p, root);
                    shaman.GetComponent<PoisonShamanAI>().SpawnPoisonPuddleCallback = pp =>
                        EnemyFactory.SpawnPoisonPool(pp, 5f, 4f, 1.5f, root, shaman);
                    elite = shaman;
                    break;
                default:
                    var necro = EnemyFactory.SpawnNecromancer(pos, p, root);
                    necro.GetComponent<NecromancerAI>().SpawnSkeletonCallback = sp =>
                    {
                        var sk = EnemyFactory.SpawnSkeleton(sp, p, root);
                        RegisterEnemy(sk, 3, () => { });
                        return sk;
                    };
                    elite = necro;
                    break;
            }
            ShowBanner("精英怪出现！");
            RegisterEnemy(elite, 15, onDied);
        }

        // 15% 概率在当前房间生成神秘祭坛（可选互动）
        private void MaybeAddAltar()
        {
            if (Random.value >= 0.15f) return;

            var go = new GameObject("AltarPedestal");
            go.transform.SetParent(_currentRoomRoot.transform, true);
            go.transform.position   = new Vector3(0f, 2.8f, 0f);
            go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = MakeUnitSquareSprite();
            sr.color        = new Color(0.75f, 0.3f, 0.95f);
            sr.sortingOrder = 7;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius    = 0.7f;
            col.isTrigger = true;

            var mystery = go.AddComponent<MysteryPedestal>();
            mystery.OnResolved += HandleAltar;
            ShowBanner("神秘祭坛出现！（可选择互动）");
        }

        private void HandleAltar(MysteryOutcome outcome)
        {
            switch (outcome)
            {
                case MysteryOutcome.Lucky:
                    RunCoins += 25;
                    ShowBanner("祭坛祝福：+25 金币！");
                    break;
                case MysteryOutcome.Gift:
                    var gift = GenerateRandomTalent();
                    ApplyTalentToPlayer(gift);
                    ShowBanner($"祭坛馈赠：天赋 [{gift.talentName}]！");
                    break;
                case MysteryOutcome.Heal:
                    if (_playerHealth != null) _playerHealth.Heal(9999f);
                    ShowBanner("祭坛治愈：满血复活！");
                    break;
                case MysteryOutcome.Cursed:
                    if (_player != null)
                    {
                        var stats = _player.GetComponent<CharacterStats>();
                        stats?.AddModifier(new StatModifier(StatType.MaxHP, ModifierOp.PercentMul, -0.15f, "Altar_Curse"));
                    }
                    ShowBanner("祭坛诅咒：最大生命 -15%！");
                    break;
            }
        }

        // 每进入新房间：限时天赋计数 -1，归零时移除
        private void OnNewRoomEntered()
        {
            if (_player == null) return;
            var stats = _player.GetComponent<CharacterStats>();
            for (int i = _activeTalents.Count - 1; i >= 0; i--)
            {
                var at = _activeTalents[i];
                if (at.IsPermanent) continue;
                at.RoomsLeft--;
                if (at.RoomsLeft <= 0)
                {
                    stats?.RemoveModifiersFrom(at.Source);
                    _activeTalents.RemoveAt(i);
                    ShowBanner($"天赋 [{at.Data.talentName}] 已到期消失");
                }
            }
        }

        private void OpenRightDoor()
        {
            if (_currentRoomRoot == null) return;
            if (_currentRoomRoot.transform.Find("Door") != null) return;

            var doorGO = new GameObject("Door");
            doorGO.transform.SetParent(_currentRoomRoot.transform, true);
            doorGO.transform.position   = new Vector3(arenaHalfWidth - 0.4f, 0f, 0f);
            doorGO.transform.localScale = new Vector3(0.35f, 1.8f, 1f); // 竖向出口

            var sr = doorGO.AddComponent<SpriteRenderer>();
            sr.sprite       = MakeUnitSquareSprite();
            sr.color        = new Color(0.25f, 0.95f, 0.35f);
            sr.sortingOrder = 8;

            var col = doorGO.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var door      = doorGO.AddComponent<DoorTrigger>();
            int nextIndex = _currentRoomIndex + 1;
            door.OnPlayerEntered += () => LoadRoom(nextIndex);
        }

        // --------------------------------------------------------------------
        //  Spawners
        // --------------------------------------------------------------------

        private void SpawnPlayer(HeroData hero)
        {
            _player = new GameObject("Player_" + hero.heroName);
            _player.transform.position   = new Vector3(-arenaHalfWidth + 0.8f, 0f, 0f);
            _player.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

            var sr = _player.AddComponent<SpriteRenderer>();
            sr.sprite       = MakeUnitSquareSprite();
            sr.color        = hero.tintColor;
            sr.sortingOrder = 10;

            var rb = _player.AddComponent<Rigidbody2D>();
            rb.gravityScale           = 0f;
            rb.freezeRotation         = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = _player.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;

            var stats = _player.AddComponent<CharacterStats>();
            stats.SetBase(StatType.MaxHP,       hero.baseMaxHP);
            stats.SetBase(StatType.Attack,      hero.baseAttack);
            stats.SetBase(StatType.Defense,     hero.baseDefense);
            stats.SetBase(StatType.MoveSpeed,   hero.baseMoveSpeed);
            stats.SetBase(StatType.AttackSpeed, hero.baseAttackSpeed);

            _playerHealth = _player.AddComponent<Health>();
            var playerTr = _player.transform;
            _playerHealth.OnDamaged += dmg =>
            {
                StartCoroutine(FlashRoutine(sr, Color.white, 0.06f));
                if (playerTr != null) DamageNumbers.Instance?.Show(playerTr.position + Vector3.up * 0.5f, dmg.Amount, dmg.IsCrit);
            };
            _playerHealth.OnDied += () =>
            {
                TriggerDeath();
                Destroy(_player);
            };

            if (hero.heroSkillType != HeroSkillType.None)
            {
                var heroSkill = _player.AddComponent<HeroActiveSkillHandler>();
                heroSkill.SkillType = hero.heroSkillType;
                heroSkill.Cooldown  = hero.heroSkillCooldown;
                heroSkill.SkillName = hero.heroSkillName;
            }

            if (hero.heroPassiveType != HeroPassiveType.None)
            {
                var heroPassive = _player.AddComponent<HeroPassiveHandler>();
                heroPassive.PassiveType = hero.heroPassiveType;
            }

            var weaponHandler = _player.AddComponent<PlayerWeaponHandler>();
            _player.AddComponent<PlayerController>();

            var (slot0, slot1) = WeaponLibrary.GetStarterWeapons(hero.heroName);
            weaponHandler.EquipWeapon(slot0, 0);
            weaponHandler.EquipWeapon(slot1, 1);
        }

        private void SpawnWeaponPedestal(Vector3 pos, WeaponInstance weapon)
        {
            var go = new GameObject("WeaponPedestal_" + weapon.Data.weaponName);
            go.transform.SetParent(_currentRoomRoot.transform, true);
            go.transform.position   = pos;
            go.transform.localScale = new Vector3(0.65f, 0.65f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = MakeUnitSquareSprite();
            sr.color        = WeaponData.GetRarityColor(weapon.Data.rarity);
            sr.sortingOrder = 7;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius    = 0.9f;
            col.isTrigger = true;

            var pedestal       = go.AddComponent<WeaponPedestal>();
            pedestal.Weapon    = weapon;
            pedestal.OnEquipped = w =>
            {
                if (_player == null) return;
                var handler = _player.GetComponent<PlayerWeaponHandler>();
                if (handler != null) handler.EquipWeapon(w, handler.ActiveSlotIndex);
                ShowBanner($"已装备: {w.Data.weaponName}");
            };
        }

        private WeaponInstance[] GetRandomWeaponOffers(int count)
        {
            var all = new System.Func<WeaponInstance>[]
            {
                WeaponLibrary.IronDagger,     WeaponLibrary.SteelDagger,
                WeaponLibrary.VenomFang,      WeaponLibrary.PhantomBlade,
                WeaponLibrary.IronSword,      WeaponLibrary.KnightSword,
                WeaponLibrary.HolyBlade,      WeaponLibrary.DragonAbyssSword,
                WeaponLibrary.IronGreatsword, WeaponLibrary.WarriorGreatsword,
                WeaponLibrary.ArmorBreaker,   WeaponLibrary.DoomBlade,
                WeaponLibrary.WoodenBow,      WeaponLibrary.HunterBow,
                WeaponLibrary.CloudPiercer,   WeaponLibrary.CelestialBow,
                WeaponLibrary.WoodStaff,      WeaponLibrary.MagicStaff,
                WeaponLibrary.FrostStaff,     WeaponLibrary.ChaosWand,
            };
            var avail  = new List<int>();
            for (int i = 0; i < all.Length; i++) avail.Add(i);

            var result = new WeaponInstance[Mathf.Min(count, avail.Count)];
            for (int i = 0; i < result.Length; i++)
            {
                int ri = Random.Range(0, avail.Count);
                result[i] = all[avail[ri]]();
                avail.RemoveAt(ri);
            }
            return result;
        }

        private void ScaleEnemyStats(GameObject enemy, float scale)
        {
            if (scale <= 1.001f || enemy == null) return;
            var stats = enemy.GetComponent<CharacterStats>();
            if (stats == null) return;
            stats.SetBase(StatType.MaxHP,  stats.Get(StatType.MaxHP)  * scale);
            stats.SetBase(StatType.Attack, stats.Get(StatType.Attack) * scale);
            enemy.GetComponent<Health>()?.Heal(99999f);
        }

        private TalentPickup SpawnTalentOrb(TalentData data, Color color)
        {
            var go = new GameObject("Talent_" + data.talentName);
            go.transform.SetParent(_currentRoomRoot.transform, true);
            go.transform.localScale = new Vector3(0.55f, 0.55f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = MakeUnitSquareSprite();
            sr.color        = color;
            sr.sortingOrder = 7;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius    = 0.5f;
            col.isTrigger = true;

            var pickup = go.AddComponent<TalentPickup>();
            pickup.talent = data;
            return pickup;
        }

        private CoinPickup SpawnCoinPickup(Vector3 pos, int amount)
        {
            var go = new GameObject("Coin");
            go.transform.SetParent(_currentRoomRoot.transform, true);
            go.transform.position   = pos;
            go.transform.localScale = new Vector3(0.3f, 0.3f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = MakeUnitSquareSprite();
            sr.color        = new Color(1f, 0.85f, 0.25f);
            sr.sortingOrder = 7;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius    = 0.6f;
            col.isTrigger = true;

            var coin = go.AddComponent<CoinPickup>();
            coin.amount = amount;
            return coin;
        }

        private ShopPedestal SpawnShopPedestal(Vector3 pos, TalentData talent, Color color, int price)
        {
            var go = new GameObject("Shop_" + talent.talentName);
            go.transform.SetParent(_currentRoomRoot.transform, true);
            go.transform.position   = pos;
            go.transform.localScale = new Vector3(0.7f, 0.7f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = MakeUnitSquareSprite();
            sr.color        = color;
            sr.sortingOrder = 7;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius    = 0.9f;
            col.isTrigger = true;

            var shop = go.AddComponent<ShopPedestal>();
            shop.talent     = talent;
            shop.price      = price;
            shop.GetCoins   = () => RunCoins;
            shop.SpendCoins = amount => RunCoins -= amount;
            shop.OnPurchased = t => ApplyTalentToPlayer(t);
            return shop;
        }

        // --------------------------------------------------------------------
        //  World setup
        // --------------------------------------------------------------------

        private void EnsureCamera()
        {
            if (Camera.main != null) return;
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic    = true;
            cam.orthographicSize = 5.5f;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.18f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camGO.AddComponent<AudioListener>();
        }

        private void BuildArena()
        {
            _arenaRoot = new GameObject("Arena");
            var wallColor = new Color(0.3f, 0.3f, 0.35f);
            MakeWall(new Vector2(0f,  arenaHalfHeight + 0.25f), new Vector2(arenaHalfWidth * 2f + 0.5f, 0.5f), wallColor);
            MakeWall(new Vector2(0f, -arenaHalfHeight - 0.25f), new Vector2(arenaHalfWidth * 2f + 0.5f, 0.5f), wallColor);
            MakeWall(new Vector2( arenaHalfWidth + 0.25f, 0f),  new Vector2(0.5f, arenaHalfHeight * 2f + 0.5f), wallColor);
            MakeWall(new Vector2(-arenaHalfWidth - 0.25f, 0f),  new Vector2(0.5f, arenaHalfHeight * 2f + 0.5f), wallColor);
        }

        private void MakeWall(Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject("Wall");
            go.transform.SetParent(_arenaRoot.transform, true);
            go.transform.position   = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = MakeUnitSquareSprite();
            sr.color        = color;
            sr.sortingOrder = 0;
            go.AddComponent<BoxCollider2D>();
        }

        // --------------------------------------------------------------------
        //  Utility
        // --------------------------------------------------------------------

        private System.Collections.IEnumerator FlashRoutine(SpriteRenderer sr, Color flashColor, float duration)
        {
            if (sr == null) yield break;
            var original = sr.color;
            sr.color = flashColor;
            yield return new WaitForSeconds(duration);
            if (sr != null) sr.color = original;
        }

        private void ShowBanner(string message)
        {
            _bannerMessage = message;
            _bannerUntil   = Time.time + 3f;
        }

        private static Sprite _cachedSquare;
        private static Sprite MakeUnitSquareSprite()
        {
            if (_cachedSquare != null) return _cachedSquare;
            const int size = 32;
            var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            _cachedSquare = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _cachedSquare;
        }

        private static Texture2D _whitePixel;
        private static Texture2D WhitePixel
        {
            get
            {
                if (_whitePixel == null)
                {
                    _whitePixel = new Texture2D(1, 1);
                    _whitePixel.SetPixel(0, 0, Color.white);
                    _whitePixel.Apply();
                    _whitePixel.hideFlags = HideFlags.HideAndDontSave;
                }
                return _whitePixel;
            }
        }

        private void FillRect(Rect rect, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, WhitePixel);
            GUI.color = prev;
        }

        // --------------------------------------------------------------------
        //  GUI
        // --------------------------------------------------------------------

        private void OnGUI()
        {
            switch (_state)
            {
                case State.Menu:          DrawMenu();                                                break;
                case State.Playing:       DrawHUD();                                                break;
                case State.FloorComplete: DrawFloorComplete();                                      break;
                case State.Victory:       DrawEndScreen("VICTORY!",  new Color(1f, 0.9f, 0.2f), true);  break;
                case State.Death:         DrawEndScreen("YOU DIED",  new Color(1f, 0.3f, 0.3f), false); break;
            }
        }

        private void DrawMenu()
        {
            FillRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.08f, 0.08f, 0.12f));

            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
                normal   = { textColor = new Color(0.95f, 0.95f, 0.4f) }
            };
            GUI.Label(new Rect(0, 30, Screen.width, 60), "2D ROGUELIKE", title);

            var info = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, alignment = TextAnchor.MiddleCenter,
                normal   = { textColor = Color.white }
            };
            GUI.Label(new Rect(0, 90, Screen.width, 28), $"Unlock Currency: {_persistent.UnlockCurrency}", info);

            float cardW = 195f, cardH = 210f, gap = 10f;
            float totalW = _heroes.Length * cardW + (_heroes.Length - 1) * gap;
            float startX = (Screen.width - totalW) * 0.5f;
            float y      = 135f;

            for (int i = 0; i < _heroes.Length; i++)
            {
                var h        = _heroes[i];
                bool unlocked = _persistent.IsHeroUnlocked(h.heroName);
                bool selected = i == _selectedHeroIndex;

                var rect = new Rect(startX + i * (cardW + gap), y, cardW, cardH);
                Color bg = !unlocked ? new Color(0.28f, 0.15f, 0.15f)
                         : selected  ? new Color(0.2f, 0.45f, 0.75f)
                                     : new Color(0.18f, 0.22f, 0.3f);
                FillRect(rect, bg);
                FillRect(new Rect(rect.x + 8, rect.y + 8, 34, 34), h.tintColor);

                var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
                GUI.Label(new Rect(rect.x + 50, rect.y + 8, cardW - 58, 26), h.heroName, nameStyle);

                var descStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true, normal = { textColor = new Color(0.85f, 0.85f, 0.85f) } };
                GUI.Label(new Rect(rect.x + 8, rect.y + 48, cardW - 16, 36), h.description, descStyle);

                var statStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.8f, 0.9f, 1f) } };
                GUI.Label(new Rect(rect.x + 8, rect.y + 88, cardW - 16, 18),
                    $"HP {h.baseMaxHP:0}  ATK {h.baseAttack:0}  SPD {h.baseMoveSpeed:0.0}", statStyle);

                var skillStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(1f, 0.85f, 0.4f) } };
                GUI.Label(new Rect(rect.x + 8, rect.y + 108, cardW - 16, 18),
                    $"[F] {h.heroSkillName}  CD:{h.heroSkillCooldown:0}s", skillStyle);

                var passiveStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.6f, 1f, 0.7f) } };
                GUI.Label(new Rect(rect.x + 8, rect.y + 126, cardW - 16, 18),
                    $"[被动] {h.heroPassiveName}", passiveStyle);

                var btnRect = new Rect(rect.x + 8, rect.y + cardH - 36, cardW - 16, 28);
                if (unlocked)
                {
                    if (GUI.Button(btnRect, selected ? "✓ 已选择" : "选择"))
                        _selectedHeroIndex = i;
                }
                else
                {
                    bool affordable = _persistent.UnlockCurrency >= h.unlockCost;
                    GUI.enabled = affordable;
                    if (GUI.Button(btnRect, $"解锁 ({h.unlockCost})"))
                        _persistent.TryUnlockHero(h.heroName, h.unlockCost);
                    GUI.enabled = true;
                }
            }

            var startBtn = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
            bool canStart = _selectedHeroIndex >= 0 && _persistent.IsHeroUnlocked(_heroes[_selectedHeroIndex].heroName);
            GUI.enabled = canStart;
            if (GUI.Button(new Rect(Screen.width / 2f - 140, y + cardH + 20, 280, 50), "开始冒险", startBtn))
                StartRun();
            GUI.enabled = true;

            var hint = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.6f, 0.6f, 0.7f) } };
            GUI.Label(new Rect(0, Screen.height - 28, Screen.width, 20),
                "Save file: " + Application.persistentDataPath, hint);
        }

        private void DrawHUD()
        {
            var label = new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white } };
            string roomName = _currentRoomIndex < _floorRooms.Count ? _floorRooms[_currentRoomIndex] : "—";
            GUI.Label(new Rect(10, 10, 700, 26),
                $"Floor {CurrentFloor}/{maxFloor} · Room {_currentRoomIndex + 1}/{_floorRooms.Count} · {roomName} · 难度 ×{FloorScale:0.0}", label);
            GUI.Label(new Rect(10, 34, 800, 26),
                "WASD移动 · Space/左键普攻 · R/右键武器技能 · F英雄技能 · Q切换武器 · E购买/装备 · 绿门进入下一间", label);
            if (_playerHealth != null)
            {
                GUI.Label(new Rect(10, 58, 560, 26),
                    $"HP: {Mathf.CeilToInt(_playerHealth.Current)} / {Mathf.CeilToInt(_playerHealth.Max)}   金币: {RunCoins}",
                    label);
            }
            DrawHeroSkillHUD(label);

            DrawWeaponHUD();

            DrawBossHPBar();

            DrawTalentStatus();

            if (_pendingTalent != null) DrawTalentReplacementOverlay();

            if (Time.time < _bannerUntil && !string.IsNullOrEmpty(_bannerMessage))
            {
                var bannerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                    normal   = { textColor = new Color(1f, 0.9f, 0.4f) }
                };
                GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, 40), _bannerMessage, bannerStyle);
            }
        }

        private void DrawWeaponHUD()
        {
            if (_player == null) return;
            var handler = _player.GetComponent<PlayerWeaponHandler>();
            if (handler == null) return;

            float panelX = Screen.width - 340f;
            float panelY = 10f;
            float panelW = 330f;
            float panelH = handler.ActiveWeapon?.Data?.HasSkill == true ? 110f : 70f;

            FillRect(new Rect(panelX - 6, panelY - 4, panelW + 12, panelH + 8), new Color(0f, 0f, 0f, 0.55f));

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, fontStyle = FontStyle.Bold,
                normal   = { textColor = new Color(0.9f, 0.9f, 0.6f) }
            };
            GUI.Label(new Rect(panelX, panelY, panelW, 20), "── 武器栏 (Q切换) ──", titleStyle);

            for (int i = 0; i < 2; i++)
            {
                var wi     = handler.Slots[i];
                bool active = handler.ActiveSlotIndex == i;
                float y    = panelY + 20f + i * 24f;

                Color slotColor = wi == null ? new Color(0.5f, 0.5f, 0.5f)
                                : WeaponData.GetRarityColor(wi.Data.rarity);
                if (!active) slotColor *= 0.65f;

                string prefix   = active ? "▶" : "  ";
                string slotText = wi == null
                    ? $"{prefix} [{i + 1}] 空"
                    : $"{prefix} [{i + 1}] {wi.ShortName}  {wi.CategoryLabel}  {wi.EffectiveDamage:0}伤害  {wi.Data.attackSpeed:0.0}/s";

                var slotStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize  = active ? 13 : 12,
                    fontStyle = active ? FontStyle.Bold : FontStyle.Normal,
                    normal    = { textColor = slotColor }
                };
                GUI.Label(new Rect(panelX, y, panelW, 22), slotText, slotStyle);
            }

            var active_wi = handler.ActiveWeapon;
            if (active_wi?.Data?.HasSkill == true)
            {
                float  skillY    = panelY + 70f;
                string skillName = active_wi.Data.skill.skillName;
                float  cdRem     = handler.SkillCooldownRemaining;
                bool   ready     = handler.SkillReady;

                string skillLabel = ready
                    ? $"技能: {skillName} [就绪!] (R/右键)"
                    : $"技能: {skillName} CD: {cdRem:0.0}s";

                Color skillColor = ready ? new Color(0.5f, 1f, 0.5f) : new Color(0.7f, 0.7f, 1f);
                var skillStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12, normal = { textColor = skillColor }
                };
                GUI.Label(new Rect(panelX, skillY, panelW, 20), skillLabel, skillStyle);

                float barW = panelW - 4f;
                float barY = skillY + 20f;
                FillRect(new Rect(panelX, barY, barW, 8), new Color(0.3f, 0.3f, 0.3f));
                float fill = 1f - handler.SkillCooldownRatio;
                FillRect(new Rect(panelX, barY, barW * fill, 8), ready ? new Color(0.3f, 0.9f, 0.3f) : new Color(0.3f, 0.5f, 1f));
            }
        }

        private void DrawHeroSkillHUD(GUIStyle baseLabel)
        {
            if (_player == null) return;
            var skillHandler = _player.GetComponent<HeroActiveSkillHandler>();
            if (skillHandler == null || skillHandler.SkillType == HeroSkillType.None) return;

            float panelX = 10f;
            float panelY = 82f;
            float panelW = 220f;
            float panelH = 38f;

            FillRect(new Rect(panelX - 4, panelY - 2, panelW + 8, panelH + 4), new Color(0f, 0f, 0f, 0.5f));

            bool   ready      = skillHandler.IsReady;
            float  cdRem      = skillHandler.CooldownRemaining;
            string skillLabel = ready
                ? $"[F] {skillHandler.SkillName}  就绪!"
                : $"[F] {skillHandler.SkillName}  CD: {cdRem:0.0}s";

            Color skillColor = ready ? new Color(1f, 0.85f, 0.1f) : new Color(0.7f, 0.65f, 0.4f);
            var style = new GUIStyle(baseLabel) { fontSize = 13, normal = { textColor = skillColor } };
            GUI.Label(new Rect(panelX, panelY, panelW, 20), skillLabel, style);

            float barW = panelW;
            FillRect(new Rect(panelX, panelY + 20f, barW, 8), new Color(0.3f, 0.3f, 0.3f));
            float fill = 1f - skillHandler.CooldownRatio;
            FillRect(new Rect(panelX, panelY + 20f, barW * fill, 8),
                ready ? new Color(1f, 0.8f, 0.1f) : new Color(0.5f, 0.45f, 0.2f));
        }

        private void DrawTalentStatus()
        {
            if (_activeTalents.Count == 0) return;
            float panelX = 10f;
            float panelY = 126f;
            float panelW = 220f;
            float rowH   = 20f;
            float panelH = _activeTalents.Count * rowH + 8f;
            FillRect(new Rect(panelX - 4, panelY - 4, panelW + 8, panelH + 8), new Color(0f, 0f, 0f, 0.5f));

            var title = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };
            GUI.Label(new Rect(panelX, panelY, panelW, 16), "── 天赋 ──", title);

            for (int i = 0; i < _activeTalents.Count; i++)
            {
                var at    = _activeTalents[i];
                string dur = at.IsPermanent ? "∞" : $"{at.RoomsLeft}房";
                Color  c   = at.IsPermanent ? new Color(0.9f, 0.9f, 0.6f) : new Color(1f, 0.7f, 0.3f);
                var st = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = c } };
                GUI.Label(new Rect(panelX, panelY + 16f + i * rowH, panelW, rowH),
                    $"[{i + 1}] {at.Data.talentName}  ({dur})", st);
            }
        }

        private void DrawTalentReplacementOverlay()
        {
            FillRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.72f));

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal   = { textColor = new Color(1f, 0.85f, 0.2f) }
            };
            GUI.Label(new Rect(0, cy - 100f, Screen.width, 36),
                $"天赋已满！新天赋：{_pendingTalent.talentName}", titleStyle);

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16, alignment = TextAnchor.MiddleCenter,
                normal   = { textColor = Color.white }
            };
            GUI.Label(new Rect(0, cy - 60f, Screen.width, 26), "选择替换哪一个（或取消放弃新天赋）", subStyle);

            float btnW = 280f;
            float btnH = 46f;
            var   btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 15 };

            for (int i = 0; i < _activeTalents.Count; i++)
            {
                var at  = _activeTalents[i];
                string dur = at.IsPermanent ? "永久" : $"剩{at.RoomsLeft}房";
                float  y   = cy - 10f + i * (btnH + 10f);
                if (GUI.Button(new Rect(cx - btnW * 0.5f, y, btnW, btnH),
                    $"替换：{at.Data.talentName} ({dur})", btnStyle))
                {
                    ReplaceTalentAt(i);
                }
            }

            float cancelY = cy - 10f + _activeTalents.Count * (btnH + 10f) + 10f;
            if (GUI.Button(new Rect(cx - btnW * 0.5f, cancelY, btnW, 38),
                "取消（放弃新天赋）", btnStyle))
            {
                _pendingTalent = null;
            }
        }

        private void DrawBossHPBar()
        {
            if (_bossHealth == null) return;
            float ratio = Mathf.Clamp01(_bossHealth.Current / _bossHealth.Max);
            float barW  = Screen.width * 0.5f;
            float barH  = 18f;
            float barX  = (Screen.width - barW) * 0.5f;
            float barY  = Screen.height - 46f;

            FillRect(new Rect(barX - 4, barY - 24, barW + 8, barH + 30), new Color(0f, 0f, 0f, 0.65f));

            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = new Color(1f, 0.35f, 0.35f) }
            };
            GUI.Label(new Rect(barX, barY - 22f, barW, 20f), _bossName ?? "BOSS", nameStyle);

            FillRect(new Rect(barX, barY, barW, barH), new Color(0.22f, 0.08f, 0.08f));
            FillRect(new Rect(barX, barY, barW * ratio, barH), new Color(0.85f, 0.15f, 0.15f));

            var hpStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 12, alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = Color.white }
            };
            GUI.Label(new Rect(barX, barY, barW, barH),
                $"{Mathf.CeilToInt(_bossHealth.Current)} / {Mathf.CeilToInt(_bossHealth.Max)}", hpStyle);
        }

        private void DrawFloorComplete()
        {
            FillRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.65f));

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal   = { textColor = new Color(0.35f, 1f, 0.5f) }
            };
            GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 70), $"第 {CurrentFloor} 层清除！", titleStyle);

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, alignment = TextAnchor.MiddleCenter,
                normal   = { textColor = Color.white }
            };
            GUI.Label(new Rect(0, Screen.height * 0.36f, Screen.width, 30),
                $"已获得 +{clearReward} 解锁货币   当前金币: {RunCoins}", subStyle);

            var nextFloor = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16, alignment = TextAnchor.MiddleCenter,
                normal   = { textColor = new Color(1f, 0.8f, 0.5f) }
            };
            GUI.Label(new Rect(0, Screen.height * 0.44f, Screen.width, 26),
                $"下一层难度: ×{1f + CurrentFloor * 0.3f:0.0}  (HP↑ ATK↑)", nextFloor);

            float btnX = Screen.width / 2f - 140f;
            float btnY = Screen.height * 0.52f;
            var btn = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
            if (GUI.Button(new Rect(btnX, btnY, 280, 50), $"进入第 {CurrentFloor + 1} 层 ▶", btn))
                AdvanceFloor();
            if (GUI.Button(new Rect(btnX, btnY + 60f, 280, 50), "返回主菜单", btn))
                EnterMenu();
        }

        private void DrawEndScreen(string title, Color color, bool victory)
        {
            FillRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.6f));

            var bigStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 56, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
                normal   = { textColor = color }
            };
            GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, 80), title, bigStyle);

            var subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, alignment = TextAnchor.MiddleCenter,
                normal   = { textColor = Color.white }
            };
            if (victory)
                GUI.Label(new Rect(0, Screen.height * 0.30f, Screen.width, 30),
                    $"全部 {maxFloor} 层通关！   +{clearReward} 解锁货币   (合计: {_persistent.UnlockCurrency})", subStyle);

            float statsY = Screen.height * (victory ? 0.38f : 0.30f);
            var statStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15, alignment = TextAnchor.MiddleCenter,
                normal   = { textColor = new Color(0.85f, 0.85f, 0.85f) }
            };
            GUI.Label(new Rect(0, statsY,       Screen.width, 24), $"通关层数: {CurrentFloor} / {maxFloor}", statStyle);
            GUI.Label(new Rect(0, statsY + 26f, Screen.width, 24), $"击杀敌人: {_enemiesKilled}", statStyle);
            GUI.Label(new Rect(0, statsY + 52f, Screen.width, 24), $"总输出伤害: {Mathf.RoundToInt(_totalDamageDealt):N0}", statStyle);
            GUI.Label(new Rect(0, statsY + 78f, Screen.width, 24), $"剩余金币: {RunCoins}", statStyle);

            float btnX = Screen.width / 2f - 140f;
            float btnY = Screen.height * (victory ? 0.62f : 0.58f);
            var btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 18 };
            if (GUI.Button(new Rect(btnX, btnY,       280, 42), "再次挑战", btnStyle)) StartRun();
            if (GUI.Button(new Rect(btnX, btnY + 54f, 280, 42), "返回主菜单", btnStyle)) EnterMenu();
        }
    }
}
