using Game.Combat;
using Game.Data;
using Game.Player;
using UnityEngine;

namespace Game.Dev
{
    /// Runtime scaffold: procedurally builds a playable demo so we can see the
    /// core systems working without authoring scenes/prefabs in the Editor yet.
    public class TestBootstrap : MonoBehaviour
    {
        [SerializeField] private int enemyCount = 5;
        [SerializeField] private float arenaHalfWidth = 8f;
        [SerializeField] private float arenaHalfHeight = 4.5f;

        private GameObject _player;

        private void Start()
        {
            EnsureCamera();
            BuildArena();
            SpawnPlayer(Vector3.zero);
            for (int i = 0; i < enemyCount; i++)
            {
                float angle = (i / (float)enemyCount) * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(angle) * 3f, Mathf.Sin(angle) * 2f, 0f);
                SpawnEnemy(pos);
            }
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

        private void SpawnPlayer(Vector3 pos)
        {
            _player = new GameObject("Player");
            _player.transform.position = pos;

            var sr = _player.AddComponent<SpriteRenderer>();
            sr.sprite = MakeUnitSquareSprite();
            sr.color = new Color(0.4f, 0.85f, 1f);
            sr.sortingOrder = 10;
            _player.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

            var rb = _player.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = _player.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;

            _player.AddComponent<CharacterStats>();
            var hp = _player.AddComponent<Health>();
            hp.OnDied += () =>
            {
                Debug.Log("[TestBootstrap] Player died.");
                Destroy(_player);
            };

            _player.AddComponent<PlayerController>();
        }

        private void SpawnEnemy(Vector3 pos)
        {
            var go = new GameObject("Enemy");
            go.transform.position = pos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeUnitSquareSprite();
            sr.color = new Color(0.9f, 0.3f, 0.3f);
            sr.sortingOrder = 5;
            go.transform.localScale = new Vector3(0.7f, 0.7f, 1f);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;

            var stats = go.AddComponent<CharacterStats>();
            stats.SetBase(StatType.MaxHP, 40f);
            stats.SetBase(StatType.Defense, 2f);
            stats.SetBase(StatType.MoveSpeed, 0f);

            var hp = go.AddComponent<Health>();
            hp.OnDamaged += info => StartFlash(sr, Color.white);
            hp.OnDied += () => Destroy(go);
        }

        private void StartFlash(SpriteRenderer sr, Color flashColor)
        {
            StartCoroutine(FlashRoutine(sr, flashColor));
        }

        private System.Collections.IEnumerator FlashRoutine(SpriteRenderer sr, Color flashColor)
        {
            if (sr == null) yield break;
            var original = sr.color;
            sr.color = flashColor;
            yield return new WaitForSeconds(0.08f);
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
    }
}
