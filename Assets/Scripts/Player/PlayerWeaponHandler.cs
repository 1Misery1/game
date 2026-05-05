using Game.Combat;
using Game.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    // 管理玩家的两把武器槽位、普攻、技能释放、武器切换
    [RequireComponent(typeof(CharacterStats))]
    public class PlayerWeaponHandler : MonoBehaviour
    {
        public WeaponInstance[] Slots = new WeaponInstance[2];  // 武器槽，最多2把
        public int ActiveSlotIndex { get; private set; } = 0;

        private CharacterStats _stats;
        private float _lastAttackTime;
        private float _skillCooldownRemaining;

        public WeaponInstance ActiveWeapon => Slots[ActiveSlotIndex];

        // 技能冷却进度（0=可用，1=冷却中）
        public float SkillCooldownRatio
        {
            get
            {
                float cd = RawSkillCooldown;
                return cd > 0f ? Mathf.Clamp01(_skillCooldownRemaining / cd) : 0f;
            }
        }

        public bool SkillReady => _skillCooldownRemaining <= 0f && ActiveWeapon?.Data?.HasSkill == true;

        public float SkillCooldownRemaining => _skillCooldownRemaining;

        private void Awake()
        {
            _stats = GetComponent<CharacterStats>();
        }

        private void Update()
        {
            if (_skillCooldownRemaining > 0f)
                _skillCooldownRemaining -= Time.deltaTime;

            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
                SwitchWeapon();
        }

        public void SwitchWeapon()
        {
            if (Slots[0] == null && Slots[1] == null) return;
            // 切换到另一槽位（跳过空槽）
            int next = 1 - ActiveSlotIndex;
            if (Slots[next] != null) ActiveSlotIndex = next;
        }

        public void EquipWeapon(WeaponInstance weapon, int slot)
        {
            if (slot < 0 || slot >= Slots.Length) return;
            Slots[slot] = weapon;
        }

        // 普通攻击
        public bool TryAttack(Vector2 aimDir)
        {
            var weapon = ActiveWeapon;

            // 无武器时进行徒手攻击（回退行为）
            if (weapon == null)
            {
                float punchInterval = 0.5f;
                if (Time.time < _lastAttackTime + punchInterval) return false;
                _lastAttackTime = Time.time;
                PunchAttack();
                return true;
            }

            float atkSpeed = weapon.Data.attackSpeed * Mathf.Max(_stats.Get(StatType.AttackSpeed), 0.1f);
            float interval = 1f / atkSpeed;
            if (Time.time < _lastAttackTime + interval) return false;

            _lastAttackTime = Time.time;
            ExecuteNormalAttack(weapon, aimDir);
            return true;
        }

        // 技能攻击
        public bool TryUseSkill(Vector2 aimDir)
        {
            var weapon = ActiveWeapon;
            if (weapon == null || !weapon.Data.HasSkill) return false;
            if (_skillCooldownRemaining > 0f) return false;

            _skillCooldownRemaining = RawSkillCooldown;
            WeaponSkillExecutor.Execute(weapon, _stats, transform, aimDir);
            return true;
        }

        private void PunchAttack()
        {
            float dmg = _stats.Get(StatType.Attack) + 8f;
            var cols = Physics2D.OverlapCircleAll(transform.position, 1.2f);
            foreach (var col in cols)
            {
                if (col.gameObject == gameObject) continue;
                col.GetComponent<IDamageable>()?.TakeDamage(new DamageInfo
                {
                    Amount = dmg, Type = DamageType.Physical, IsCrit = false, Source = gameObject
                });
            }
        }

        private void ExecuteNormalAttack(WeaponInstance weapon, Vector2 aimDir)
        {
            float damage = weapon.EffectiveDamage + _stats.Get(StatType.Attack);
            bool isCrit = Random.value < _stats.Get(StatType.CritRate);
            if (isCrit) damage *= _stats.Get(StatType.CritDamage);
            var type = weapon.Data.damageType;

            switch (weapon.Data.category)
            {
                case WeaponCategory.Dagger:
                case WeaponCategory.Longsword:
                case WeaponCategory.Greatsword:
                    MeleeAttack(damage, weapon.Data.attackRange, type, isCrit);
                    break;
                case WeaponCategory.Bow:
                    RangedAttack(damage, aimDir, weapon.Data.attackRange, type, isCrit);
                    break;
                case WeaponCategory.Staff:
                    float aoe = weapon.Data.aoeRadius > 0f ? weapon.Data.aoeRadius : 1.2f;
                    MagicBlast(damage, aimDir, weapon.Data.attackRange, aoe, type, isCrit);
                    break;
            }
        }

        private void MeleeAttack(float damage, float range, DamageType type, bool isCrit)
        {
            var cols = Physics2D.OverlapCircleAll(transform.position, range);
            foreach (var col in cols)
            {
                if (col.gameObject == gameObject) continue;
                col.GetComponent<IDamageable>()?.TakeDamage(new DamageInfo
                {
                    Amount = damage, Type = type, IsCrit = isCrit, Source = gameObject
                });
            }
        }

        private void RangedAttack(float damage, Vector2 dir, float range, DamageType type, bool isCrit)
        {
            if (dir == Vector2.zero) dir = Vector2.right;
            var hits = Physics2D.RaycastAll(transform.position, dir, range);
            foreach (var hit in hits)
            {
                if (hit.collider.gameObject == gameObject) continue;
                var d = hit.collider.GetComponent<IDamageable>();
                if (d != null)
                {
                    d.TakeDamage(new DamageInfo { Amount = damage, Type = type, IsCrit = isCrit, Source = gameObject });
                    break; // 普通箭矢命中第一个目标后停止
                }
            }
        }

        private void MagicBlast(float damage, Vector2 dir, float range, float radius, DamageType type, bool isCrit)
        {
            if (dir == Vector2.zero) dir = Vector2.right;
            Vector2 target = (Vector2)transform.position + dir * Mathf.Min(range, 6f);
            var cols = Physics2D.OverlapCircleAll(target, radius);
            foreach (var col in cols)
            {
                if (col.gameObject == gameObject) continue;
                col.GetComponent<IDamageable>()?.TakeDamage(new DamageInfo
                {
                    Amount = damage, Type = type, IsCrit = isCrit, Source = gameObject
                });
            }
        }

        private float RawSkillCooldown
        {
            get
            {
                if (ActiveWeapon?.Data?.HasSkill != true) return 0f;
                float cdr = Mathf.Clamp01(_stats.Get(StatType.CooldownReduction));
                return ActiveWeapon.Data.skill.cooldown * (1f - cdr);
            }
        }
    }
}
