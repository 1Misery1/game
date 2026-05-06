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

        // Room pool weights (Boss handled separately, always last)
        private static readonly (string type, float weight)[] RoomPool =
        {
            ("Monster", 4.0f),
            ("Talent",  2.0f),
            ("Coin",    2.0f),
            ("Shop",    1.5f),
            ("Mystery", 1.5f),
            ("Elite",   1.2f),
            ("Weapon",  1.5f),
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
            if (_player != null) _player.transform.position = Vector3.zero;
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
        }

        // --------------------------------------------------------------------
        //  Floor generation
        // --------------------------------------------------------------------

        private List<string> GenerateFloor()
        {
            float total = 0f;
            foreach (var e in RoomPool) total += e.weight;

            var rooms = new List<string>();
            for (int i = 0; i < nonBossRoomCount; i++)
            {
                float roll = Random.value * total;
                float acc  = 0f;
                foreach (var e in RoomPool)
                {
                    acc += e.weight;
                    if (roll <= acc) { rooms.Add(e.type); break; }
                }
            }
            rooms.Add("Boss");
            return rooms;
        }

        private void LoadRoom(int index)
        {
            if (_currentRoomRoot != null) Destroy(_currentRoomRoot);
            _currentRoomIndex = index;
            if (_player != null) _player.transform.position = Vector3.zero;

            if (index >= _floorRooms.Count)
            {
                TriggerVictory();
                return;
            }

            var type = _floorRooms[index];
            _currentRoomRoot = new GameObject($"Room_{index}_{type}");
            switch (type)
            {
                case "Monster": BuildMonsterRoom(); break;
                case "Talent":  BuildTalentRoom();  break;
                case "Coin":    BuildCoinRoom();    break;
                case "Shop":    BuildShopRoom();    break;
                case "Mystery": BuildMysteryRoom(); break;
                case "Elite":   BuildEliteRoom();   break;
                case "Weapon":  BuildWeaponRoom();  break;
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
            int remaining = 3;
            float[] angles = { Mathf.PI * 0.5f, Mathf.PI * (0.5f + 2f / 3f), Mathf.PI * (0.5f + 4f / 3f) };
            for (int i = 0; i < 3; i++)
            {
                var pos = new Vector3(Mathf.Cos(angles[i]) * 3.5f, Mathf.Sin(angles[i]) * 2.2f, 0f);
                SpawnRandomNormalEnemy(pos, () => { remaining--; if (remaining <= 0) OpenDoorToNext(); });
            }
        }

        private void BuildEliteRoom()
        {
            if (_player == null) return;
            int remaining = 3; // 精英 + 2初始小怪
            System.Action dec = () => { remaining--; if (remaining <= 0) OpenDoorToNext(); };

            switch (Random.Range(0, 4))
            {
                case 0: EliteRoom_Commander(dec);    break;
                case 1: EliteRoom_Witch(dec);        break;
                case 2: EliteRoom_PoisonShaman(dec); break;
                case 3: EliteRoom_Necromancer(dec);  break;
            }
        }

        // 腐败士官 + 2腐败盾士 — 光环让盾卫极度坚韧
        private void EliteRoom_Commander(System.Action dec)
        {
            ShowBanner("精英 — 腐败士官领军！战斗光环已强化盾卫！");
            var p    = _player.transform;
            var root = _currentRoomRoot.transform;
            var elite = EnemyFactory.SpawnCommander(new Vector3(0f, 2f, 0f), p, root);
            RegisterEnemy(elite, 15, dec);
            Vector3[] pos = { new Vector3(-3f, -1.5f, 0f), new Vector3(3f, -1.5f, 0f) };
            foreach (var mp in pos)
                RegisterEnemy(EnemyFactory.SpawnShieldGuard(mp, p, root), 6, dec);
        }

        // 女巫 + 2初始蝙蝠 — 女巫持续召唤蝙蝠群
        private void EliteRoom_Witch(System.Action dec)
        {
            ShowBanner("精英 — 恶毒女巫降临！蝙蝠群随时增援！");
            var p    = _player.transform;
            var root = _currentRoomRoot.transform;
            var elite = EnemyFactory.SpawnWitch(new Vector3(0f, 2f, 0f), p, root, spawnPos =>
            {
                var bat = EnemyFactory.SpawnBat(spawnPos, p, root);
                RegisterEnemy(bat, 3, () => { }); // 召唤蝙蝠不计入房间计数
                return bat;
            });
            RegisterEnemy(elite, 15, dec);
            Vector3[] pos = { new Vector3(-3f, -1.5f, 0f), new Vector3(3f, -1.5f, 0f) };
            foreach (var mp in pos)
                RegisterEnemy(EnemyFactory.SpawnBat(mp, p, root), 3, dec);
        }

        // 毒蛇祭司 + 2毒蜘蛛 — 祭司持续强化蜘蛛，投放毒池封路
        private void EliteRoom_PoisonShaman(System.Action dec)
        {
            ShowBanner("精英 — 毒蛇祭司现身！毒蜘蛛已受到祝福强化！");
            var p    = _player.transform;
            var root = _currentRoomRoot.transform;
            var elite = EnemyFactory.SpawnPoisonShaman(new Vector3(0f, 2f, 0f), p, root);
            var ai    = elite.GetComponent<PoisonShamanAI>();
            ai.SpawnPoisonPuddleCallback = pos =>
                EnemyFactory.SpawnPoisonPool(pos, 5f, 4f, 1.5f, root, elite);
            RegisterEnemy(elite, 15, dec);
            Vector3[] pos2 = { new Vector3(-3f, -1.5f, 0f), new Vector3(3f, -1.5f, 0f) };
            foreach (var mp in pos2)
                RegisterEnemy(EnemyFactory.SpawnPoisonSpider(mp, p, root), 3, dec);
        }

        // 死灵术士 + 2骷髅 — 术士回血自保，不断召唤骷髅援军
        private void EliteRoom_Necromancer(System.Action dec)
        {
            ShowBanner("精英 — 死灵术士登场！骷髅军团不断复活！");
            var p    = _player.transform;
            var root = _currentRoomRoot.transform;
            var elite = EnemyFactory.SpawnNecromancer(new Vector3(0f, 2f, 0f), p, root);
            var ai    = elite.GetComponent<NecromancerAI>();
            ai.SpawnSkeletonCallback = pos =>
            {
                var sk = EnemyFactory.SpawnSkeleton(pos, p, root);
                RegisterEnemy(sk, 3, () => { }); // 召唤骷髅不计入房间计数
                return sk;
            };
            RegisterEnemy(elite, 15, dec);
            Vector3[] pos2 = { new Vector3(-3f, -1.5f, 0f), new Vector3(3f, -1.5f, 0f) };
            foreach (var mp in pos2)
                RegisterEnemy(EnemyFactory.SpawnSkeleton(mp, p, root), 3, dec);
        }

        private void BuildTalentRoom()
        {
            int remaining = 2;
            Vector3[] positions = { new Vector3(-2.5f, 2f, 0f), new Vector3(2.5f, 2f, 0f) };
            for (int i = 0; i < 2; i++)
                SpawnRandomNormalEnemy(positions[i], () => { remaining--; if (remaining <= 0) DropTalentChoices(); });
        }

        private static readonly (string name, string desc, StatType stat, ModifierOp op, float value, Color color)[] TalentPool =
        {
            ("强力",   "+20% 攻击力",   StatType.Attack,            ModifierOp.PercentMul, 0.20f, new Color(1f,   0.40f, 0.40f)),
            ("疾风",   "+25% 移动速度", StatType.MoveSpeed,         ModifierOp.PercentMul, 0.25f, new Color(0.4f, 0.90f, 1f  )),
            ("活力",   "+50 最大生命",  StatType.MaxHP,             ModifierOp.Flat,       50f,   new Color(1f,   0.90f, 0.30f)),
            ("守护",   "+5 防御",       StatType.Defense,           ModifierOp.Flat,        5f,   new Color(0.4f, 0.70f, 1f  )),
            ("鹰眼",   "+15% 暴击率",   StatType.CritRate,          ModifierOp.Flat,       0.15f, new Color(1f,   0.85f, 0.20f)),
            ("致命",   "+25% 暴击伤害", StatType.CritDamage,        ModifierOp.Flat,       0.25f, new Color(1f,   0.50f, 0.10f)),
            ("狂战",   "+15% 攻击速度", StatType.AttackSpeed,       ModifierOp.PercentMul, 0.15f, new Color(1f,   0.30f, 0.60f)),
            ("奥能",   "+30% 技能强度", StatType.SkillPower,        ModifierOp.PercentMul, 0.30f, new Color(0.7f, 0.40f, 1f  )),
            ("敏捷",   "+10% 冷却缩减", StatType.CooldownReduction, ModifierOp.Flat,       0.10f, new Color(0.5f, 1f,   0.90f)),
            ("财富",   "+30% 金币获取", StatType.CoinGain,          ModifierOp.PercentMul, 0.30f, new Color(1f,   0.85f, 0.10f)),
            ("铁壁",   "+10 防御",      StatType.Defense,           ModifierOp.Flat,       10f,   new Color(0.3f, 0.60f, 1f  )),
            ("泰坦",   "+100 最大生命", StatType.MaxHP,             ModifierOp.Flat,      100f,   new Color(0.9f, 0.40f, 0.40f)),
            ("冲劲",   "+20% 移动速度", StatType.MoveSpeed,         ModifierOp.PercentMul, 0.20f, new Color(0.3f, 0.95f, 0.50f)),
            ("猛力",   "+30% 攻击力",   StatType.Attack,            ModifierOp.PercentMul, 0.30f, new Color(1f,   0.20f, 0.20f)),
        };

        private (string name, string desc, StatType stat, ModifierOp op, float value, Color color)[] PickRandomTalents(int count)
        {
            var indices = new List<int>();
            for (int i = 0; i < TalentPool.Length; i++) indices.Add(i);
            var result = new (string, string, StatType, ModifierOp, float, Color)[Mathf.Min(count, TalentPool.Length)];
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
                var talent = ScriptableObject.CreateInstance<TalentData>();
                talent.talentName  = def.name;
                talent.description = def.desc;
                talent.modifiers.Add(new StatModifierEntry { stat = def.stat, op = def.op, value = def.value });
                pickups.Add(SpawnTalentOrb(talent, def.color));
            }
            for (int i = 0; i < pickups.Count; i++)
                pickups[i].transform.position = new Vector3(-3.5f + 3.5f * i, -2.2f, 0f);

            var snapshot = new List<TalentPickup>(pickups);
            foreach (var p in snapshot)
            {
                var self = p;
                self.OnPicked += chosen =>
                {
                    ApplyTalentToPlayer(chosen);
                    foreach (var other in snapshot)
                        if (other != null && other != self) Destroy(other.gameObject);
                    OpenDoorToNext();
                };
            }
        }

        private void BuildCoinRoom()
        {
            int remaining = 5;
            ShowBanner("COIN ROOM — collect all to proceed");
            for (int i = 0; i < 5; i++)
            {
                var pos  = new Vector3(Random.Range(-5f, 5f), Random.Range(-3f, 3f), 0f);
                var coin = SpawnCoinPickup(pos, amount: 6);
                coin.OnPicked += amt =>
                {
                    RunCoins += amt;
                    remaining--;
                    if (remaining <= 0) OpenDoorToNext();
                };
            }
        }

        private void BuildShopRoom()
        {
            ShowBanner("SHOP — walk up and press E to buy");
            var defs = new (string name, string desc, StatType stat, ModifierOp op, float value, Color color, int price)[]
            {
                ("Power Up",   "+20% Attack",     StatType.Attack,    ModifierOp.PercentMul, 0.20f, new Color(1f, 0.4f, 0.4f), 25),
                ("Swift Feet", "+25% Move Speed", StatType.MoveSpeed, ModifierOp.PercentMul, 0.25f, new Color(0.4f, 0.9f, 1f), 20),
                ("Vigor",      "+50 Max HP",      StatType.MaxHP,     ModifierOp.Flat,       50f,   new Color(1f, 0.9f, 0.3f), 15),
            };

            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];
                var talent = ScriptableObject.CreateInstance<TalentData>();
                talent.talentName = d.name;
                talent.description = d.desc;
                talent.modifiers.Add(new StatModifierEntry { stat = d.stat, op = d.op, value = d.value });
                var pos = new Vector3(-3.5f + 3.5f * i, 1f, 0f);
                SpawnShopPedestal(pos, talent, d.color, d.price);
            }

            OpenDoorToNext(); // shopping is optional
        }

        private void BuildMysteryRoom()
        {
            ShowBanner("MYSTERY — approach the altar at your own risk");

            var go = new GameObject("MysteryPedestal");
            go.transform.SetParent(_currentRoomRoot.transform, true);
            go.transform.position   = Vector3.zero;
            go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = MakeUnitSquareSprite();
            sr.color        = new Color(0.75f, 0.3f, 0.95f);
            sr.sortingOrder = 7;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius    = 0.7f;
            col.isTrigger = true;

            var mystery = go.AddComponent<MysteryPedestal>();
            mystery.OnResolved += HandleMystery;
        }

        private void HandleMystery(MysteryOutcome outcome)
        {
            switch (outcome)
            {
                case MysteryOutcome.Lucky:
                    RunCoins += 25;
                    ShowBanner("LUCKY!  +25 coins");
                    break;
                case MysteryOutcome.Gift:
                    var gift = GenerateRandomTalent();
                    ApplyTalentToPlayer(gift);
                    ShowBanner($"GIFT!  Free talent: {gift.talentName}");
                    break;
                case MysteryOutcome.Heal:
                    if (_playerHealth != null) _playerHealth.Heal(9999f);
                    ShowBanner("HEALED to full!");
                    break;
                case MysteryOutcome.Cursed:
                    ApplyCurse();
                    ShowBanner("CURSED!  -15% Max HP");
                    break;
            }
            OpenDoorToNext();
        }

        private void ApplyCurse()
        {
            if (_player == null) return;
            var stats = _player.GetComponent<CharacterStats>();
            if (stats == null) return;
            stats.AddModifier(new StatModifier(StatType.MaxHP, ModifierOp.PercentMul, -0.15f, "Mystery_Curse"));
        }

        private TalentData GenerateRandomTalent()
        {
            var d = TalentPool[Random.Range(0, TalentPool.Length)];
            var t = ScriptableObject.CreateInstance<TalentData>();
            t.talentName  = d.name;
            t.description = d.desc;
            t.modifiers.Add(new StatModifierEntry { stat = d.stat, op = d.op, value = d.value });
            return t;
        }

        private void BuildWeaponRoom()
        {
            ShowBanner("武器房 — 走近后按 E 装备到当前槽（Q切换槽位）");
            var offers = GetRandomWeaponOffers(3);
            for (int i = 0; i < offers.Length; i++)
                SpawnWeaponPedestal(new Vector3(-3.5f + 3.5f * i, 0.5f, 0f), offers[i]);
            OpenDoorToNext(); // weapon room is optional
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
            if (_player == null) return;
            var stats = _player.GetComponent<CharacterStats>();
            ModifierApplier.ApplyTalent(stats, talent);
            var hp = _player.GetComponent<Health>();
            if (hp != null) hp.Heal(9999f);
            Debug.Log($"[Talent] picked: {talent.talentName}");
        }

        private void OpenDoorToNext()
        {
            if (_currentRoomRoot == null) return;
            if (_currentRoomRoot.transform.Find("Door") != null) return;

            var doorGO = new GameObject("Door");
            doorGO.transform.SetParent(_currentRoomRoot.transform, true);
            doorGO.transform.position   = new Vector3(0f, -arenaHalfHeight + 0.4f, 0f);
            doorGO.transform.localScale = new Vector3(1.6f, 0.35f, 1f);

            var sr = doorGO.AddComponent<SpriteRenderer>();
            sr.sprite       = MakeUnitSquareSprite();
            sr.color        = new Color(0.25f, 0.95f, 0.35f);
            sr.sortingOrder = 8;

            var col = doorGO.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var door     = doorGO.AddComponent<DoorTrigger>();
            int nextIndex = _currentRoomIndex + 1;
            door.OnPlayerEntered += () => LoadRoom(nextIndex);
        }

        // --------------------------------------------------------------------
        //  Spawners
        // --------------------------------------------------------------------

        private void SpawnPlayer(HeroData hero)
        {
            _player = new GameObject("Player_" + hero.heroName);
            _player.transform.position   = Vector3.zero;
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
