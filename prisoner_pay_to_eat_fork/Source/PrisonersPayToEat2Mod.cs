using HarmonyLib;
using Verse;
using System.Reflection;

namespace PrisonersPayToEat2
{
    public class PrisonersPayToEat2Mod : Mod
    {
        public const string HarmonyId = "aaa.prisonerspaytoeat2";
        public const string ModVersion = "2.4.0";

        public static PrisonersPayToEat2Settings Settings;
        public static PrisonersPayToEat2Mod Instance;

        public PrisonersPayToEat2Mod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<PrisonersPayToEat2Settings>();

            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            // 劳作区殖民者避让、卫生与温控（Dubs Bad Hygiene）软兼容都不在这里打补丁：
            // 两者都要枚举 / 补丁其他 MOD 的类型，而触碰别的 MOD 的方法会触发它的静态构造器。
            // Mod 构造函数阶段 Defs 还没加载 —— RJW 在静态字段里解析约 110 个 Def，一旦此时
            // 执行就全部缓存成 null，之后生成 Pawn 时抛 NullReferenceException，世界 / 地图
            // 生成与渲染一起崩。改由 PPTELateInit（[StaticConstructorOnStartup]，Defs 加载之后）
            // 执行，时序上仍然早于任何地图和 Pawn 的创建。

            Log.Message($"[PrisonersPayToEat2] v{ModVersion} loaded (RimWorld 1.7). PrisonLabor={PrisonLaborBridge.Present}, OrganHarvest={Settings.enableOrganHarvest}, FoodMul={Settings.foodPriceMultiplier}, WageMul={Settings.wageMultiplier}");
        }

        public override string SettingsCategory() => "PPTE2_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }
    }
}