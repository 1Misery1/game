namespace Game.Data
{
    public enum WeaponRarity
    {
        Common,
        Rare,
        Elite,
        Boss
    }

    public enum DamageType
    {
        Physical,
        Magical,
        True
    }

    public enum RoomType
    {
        Monster,
        Talent,
        Coin,
        Shop,
        Mystery,
        Boss
    }

    public enum StatType
    {
        MaxHP,
        Attack,
        Defense,
        MoveSpeed,
        AttackSpeed,
        CritRate,
        CritDamage,
        SkillPower,
        CooldownReduction,
        CoinGain
    }

    public enum ModifierOp
    {
        Flat,
        PercentAdd,
        PercentMul
    }
}
