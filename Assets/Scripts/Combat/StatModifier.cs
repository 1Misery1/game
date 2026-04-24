using Game.Data;

namespace Game.Combat
{
    public sealed class StatModifier
    {
        public readonly StatType Stat;
        public readonly ModifierOp Op;
        public readonly float Value;
        public readonly object Source;

        public StatModifier(StatType stat, ModifierOp op, float value, object source = null)
        {
            Stat = stat;
            Op = op;
            Value = value;
            Source = source;
        }
    }
}
