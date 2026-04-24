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
    /// Single-scene state machine: Menu -> Playing -> Victory/Death -> Menu.
    /// Drives random dungeon generation across all 6 GDD room types.
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private float arenaHalfWidth = 8f;
        [SerializeField] private float arenaHalfHeight = 4.5f;
        [SerializeField] private int clearReward = 50;
        [SerializeField] private int nonBossRoomCount = 4;

        private enum State { Menu, Playing, Victory, Death }

        // Room pool weights (Boss handled separately, always last)
        private static readonly (string type, float weight)[] RoomPool =
        {
            ("Monster", 4.0f),
            ("Talent",  2.0f),
            ("Coin",    2.0f),
            ("Shop",    1.5f),
            ("Mystery", 1.5f),
        };

        public int RunCoins { get; private set; }
        public int CurrentFloor { get; private set; } = 1;

        private State _state = State.Menu;
        private PersistentState _persistent;
        private HeroData[] _heroes;
        private int _selectedHeroIndex = 0;

        private GameObject _arenaRoot;
        private GameObject _player;
        private Health _playerHealth;
        private GameObject _currentRoomRoot;
        private List<string> _floorRooms = new List<string>();
        private int _currentRoomIndex;

        private string _bannerMessage;
        private float _bannerUntil;

        // --------------------------------------------------------------------
        //  Startup
        // --------------------------------------------------------------------

        private void Start()
        {
            _persistent = PersistentState.Load();
            BuildHeroPool();
            EnsureStarterUnlocked();
            EnsureCamera();
            EnterMenu();
        }

        private void BuildHeroPool()
        {
            _heroes = new[]
            {
                MakeHero("Warrior", "Balanced. Tanky melee bruiser.",    120f, 12f, 3f, 5f,   1.0f, 0,  new Color(0.4f, 0.85f, 1f)),
                MakeHero("Rogue",   "Fast, fragile, relentless.",         80f, 10f, 0f, 7f,   1.3f, 30, new Color(0.8f, 1f,   0.5f)),
                MakeHero("Mage",    "Glass cannon. Huge attack damage.",  70f, 20f, 0f, 4.5f, 0.8f, 60, new Color(1f,   0.6f, 1f)),
            };
        }

        private HeroData MakeHero(string name, string desc, float hp, float atk, float def, float ms, float asp, int cost, Color tint)
        {
            var h = ScriptableObject.CreateInstance<HeroData>();
            h.heroName = name;
            h.description = desc;
            h.baseMaxHP = hp;
            h.baseAttack = atk;
            h.baseDefense = def;
            h.baseMoveSpeed = ms;
            h.baseAttackSpeed = asp;
            h.unlockCost = cost;
            h.tintColor = tint;
            h.unlockedByDefault = cost == 0;
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
            _state = State.Playing;
            RunCoins = 0;
            CurrentFloor = 1;
            _floorRooms = GenerateFloor();
            BuildArena();
            SpawnPlayer(hero);
            LoadRoom(0);
        }

        private void TriggerVictory()
        {
            _state = State.Victory;
            _persistent.AddCurrency(clearReward);
        }

        private void TriggerDeath()
        {
            _state = State.Death;
        }

        private void CleanupRun()
        {
            if (_currentRoomRoot != null) Destroy(_currentRoomRoot);
            if (_arenaRoot != null) Destroy(_arenaRoot);
            if (_player != null) Destroy(_player);
            _playerHealth = null;
            _currentRoomIndex = 0;
            _bannerMessage = null;
            _bannerUntil = 0f;
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
                float acc = 0f;
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
                case "Boss":    BuildBossRoom();    break;
            }
        }

        // --------------------------------------------------------------------
        //  Room builders
        // --------------------------------------------------------------------

        private void BuildMonsterRoom()
        {
            int remaining = 3;
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.PI * 2f / 3f + Mathf.PI / 2f;
                var pos = new Vector3(Mathf.Cos(angle) * 3.5f, Mathf.Sin(angle) * 2.2f, 0f);
                var enemy = SpawnDummyEnemy(pos, 40f, 2f, 0.7f, new Color(0.9f, 0.3f, 0.3f), coinDrop: 4);
                var hp = enemy.GetComponent<Health>();
                hp.OnDied += () =>
                {
                    remaining--;
                    if (remaining <= 0) OpenDoorToNext();
                };
            }
        }

        private void BuildTalentRoom()
        {
            int remaining = 2;
            for (int i = 0; i < 2; i++)
            {
                var pos = new Vector3(-2.5f + 5f * i, 2f, 0f);
                var enemy = SpawnDummyEnemy(pos, 30f, 1f, 0.7f, new Color(0.95f, 0.55f, 0.25f), coinDrop: 3);
                var hp = enemy.GetComponent<Health>();
                hp.OnDied += () =>
                {
                    remaining--;
                    if (remaining <= 0) DropTalentChoices();
                };
            }
        }

        private void DropTalentChoices()
        {
            var defs = new (string name, string desc, StatType stat, ModifierOp op, float value, Color color)[]
            {
                ("Power Up",   "+20% Attack",     StatType.Attack,    ModifierOp.PercentMul, 0.20f, new Color(1f, 0.4f, 0.4f)),
                ("Swift Feet", "+25% Move Speed", StatType.MoveSpeed, ModifierOp.PercentMul, 0.25f, new Color(0.4f, 0.9f, 1f)),
                ("Vigor",      "+50 Max HP",      StatType.MaxHP,     ModifierOp.Flat,       50f,   new Color(1f, 0.9f, 0.3f)),
            };

            var pickups = new List<TalentPickup>();
            foreach (var def in defs)
            {
                var talent = ScriptableObject.CreateInstance<TalentData>();
                talent.talentName = def.name;
                talent.description = def.desc;
                talent.modifiers.Add(new StatModifierEntry { stat = def.stat, op = def.op, value = def.value });
                pickups.Add(SpawnTalentOrb(talent, def.color));
            }
            for (int i = 0; i < pickups.Count; i++)
            {
                pickups[i].transform.position = new Vector3(-3.5f + 3.5f * i, -2.2f, 0f);
            }

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
                var pos = new Vector3(
                    Random.Range(-5f, 5f),
                    Random.Range(-3f, 3f), 0f);
                var coin = SpawnCoinPickup(pos, amount: 6);
                coin.OnPicked += (amt) =>
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

            OpenDoorToNext(); // shopping is optional; leave whenever
        }

        private void BuildMysteryRoom()
        {
            ShowBanner("MYSTERY — approach the altar at your own risk");

            var go = new GameObject("MysteryPedestal");
            go.transform.SetParent(_currentRoomRoot.transform, true);
            go.transform.position = Vector3.zero;
            go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeUnitSquareSprite();
            sr.color = new Color(0.75f, 0.3f, 0.95f);
            sr.sortingOrder = 7;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.7f;
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
            var curse = new StatModifier(StatType.MaxHP, ModifierOp.PercentMul, -0.15f, "Mystery_Curse");
            stats.AddModifier(curse);
        }

        private TalentData GenerateRandomTalent()
        {
            var defs = new (string name, StatType stat, ModifierOp op, float value)[]
            {
                ("Power Up",   StatType.Attack,    ModifierOp.PercentMul, 0.20f),
                ("Swift Feet", StatType.MoveSpeed, ModifierOp.PercentMul, 0.25f),
                ("Vigor",      StatType.MaxHP,     ModifierOp.Flat,       50f),
                ("Guardian",   StatType.Defense,   ModifierOp.Flat,       5f),
            };
            var d = defs[Random.Range(0, defs.Length)];
            var t = ScriptableObject.CreateInstance<TalentData>();
            t.talentName = d.name;
            t.modifiers.Add(new StatModifierEntry { stat = d.stat, op = d.op, value = d.value });
            return t;
        }

        private void BuildBossRoom()
        {
            ShowBanner("BOSS — kill or be killed");
            var boss = SpawnDummyEnemy(new Vector3(0f, 2.5f, 0f), 120f, 3f, 1.6f, new Color(0.55f, 0.08f, 0.12f), coinDrop: 0);
            boss.name = "Boss";
            var stats = boss.GetComponent<CharacterStats>();
            stats.SetBase(StatType.MoveSpeed, 2.2f);

            var ai = boss.AddComponent<ChaseAI>();
            ai.target = _player != null ? _player.transform : null;
            ai.stoppingDistance = 0.95f;
            ai.attackInterval = 1.0f;
            ai.contactDamage = 12f;

            var hp = boss.GetComponent<Health>();
            hp.OnDied += () => { _currentRoomIndex = _floorRooms.Count; TriggerVictory(); };
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
            // Don't open a second door
            if (_currentRoomRoot.transform.Find("Door") != null) return;

            var doorGO = new GameObject("Door");
            doorGO.transform.SetParent(_currentRoomRoot.transform, true);
            doorGO.transform.position = new Vector3(0f, -arenaHalfHeight + 0.4f, 0f);
            doorGO.transform.localScale = new Vector3(1.6f, 0.35f, 1f);

            var sr = doorGO.AddComponent<SpriteRenderer>();
            sr.sprite = MakeUnitSquareSprite();
            sr.color = new Color(0.25f, 0.95f, 0.35f);
            sr.sortingOrder = 8;

            var col = doorGO.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            var door = doorGO.AddComponent<DoorTrigger>();
            int nextIndex = _currentRoomIndex + 1;
            door.OnPlayerEntered += () => LoadRoom(nextIndex);
        }

        // --------------------------------------------------------------------
        //  Spawners
        // --------------------------------------------------------------------

        private void SpawnPlayer(HeroData hero)
        {
            _player = new GameObject("Player_" + hero.heroName);
            _player.transform.position = Vector3.zero;
            _player.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

            var sr = _player.AddComponent<SpriteRenderer>();
            sr.sprite = MakeUnitSquareSprite();
            sr.color = hero.tintColor;
            sr.sortingOrder = 10;

            var rb = _player.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
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
            _playerHealth.OnDamaged += _ => StartCoroutine(FlashRoutine(sr, Color.white, 0.06f));
            _playerHealth.OnDied += () =>
            {
                TriggerDeath();
                Destroy(_player);
            };

            _player.AddComponent<PlayerController>();
        }

        private GameObject SpawnDummyEnemy(Vector3 pos, float maxHp, float defense, float size, Color color, int coinDrop)
        {
            var go = new GameObject("Enemy");
            go.transform.SetParent(_currentRoomRoot.transform, true);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size, size, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeUnitSquareSprite();
            sr.color = color;
            sr.sortingOrder = 5;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.freezeRotation = true;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;

            var stats = go.AddComponent<CharacterStats>();
            stats.SetBase(StatType.MaxHP, maxHp);
            stats.SetBase(StatType.Defense, defense);
            stats.SetBase(StatType.MoveSpeed, 0f);

            var health = go.AddComponent<Health>();
            health.OnDamaged += _ => StartCoroutine(FlashRoutine(sr, Color.white, 0.08f));
            health.OnDied += () =>
            {
                if (coinDrop > 0) RunCoins += coinDrop;
                Destroy(go);
            };
            return go;
        }

        private TalentPickup SpawnTalentOrb(TalentData data, Color color)
        {
            var go = new GameObject("Talent_" + data.talentName);
            go.transform.SetParent(_currentRoomRoot.transform, true);
            go.transform.localScale = new Vector3(0.55f, 0.55f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeUnitSquareSprite();
            sr.color = color;
            sr.sortingOrder = 7;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;
            col.isTrigger = true;

            var pickup = go.AddComponent<TalentPickup>();
            pickup.talent = data;
            return pickup;
        }

        private CoinPickup SpawnCoinPickup(Vector3 pos, int amount)
        {
            var go = new GameObject("Coin");
            go.transform.SetParent(_currentRoomRoot.transform, true);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.3f, 0.3f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeUnitSquareSprite();
            sr.color = new Color(1f, 0.85f, 0.25f);
            sr.sortingOrder = 7;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.6f;
            col.isTrigger = true;

            var coin = go.AddComponent<CoinPickup>();
            coin.amount = amount;
            return coin;
        }

        private ShopPedestal SpawnShopPedestal(Vector3 pos, TalentData talent, Color color, int price)
        {
            var go = new GameObject("Shop_" + talent.talentName);
            go.transform.SetParent(_currentRoomRoot.transform, true);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.7f, 0.7f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeUnitSquareSprite();
            sr.color = color;
            sr.sortingOrder = 7;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.9f;
            col.isTrigger = true;

            var shop = go.AddComponent<ShopPedestal>();
            shop.talent = talent;
            shop.price = price;
            shop.GetCoins = () => RunCoins;
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
            cam.orthographic = true;
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
            MakeWall(new Vector2( arenaHalfWidth + 0.25f, 0f), new Vector2(0.5f, arenaHalfHeight * 2f + 0.5f), wallColor);
            MakeWall(new Vector2(-arenaHalfWidth - 0.25f, 0f), new Vector2(0.5f, arenaHalfHeight * 2f + 0.5f), wallColor);
        }

        private void MakeWall(Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject("Wall");
            go.transform.SetParent(_arenaRoot.transform, true);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeUnitSquareSprite();
            sr.color = color;
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
            _bannerUntil = Time.time + 3f;
        }

        private static Sprite _cachedSquare;
        private static Sprite MakeUnitSquareSprite()
        {
            if (_cachedSquare != null) return _cachedSquare;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
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
                case State.Menu:    DrawMenu(); break;
                case State.Playing: DrawHUD();  break;
                case State.Victory: DrawEndScreen("VICTORY!",  new Color(1f, 0.9f, 0.2f), true); break;
                case State.Death:   DrawEndScreen("YOU DIED",  new Color(1f, 0.3f, 0.3f), false); break;
            }
        }

        private void DrawMenu()
        {
            FillRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.08f, 0.08f, 0.12f));

            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.95f, 0.4f) }
            };
            GUI.Label(new Rect(0, 30, Screen.width, 60), "2D ROGUELIKE", title);

            var info = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(0, 90, Screen.width, 28), $"Unlock Currency: {_persistent.UnlockCurrency}", info);

            float cardW = 260f, cardH = 180f, gap = 16f;
            float totalW = _heroes.Length * cardW + (_heroes.Length - 1) * gap;
            float startX = (Screen.width - totalW) * 0.5f;
            float y = 150f;

            for (int i = 0; i < _heroes.Length; i++)
            {
                var h = _heroes[i];
                bool unlocked = _persistent.IsHeroUnlocked(h.heroName);
                bool selected = i == _selectedHeroIndex;

                var rect = new Rect(startX + i * (cardW + gap), y, cardW, cardH);
                Color bg = !unlocked ? new Color(0.28f, 0.15f, 0.15f)
                         : selected  ? new Color(0.2f, 0.45f, 0.75f)
                                     : new Color(0.18f, 0.22f, 0.3f);
                FillRect(rect, bg);
                FillRect(new Rect(rect.x + 10, rect.y + 10, 40, 40), h.tintColor);

                var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
                GUI.Label(new Rect(rect.x + 60, rect.y + 10, cardW - 70, 30), h.heroName, nameStyle);

                var descStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, normal = { textColor = new Color(0.85f, 0.85f, 0.85f) } };
                GUI.Label(new Rect(rect.x + 10, rect.y + 60, cardW - 20, 40), h.description, descStyle);

                var statStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = new Color(0.8f, 0.9f, 1f) } };
                GUI.Label(new Rect(rect.x + 10, rect.y + 100, cardW - 20, 20),
                    $"HP {h.baseMaxHP:0}   ATK {h.baseAttack:0}   SPD {h.baseMoveSpeed:0.0}", statStyle);

                var btnRect = new Rect(rect.x + 10, rect.y + cardH - 40, cardW - 20, 30);
                if (unlocked)
                {
                    if (GUI.Button(btnRect, selected ? "✓ SELECTED" : "SELECT"))
                        _selectedHeroIndex = i;
                }
                else
                {
                    bool affordable = _persistent.UnlockCurrency >= h.unlockCost;
                    GUI.enabled = affordable;
                    if (GUI.Button(btnRect, $"UNLOCK ({h.unlockCost})"))
                        _persistent.TryUnlockHero(h.heroName, h.unlockCost);
                    GUI.enabled = true;
                }
            }

            var startBtn = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
            bool canStart = _selectedHeroIndex >= 0 && _persistent.IsHeroUnlocked(_heroes[_selectedHeroIndex].heroName);
            GUI.enabled = canStart;
            if (GUI.Button(new Rect(Screen.width / 2f - 140, y + cardH + 30, 280, 54), "START RUN", startBtn))
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
            GUI.Label(new Rect(10, 10, 560, 26),
                $"Floor {CurrentFloor} · Room {_currentRoomIndex + 1}/{_floorRooms.Count} · {roomName}", label);
            GUI.Label(new Rect(10, 34, 560, 26),
                "WASD to move · Space/LMB attack · E to buy · Walk into green door", label);
            if (_playerHealth != null)
            {
                GUI.Label(new Rect(10, 58, 560, 26),
                    $"HP: {Mathf.CeilToInt(_playerHealth.Current)} / {Mathf.CeilToInt(_playerHealth.Max)}   Coins: {RunCoins}",
                    label);
            }

            if (Time.time < _bannerUntil && !string.IsNullOrEmpty(_bannerMessage))
            {
                var bannerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.9f, 0.4f) }
                };
                GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, 40), _bannerMessage, bannerStyle);
            }
        }

        private void DrawEndScreen(string title, Color color, bool victory)
        {
            FillRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.6f));

            var bigStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 56, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
                normal = { textColor = color }
            };
            GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, 80), title, bigStyle);

            if (victory)
            {
                var reward = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
                GUI.Label(new Rect(0, Screen.height * 0.38f, Screen.width, 40),
                    $"+{clearReward} Unlock Currency   (Total: {_persistent.UnlockCurrency})", reward);
            }

            float btnX = Screen.width / 2f - 140f;
            float btnY = Screen.height * 0.55f;
            var btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 18 };
            if (GUI.Button(new Rect(btnX, btnY, 280, 42), "RESTART SAME HERO", btnStyle))
                StartRun();
            if (GUI.Button(new Rect(btnX, btnY + 54, 280, 42), "BACK TO MAIN MENU", btnStyle))
                EnterMenu();
        }
    }
}
