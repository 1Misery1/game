using Game.Combat;
using Game.Data;
using UnityEngine;

namespace Game.Player
{
    // 武器技能执行器：根据技能类型执行对应的技能效果
    public static class WeaponSkillExecutor
    {
        public static void Execute(WeaponInstance weapon, CharacterStats stats, Transform origin, Vector2 aimDir)
        {
            if (!weapon.Data.HasSkill) return;

            float baseDmg = weapon.EffectiveDamage + stats.Get(StatType.Attack);
            float skillMul = weapon.EffectiveSkillMultiplier * stats.Get(StatType.SkillPower);
            float totalDmg = baseDmg * skillMul;
            var skill = weapon.Data.skill;

            if (aimDir == Vector2.zero) aimDir = Vector2.right;

            switch (skill.skillType)
            {
                case WeaponSkillType.VenomSpray:   DoVenomSpray(totalDmg, origin, aimDir, skill);   break;
                case WeaponSkillType.PhantomSlash: DoPhantomSlash(totalDmg, origin, skill);          break;
                case WeaponSkillType.HolyStrike:   DoHolyStrike(totalDmg, stats, origin, aimDir, skill); break;
                case WeaponSkillType.AbyssWave:    DoAbyssWave(totalDmg, origin, aimDir, skill);     break;
                case WeaponSkillType.EarthShatter: DoEarthShatter(totalDmg, origin, skill);          break;
                case WeaponSkillType.DoomFall:     DoDoomFall(totalDmg, origin, skill);              break;
                case WeaponSkillType.PiercingArrow:DoPiercingArrow(totalDmg, origin, aimDir, skill); break;
                case WeaponSkillType.RainOfArrows: DoRainOfArrows(totalDmg, origin, aimDir, skill);  break;
                case WeaponSkillType.FrostNova:    DoFrostNova(totalDmg, origin, skill);             break;
                case WeaponSkillType.ChaosBurst:   DoChaosBurst(totalDmg, origin, aimDir, skill);    break;
            }
        }

        // 毒牙·毒液喷射：向前方喷洒毒液，造成AOE物理伤害，追加40%真实伤害模拟毒素效果
        private static void DoVenomSpray(float dmg, Transform origin, Vector2 dir, WeaponSkillData skill)
        {
            Vector2 center = (Vector2)origin.position + dir * 1.8f;
            HitCircle(center, skill.skillRadius, dmg, DamageType.Physical, origin.gameObject);
            HitCircle(center, skill.skillRadius, dmg * 0.4f, DamageType.True, origin.gameObject);
        }

        // 幻影之刃·幻影连斩：快速三连击，总伤害分3段，每段造成(1/3)伤害
        private static void DoPhantomSlash(float totalDmg, Transform origin, WeaponSkillData skill)
        {
            int hits = skill.skillHitCount > 0 ? skill.skillHitCount : 1;
            float perHit = totalDmg / hits;
            for (int i = 0; i < hits; i++)
                HitCircle(origin.position, skill.skillRadius, perHit, DamageType.Physical, origin.gameObject);
        }

        // 圣光剑·圣光斩：前方一击造成物理伤害，同时附加30%真实伤害并恢复10%最大生命值
        private static void DoHolyStrike(float dmg, CharacterStats stats, Transform origin, Vector2 dir, WeaponSkillData skill)
        {
            Vector2 center = (Vector2)origin.position + dir * 2f;
            HitCircle(center, skill.skillRadius, dmg, DamageType.Physical, origin.gameObject);
            HitCircle(center, skill.skillRadius, dmg * 0.3f, DamageType.True, origin.gameObject);
            var health = origin.GetComponent<Health>();
            if (health != null)
                health.Heal(stats.Get(StatType.MaxHP) * 0.1f);
        }

        // 龙渊剑·龙渊斩波：向前方释放斩波，对扇形区域造成伤害（以前方为中心的宽范围圆形模拟）
        private static void DoAbyssWave(float dmg, Transform origin, Vector2 dir, WeaponSkillData skill)
        {
            Vector2 center = (Vector2)origin.position + dir * (skill.skillRange * 0.5f);
            HitCircle(center, skill.skillRadius, dmg, DamageType.Physical, origin.gameObject);
        }

        // 破甲重剑·大地震荡：以自身为中心大范围震荡，造成AOE物理伤害
        private static void DoEarthShatter(float dmg, Transform origin, WeaponSkillData skill)
        {
            HitCircle(origin.position, skill.skillRadius, dmg, DamageType.Physical, origin.gameObject);
        }

        // 末日巨剑·毁灭天降：天空之力降临，覆盖整个场地造成毁灭性伤害
        private static void DoDoomFall(float dmg, Transform origin, WeaponSkillData skill)
        {
            HitCircle(origin.position, skill.skillRadius, dmg, DamageType.Physical, origin.gameObject);
        }

        // 穿云弓·穿云箭：射出穿透之矢，沿直线贯穿所有敌人
        private static void DoPiercingArrow(float dmg, Transform origin, Vector2 dir, WeaponSkillData skill)
        {
            var hits = Physics2D.RaycastAll(origin.position, dir, skill.skillRange);
            foreach (var hit in hits)
            {
                if (hit.collider.gameObject == origin.gameObject) continue;
                hit.collider.GetComponent<IDamageable>()?.TakeDamage(new DamageInfo
                {
                    Amount = dmg, Type = DamageType.Physical, IsCrit = false, Source = origin.gameObject
                });
            }
        }

        // 天风弓·箭雨：在目标区域降下多支箭，每支造成等额伤害
        private static void DoRainOfArrows(float totalDmg, Transform origin, Vector2 dir, WeaponSkillData skill)
        {
            int hits = skill.skillHitCount > 0 ? skill.skillHitCount : 5;
            float perHit = totalDmg / hits;
            Vector2 center = (Vector2)origin.position + dir * Mathf.Min(skill.skillRange, 5f);
            for (int i = 0; i < hits; i++)
            {
                Vector2 offset = Random.insideUnitCircle * skill.skillRadius * 0.6f;
                HitCircle(center + offset, 1.2f, perHit, DamageType.Physical, origin.gameObject);
            }
        }

        // 寒冰法杖·冰霜新星：以自身为中心爆发冰霜，造成AOE魔法伤害，追加真实伤害模拟冰冻效果
        private static void DoFrostNova(float dmg, Transform origin, WeaponSkillData skill)
        {
            HitCircle(origin.position, skill.skillRadius, dmg, DamageType.Magical, origin.gameObject);
            HitCircle(origin.position, skill.skillRadius, dmg * 0.25f, DamageType.True, origin.gameObject);
        }

        // 混沌魔杖·混沌爆发：在目标位置释放混沌能量，造成AOE魔法伤害，并附加随机元素追加伤害
        private static void DoChaosBurst(float dmg, Transform origin, Vector2 dir, WeaponSkillData skill)
        {
            Vector2 center = (Vector2)origin.position + dir * Mathf.Min(skill.skillRange, 5f);
            HitCircle(center, skill.skillRadius, dmg, DamageType.Magical, origin.gameObject);
            // 随机元素：0=火(Physical), 1=雷(Magical), 2=毒(True)
            var bonusType = (DamageType)Random.Range(0, 3);
            HitCircle(center, skill.skillRadius * 0.7f, dmg * 0.45f, bonusType, origin.gameObject);
        }

        private static void HitCircle(Vector2 center, float radius, float damage, DamageType type, GameObject source)
        {
            var cols = Physics2D.OverlapCircleAll(center, radius);
            foreach (var col in cols)
            {
                if (col.gameObject == source) continue;
                col.GetComponent<IDamageable>()?.TakeDamage(new DamageInfo
                {
                    Amount = damage, Type = type, IsCrit = false, Source = source
                });
            }
        }
    }
}
