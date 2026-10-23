using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace HungerAndHavoc.Pawn.Compat
{
    internal static class RHAH_CombatExtendedLoadout
    {
        static bool resolved;
        static bool failed;
        static Action<Verse.Pawn, float> generate;

        internal static void Apply(Verse.Pawn pawn)
        {
            if (ModLister.GetActiveModWithIdentifier("CETeam.CombatExtended", false) == null)
            {
                return;
            }

            try
            {
                if (!resolved)
                {
                    resolved = true;
                    Type type = AccessTools.TypeByName("CombatExtended.LoadoutPropertiesExtension");
                    ConstructorInfo constructor = type?.GetConstructor(Type.EmptyTypes);
                    FieldInfo magazines = type?.GetField("primaryMagazineCount", BindingFlags.Instance | BindingFlags.Public);
                    MethodInfo method = type?.GetMethod("GenerateLoadoutFor", BindingFlags.Instance | BindingFlags.Public,
                        null, new[] { typeof(Verse.Pawn), typeof(float) }, null);
                    if (type == null || !type.IsPublic || !typeof(DefModExtension).IsAssignableFrom(type) || constructor == null ||
                        magazines?.FieldType != typeof(FloatRange) || method?.ReturnType != typeof(void))
                    {
                        throw new InvalidOperationException("CE public loadout API does not match the supported contract");
                    }

                    // 独立配置只给当前主武器装弹和一匣备用弹 不重跑角色的副武器或护盾生成
                    object loadout = constructor.Invoke(null);
                    magazines.SetValue(loadout, new FloatRange(1f, 1f));
                    generate = (Action<Verse.Pawn, float>)Delegate.CreateDelegate(typeof(Action<Verse.Pawn, float>), loadout, method);
                }

                if (generate == null)
                {
                    return;
                }

                generate(pawn, 0f);
            }
            catch (Exception exception)
            {
                generate = null;
                if (!failed)
                {
                    failed = true;
                    Log.Error("[RHAH] CE loadout API unavailable; skipping ammunition generation: " + exception);
                }
            }
        }
    }
}
