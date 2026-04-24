using System.Collections.Generic;
using Game.AI;
using Game.Combat;
using Game.Data;
using Game.Dungeon;
using Game.Player;
using Game.Systems;
using UnityEngine;

namespace Game.Dev
{
    /// Full 3-room flow: Monster -> Talent -> Boss.
    /// Drop this on an empty GameObject in a fresh scene and press Play.
    public class DungeonBootstrap : MonoBehaviour
    {
        [SerializeField] private float arenaHalfWidth = 8f;
        [SerializeField] private float arenaHalfHeight = 4.5f;

        private static readonly string[] Sequence = { "Monster", "Talent", "Boss" };

        private GameObject _player;
        private Health _playerHealth;
        private GameObject _currentRoomRoot;
        private int _currentRoomIndex;
        private bool _dungeonCleared;
        private bool _playerDied;

        private void Start()
        {
            EnsureCamera();
            BuildArena();
            SpawnPlayer();
            LoadRoom(0);
        }

        private void LoadRoom(int index)
        {
            if (_currentRoomRoot != null) Destroy(_currentRoomRoot);
            _currentRoomIndex = index;
            if (_player != null) _player.transform.position = Vector3.zero;

            if (index >= Sequence.Length)
            {
                _dungeonCleared = true;
                return;
            }

            _currentRoomRoot = new GameObject($"Room_{index}_{Sequence[index]}");
            switch (Sequence[index])
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
                ("Power Up",   "+20% Attack",      StatType.Attack,    ModifierOp.PercentMul, 0.20f, new Color(1f, 0.4f, 0.4f)),
                ("Swift Feet", "+25% Move Speed",  StatType.MoveSpeed, ModifierOp.PercentMul, 0.25f, new Color(0.4f, 0.9f, 1f)),
                ("Vigor",      "+50 Max HP",       StatType.MaxHP,     ModifierOp.Flat,       50f,   new Color(1f, 0.9f, 0.3f)),
            };

            var pickups = new List<TalentPickup>();
            foreach (var def in defs)
            {
                var talent = ScriptableObject.CreateInstance<TalentData>();
                talent.talentName = def.name;
                talent.description = def.desc;
                talent.modifiers.Add(new StatModifierEntry
                {
                    stat = def.stat, op = def.op, value = def.value
                });
                pickups.Add(SpawnTalentOrb(talent, def.color));
            }

            // Lay pickups in a row
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
                    {
                        if (other != null && other != self) Destroy(other.gameObject);
                    }
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
            if (hp != null) hp.Heal(9999f); // top off after +MaxHP
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
            hp.OnDied += () => { _dungeonCleared = true; };
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

        private void SpawnPlayer()
        {
            _player = new GameObject("Player");
            _player.transform.position = Vector3.zero;
            _player.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

            var sr = _player.AddComponent<SpriteRenderer>();
            sr.sprite = MakeUnitSquareSprite();
            sr.color = new Color(0.4f, 0.85f, 1f);
            sr.sortingOrder = 10;

            var rb = _player.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = _player.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;

            _player.AddComponent<CharacterStats>();
            _playerHealth = _player.AddComponent<Health>();
            _playerHealth.OnDamaged += _ => StartCoroutine(FlashRoutine(sr, Color.white, 0.06f));
            _playerHealth.OnDied += () =>
            {
                _playerDied = true;
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
            var wallColor = new Color(0.3f, 0.3f, 0.35f);
            MakeWall(new Vector2(0f,  arenaHalfHeight + 0.25f), new Vector2(arenaHalfWidth * 2f + 0.5f, 0.5f), wallColor);
            MakeWall(new Vector2(0f, -arenaHalfHeight - 0.25f), new Vector2(arenaHalfWidth * 2f + 0.5f, 0.5f), wallColor);
            MakeWall(new Vector2( arenaHalfWidth + 0.25f, 0f), new Vector2(0.5f, arenaHalfHeight * 2f + 0.5f), wallColor);
            MakeWall(new Vector2(-arenaHalfWidth - 0.25f, 0f), new Vector2(0.5f, arenaHalfHeight * 2f + 0.5f), wallColor);
        }

        private void MakeWall(Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject("Wall");
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeUnitSquareSprite();
            sr.color = color;
            sr.sortingOrder = 0;
            go.AddComponent<BoxCollider2D>();
        }

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

        private void OnGUI()
        {
            var label = new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white } };

            if (!_dungeonCleared && !_playerDied && _currentRoomIndex < Sequence.Length)
            {
                GUI.Label(new Rect(10, 10, 480, 26),
                    $"Floor 1 · Room {_currentRoomIndex + 1}/{Sequence.Length} · {Sequence[_currentRoomIndex]}",
                    label);
                GUI.Label(new Rect(10, 34, 480, 26),
                    "WASD / Arrows to move · Space / LMB to attack · Walk into green door to advance",
                    label);
                if (_playerHealth != null)
                {
                    GUI.Label(new Rect(10, 58, 480, 26),
                        $"HP: {Mathf.CeilToInt(_playerHealth.Current)} / {Mathf.CeilToInt(_playerHealth.Max)}",
                        label);
                }
            }

            if (_dungeonCleared)
            {
                DrawCenteredBig("VICTORY!", Color.yellow);
            }
            else if (_playerDied)
            {
                DrawCenteredBig("YOU DIED", new Color(1f, 0.3f, 0.3f));
            }
        }

        private void DrawCenteredBig(string text, Color color)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = color }
            };
            GUI.Label(new Rect(0f, Screen.height * 0.4f, Screen.width, 80f), text, style);
        }
    }
}
