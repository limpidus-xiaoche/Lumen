namespace Launcher.App.ViewModels;

/// <summary>常见模组的英文 slug → 中文名对照表（PCL2 风格的中英并显）。</summary>
public static class ModTranslations
{
    private static readonly Dictionary<string, string> Zh = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sodium"] = "钠",
        ["sodium-extra"] = "钠 · 扩展",
        ["lithium"] = "锂",
        ["phosphor"] = "磷",
        ["iris"] = "鸢尾花光影",
        ["indium"] = "铟",
        ["starlight"] = "星光",
        ["ferritecore"] = "铁氧体核心",
        ["memoryleakfix"] = "内存泄漏修复",
        ["entityculling"] = "实体剔除",
        ["immediatelyfast"] = "即时渲染优化",
        ["clumps"] = "经验球合并",
        ["fabric-api"] = "Fabric API",
        ["fabric-language-kotlin"] = "Kotlin 前置",
        ["architectury"] = "Architectury 前置",
        ["modmenu"] = "模组菜单",
        ["betterf3"] = "更好的 F3",
        ["jade"] = "Jade 信息显示",
        ["wthit"] = "WTHIT 信息显示",
        ["jei"] = "JEI 物品管理器",
        ["rei"] = "REI 物品管理器",
        ["roughly-enough-items"] = "REI 物品管理器",
        ["appleskin"] = "苹果皮",
        ["journeymap"] = "旅行地图",
        ["xaeros-minimap"] = "Xaero 小地图",
        ["xaeros-world-map"] = "Xaero 世界地图",
        ["create"] = "机械动力",
        ["twilightforest"] = "暮色森林",
        ["botania"] = "植物魔法",
        ["mekanism"] = "通用机械",
        ["ae2"] = "应用能源 2",
        ["thermal-expansion"] = "热力膨胀",
        ["immersive-engineering"] = "沉浸工程",
        ["farmers-delight"] = "农夫乐事",
        ["quark"] = "夸克",
        ["supplementaries"] = "附属",
        ["waystones"] = "传送石碑",
        ["lambdynamiclights"] = "动态光源",
        ["fallingleaves"] = "落叶",
        ["notenoughanimations"] = "更多动画",
        ["continuity"] = "连续性",
        ["optifine"] = "高清修复",
        ["sodiumdynamiclights"] = "动态光源",
        ["travelers-backpack"] = "旅行者背包",
        ["backpacked"] = "背包",
        ["carry-on"] = "搬运",
    };

    /// <summary>返回中文名；未收录时回退英文原名。</summary>
    public static string Translate(string slug, string title) =>
        Zh.TryGetValue(slug, out var name) ? name : title;
}
