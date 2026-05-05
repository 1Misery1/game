using System.Collections;
using Game.Combat;
using Game.Data;
using UnityEngine;

namespace Game.Player
{
    public class HeroActiveSkillHandler : MonoBehaviour
    {
        public HeroSkillType SkillType   = HeroSkillType.None;
        public float         Cooldown    = 8f;
        public string        SkillName   = "";

        public float CooldownRemaining { get; private set; }
        public bool  IsReady           => CooldownRemaining <= 0f && SkillType != HeroSkillType.None;
        public float CooldownRatio     => Cooldown > 0f ? Mathf.Clamp01(CooldownRemaining / Cooldown) : 0f;

        private CharacterStats _stats;
        private Health         _health;

        private void Awake()
        {
            _stats  = GetComponent<CharacterStats>();
            _health = GetComponent<Health>();
        }

        private void Update()
        {
            if (CooldownRemaining > 0f)
                CooldownRemaining -= Time.deltaTime;
        }

        public bool TryUse(Vector2 aimDir)
        {
            if (!IsReady) return false;
            CooldownRemaining = Cooldown;
            Execute(aimDir);
            return true;
        }

        private void Execute(Vector2 aimDir)
        {
            switch (SkillType)
            {
                case HeroSkillType.WarCry:        UseWarCry();               break;
                case HeroSkillType.ShadowStep:    UseShadowStep(aimDir);     break;
                case HeroSkillType.ArcaneSurge:   UseArcaneSurge();          break;
                case HeroSkillType.HolyLight:     UseHolyLight();            break;
                case HeroSkillType.PrecisionShot: UsePrecisionShot(aimDir);  break;
            }
        }

        // 战吼：AoE物理伤害 + 临时攻击加成
        private void UseWarCry()
        {
            DamageRadius(_stats.Get(StatType.Attack) * 1.5f, 3f, DamageType.Physical, false);
            StartCoroutine(TempModifier(StatType.Attack, ModifierOp.PercentMul, 0.30f, 5f));
        }

        // 影步：向瞄准方向冲刺 + 对路径上敌人造成伤害
        private void UseShadowStep(Vector2 aimDir)
        {
            if (aimDir == Vector2.zero) aimDir = Vector2.right;
            DamageRadius(_stats.Get(StatType.Attack) * 0.8f, 1.2f, DamageType.Physical, false);
            transform.position += (Vector3)(aimDir * 4f);
        }

        // 奥术迸发：大范围魔法AOE
        private void UseArcaneSurge()
        {
            DamageRadius(_stats.Get(StatType.Attack) * 2f, 4f, DamageType.Magical, true);
        }

        // 神圣之光：回复40%最大HP
        private void UseHolyLight()
        {
            if (_health != null)
                _health.Heal(_stats.Get(StatType.MaxHP) * 0.4f);
        }

        // 精准射击：穿透直线上所有敌人，250%伤害
        private void UsePrecisionShot(Vector2 aimDir)
        {
            if (aimDir == Vector2.zero) aimDir = Vector2.right;
            float dmg = _stats.Get(StatType.Attack) * 2.5f;
            bool isCrit = Random.value < _stats.Get(StatType.CritRate);
            if (isCrit) dmg *= _stats.Get(StatType.CritDamage);

            var hits = Physics2D.RaycastAll(transform.position, aimDir, 20f);
            foreach (var hit in hits)
            {
                if (hit.collider.gameObject == gameObject) continue;
                hit.collider.GetComponent<IDamageable>()?.TakeDamage(new DamageInfo
                {
                    Amount = dmg, Type = DamageType.Physical, IsCrit = isCrit, Source = gameObject
                });
            }
        }

        private void DamageRadius(float dmg, float radius, DamageType type, bool forceCrit)
        {
            bool isCrit = forceCrit || Random.value < _stats.Get(StatType.CritRate);
            if (isCrit) dmg *= _stats.Get(StatType.CritDamage);
            foreach (var col in Physics2D.OverlapCircleAll(transform.position, radius))
            {
                if (col.gameObject == gameObject) continue;
                col.GetComponent<IDamageable>()?.TakeDamage(new DamageInfo
                {
                    Amount = dmg, Type = type, IsCrit = isCrit, Source = gameObject
                });
            }
        }

        private IEnumerator TempModifier(StatType stat, ModifierOp op, float value, float duration)
        {
            var source = new object();
            _stats?.AddModifier(new StatModifier(stat, op, value, source));
            yield return new WaitForSeconds(duration);
            _stats?.RemoveModifiersFrom(source);
        }
    }
}
