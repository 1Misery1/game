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
    /// Replace DungeonBootstrap with this component on the Bootstrap GameObject.
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private float arenaHalfWidth = 8f;
        [SerializeField] private float arenaHalfHeight = 4.5f;
        [SerializeField] private int clearReward = 50;

        private enum State { Menu, Playing, Victory, Death }
        private static readonly string[] RoomSequence = { "Monster", "Talent", "Boss" };

        private State _state = State.Menu;
        private PersistentState _persistent;
        private HeroData[] _heroes;
        private int _selectedHeroIndex = 0;

        private GameObject _arenaRoot;
        private GameObject _player;
        private Health _playerHealth;
        private GameObject _currentRoomRoot;
        private int _currentRoomIndex;

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
        }

        // --------------------------------------------------------------------
        //  Dungeon flow (same as DungeonBootstrap, with hero stats applied)
        // --------------------------------------------------------------------

        private void LoadRoom(int index)
        {
            if (_currentRoomRoot != null) Destroy(_currentRoomRoot);
            _currentRoomIndex = index;
            if (_player != null) _player.transform.position = Vector3.zero;

            if (index >= RoomSequence.Length)
            {
                TriggerVictory();
                return;
            }

            _currentRoomRoot = new GameObject($"Room_{index}_{RoomSequence[index]}");
            switch (RoomSequence[index])
            {
                case "Monster": BuildMonsterRoom(); break;
                case "Talent":  BuildTalentRoom();  break;
                case "Boss":    BuildBossRoom();    break;
            }
        }

        private void BuildMonsterRoom()
        {
            int remaining = 3;
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.PI * 2f / 3f + Mathf.PI / 2f;
                var pos = new Vector3(Mathf.Cos(angle) * 3.5f, Mathf.Sin(angle) * 2.2f, 0f);
                var enemy = SpawnDummyEnemy(pos, 40f, 2f, 0.7f, new Color(0.9f, 0.3f, 0.3f));
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
                var enemy = SpawnDummyEnemy(pos, 30f, 1f, 0.7f, new Color(0.95f, 0.55f, 0.25f));
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

        private void ApplyTalentToPlayer(TalentData talent)
        {
            if (_player == null) return;
            var stats = _player.GetComponent<CharacterStats>();
            ModifierApplier.ApplyTalent(stats, talent);
            var hp = _player.GetComponent<Health>();
            if (hp != null) hp.Heal(9999f);
            Debug.Log($"[Talent] picked: {talent.talentName}");
        }

        private void BuildBossRoom()
        {
            var boss = SpawnDummyEnemy(new Vector3(0f, 2.5f, 0f), 120f, 3f, 1.6f, new Color(0.55f, 0.08f, 0.12f));
            boss.name = "Boss";
            var stats = boss.GetComponent<CharacterStats>();
            stats.SetBase(StatType.MoveSpeed, 2.2f);

            var ai = boss.AddComponent<ChaseAI>();
            ai.target = _player != null ? _player.transform : null;
            ai.stoppingDistance = 0.95f;
            ai.attackInterval = 1.0f;
            ai.contactDamage = 12f;

            var hp = boss.GetComponent<Health>();
            hp.OnDied += () => { _currentRoomIndex = RoomSequence.Length; TriggerVictory(); };
        }

        private void OpenDoorToNext()
        {
            if (_currentRoomRoot == null) return;
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

        private GameObject SpawnDummyEnemy(Vector3 pos, float maxHp, float defense, float size, Color color)
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
            health.OnDied += () => Destroy(go);
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
                case State.Death:   DrawEndScreen("YOU DIED", new Color(1f, 0.3f, 0.3f), false); break;
            }
        }

        private void DrawMenu()
        {
            FillRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.08f, 0.08f, 0.12f));

            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 44,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.95f, 0.4f) }
            };
            GUI.Label(new Rect(0, 30, Screen.width, 60), "2D ROGUELIKE", title);

            var info = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(0, 90, Screen.width, 28), $"Unlock Currency: {_persistent.UnlockCurrency}", info);

            float cardW = 260f;
            float cardH = 180f;
            float gap = 16f;
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

                // Hero color swatch
                FillRect(new Rect(rect.x + 10, rect.y + 10, 40, 40), h.tintColor);

                var nameStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22, fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
                GUI.Label(new Rect(rect.x + 60, rect.y + 10, cardW - 70, 30), h.heroName, nameStyle);

                var descStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12, wordWrap = true,
                    normal = { textColor = new Color(0.85f, 0.85f, 0.85f) }
                };
                GUI.Label(new Rect(rect.x + 10, rect.y + 60, cardW - 20, 40), h.description, descStyle);

                var statStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    normal = { textColor = new Color(0.8f, 0.9f, 1f) }
                };
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

            var hint = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.6f, 0.6f, 0.7f) }
            };
            GUI.Label(new Rect(0, Screen.height - 28, Screen.width, 20),
                "Save file: " + Application.persistentDataPath, hint);
        }

        private void DrawHUD()
        {
            var label = new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white } };
            GUI.Label(new Rect(10, 10, 480, 26),
                $"Floor 1 · Room {_currentRoomIndex + 1}/{RoomSequence.Length} · {RoomSequence[_currentRoomIndex]}", label);
            GUI.Label(new Rect(10, 34, 480, 26),
                "WASD / Arrows to move · Space / LMB to attack · Walk into green door to advance", label);
            if (_playerHealth != null)
            {
                GUI.Label(new Rect(10, 58, 480, 26),
                    $"HP: {Mathf.CeilToInt(_playerHealth.Current)} / {Mathf.CeilToInt(_playerHealth.Max)}", label);
            }
        }

        private void DrawEndScreen(string title, Color color, bool victory)
        {
            FillRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.6f));

            var bigStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 56,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = color }
            };
            GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, 80), title, bigStyle);

            if (victory)
            {
                var reward = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20, alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
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
