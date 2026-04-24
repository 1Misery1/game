using Game.Combat;
using Game.Data;

namespace Game.Systems
{
    public static class ModifierApplier
    {
        public static void ApplyTalent(CharacterStats stats, TalentData talent)
        {
            if (stats == null || talent == null) return;
            foreach (var entry in talent.modifiers)
            {
                stats.AddModifier(new StatModifier(entry.stat, entry.op, entry.value, talent));
            }
        }

        public static void ApplyBuff(CharacterStats stats, BuffData buff)
        {
            if (stats == null || buff == null) return;
            foreach (var entry in buff.modifiers)
            {
                stats.AddModifier(new StatModifier(entry.stat, entry.op, entry.value, buff));
            }
        }

        public static void ApplyPassive(CharacterStats stats, PassiveTalentData passive)
        {
            if (stats == null || passive == null) return;
            foreach (var entry in passive.modifiers)
            {
                stats.AddModifier(new StatModifier(entry.stat, entry.op, entry.value, passive));
            }
        }

        public static void Remove(CharacterStats stats, object source)
        {
            if (stats == null) return;
            stats.RemoveModifiersFrom(source);
        }
    }
}
