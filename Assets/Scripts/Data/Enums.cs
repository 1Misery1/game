namespace Game.Data
{
    // 武器稀有度：白色/绿色无技能，蓝色/紫色有技能可附魔
    public enum WeaponRarity
    {
        White,   // 白色 - 初始武器，无技能
        Green,   // 绿色 - 进阶武器，无技能
        Blue,    // 蓝色 - 精良武器，有技能，可附魔
        Purple,  // 紫色 - 史诗武器，有技能，可附魔
    }

    // 武器类别：近战三类 + 远程两类
    public enum WeaponCategory
    {
        Dagger,     // 匕首 - 快速近战
        Longsword,  // 长剑 - 均衡近战
        Greatsword, // 双手剑 - 缓慢强力近战
        Bow,        // 弓 - 物理远程
        Staff,      // 法杖 - 魔法远程
    }

    // 武器技能类型（对应每个蓝/紫武器的独特技能）
    public enum WeaponSkillType
    {
        None,
        VenomSpray,      // 毒液喷射 (毒牙匕首)
        PhantomSlash,    // 幻影连斩 (幻影之刃)
        HolyStrike,      // 圣光斩   (圣光剑)
        AbyssWave,       // 龙渊斩波 (龙渊剑)
        EarthShatter,    // 大地震荡 (破甲重剑)
        DoomFall,        // 毁灭天降 (末日巨剑)
        PiercingArrow,   // 穿云箭   (穿云弓)
        RainOfArrows,    // 箭雨     (天风弓)
        FrostNova,       // 冰霜新星 (寒冰法杖)
        ChaosBurst,      // 混沌爆发 (混沌魔杖)
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
        Elite,   // 精英房间：1精英 + 2小怪
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

    // 英雄主动技能类型
    public enum HeroSkillType
    {
        None,
        WarCry,         // 战吼      (战士)
        ShadowStep,     // 影步      (游侠)
        ArcaneSurge,    // 奥术迸发  (法师)
        HolyLight,      // 神圣之光  (圣骑士)
        PrecisionShot,  // 精准射击  (猎人)
    }

    // 英雄被动天赋类型
    public enum HeroPassiveType
    {
        None,
        BattlefieldWill,    // 战场意志  (战士)  HP降至30%触发爆发回血
        ComboStrike,        // 连击      (游侠)  连续攻击叠加最多5层攻击加成
        ManaAmplification,  // 魔力增幅  (法师)  使用武器技能后下次普攻加倍
        SacredOath,         // 神圣誓约  (圣骑士) 击杀敌人回复5HP
        EagleEye,           // 鹰眼      (猎人)  永久暴击率+20%、暴击伤害+30%
    }
}
