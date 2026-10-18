using System.Collections.Generic;
using Verse;

namespace HungerAndHavoc.Pawn
{
    internal readonly struct GnawBite
    {
        internal GnawBite(bool wall, float nutrition, float damage, float severity, float toxic)
        {
            Wall = wall;
            Nutrition = nutrition;
            Damage = damage;
            Severity = severity;
            Toxic = toxic;
        }

        internal bool Wall { get; }
        internal float Nutrition { get; }
        internal float Damage { get; }
        internal float Severity { get; }
        internal float Toxic { get; }
    }

    internal static class RHAH_GnawHealth
    {
        internal const float BarkNutrition = 0.25f;
        internal const float BarkDamage = 12f;
        internal const float BarkSeverity = 0.2f;
        internal const float BarkToxic = 0.08f;
        internal const float WallNutrition = 0.2f;
        internal const float WallDamage = 10f;
        internal const float WallSeverity = 0.2f;
        internal const float OverSeverity = 0.3f;
        internal const int OverCount = 5;
        internal const int DurationTicks = 90000;

        internal static GnawBite ForTarget(bool wall)
        {
            Core.RHAH_Settings settings = Core.RHAH_Mod.Settings;
            if (wall)
            {
                float nutrition = settings == null ? WallNutrition : settings.wallNutrition;
                float damage = settings == null ? WallDamage : settings.wallDamage;
                return new GnawBite(true, nutrition, damage, WallSeverity, 0f);
            }

            float barkNutrition = settings == null ? BarkNutrition : settings.barkNutrition;
            float barkDamage = settings == null ? BarkDamage : settings.barkDamage;
            return new GnawBite(false, barkNutrition, barkDamage, BarkSeverity, BarkToxic);
        }

        // 绕过材料和建筑伤害倍率 只扣本次啃食的耐久
        internal static void DamageTarget(Verse.Thing target, float damage, Verse.Pawn instigator)
        {
            if (target == null || target.Destroyed || !target.def.useHitPoints || damage <= 0f)
            {
                return;
            }

            int amount = Verse.GenMath.RoundRandom(damage);
            target.HitPoints = FixedHitPointsAfterBite(target.HitPoints, amount);
            if (target.HitPoints == 0)
            {
                target.Kill(new Verse.DamageInfo(RimWorld.DamageDefOf.Blunt, amount, instigator: instigator));
            }
        }

        internal static int FixedHitPointsAfterBite(int hitPoints, int damage)
        {
            if (damage <= 0)
            {
                return hitPoints;
            }

            return System.Math.Max(0, hitPoints - damage);
        }

        internal static Verse.Thing NearestTarget(Verse.IntVec3 position, Verse.Thing tree, Verse.Thing wall)
        {
            if (tree == null)
            {
                return wall;
            }

            return wall == null || tree.Position.DistanceToSquared(position) <= wall.Position.DistanceToSquared(position)
                ? tree
                : wall;
        }

        internal static int NextWallCount(Dictionary<int, int> counts, int pawnLoadId)
        {
            if (counts == null || pawnLoadId <= 0)
            {
                return 0;
            }

            int current;
            counts.TryGetValue(pawnLoadId, out current);
            int next = current + 1;
            counts[pawnLoadId] = next;
            return next;
        }

        internal static void Forget(Dictionary<int, int> counts, int pawnLoadId)
        {
            if (counts == null || pawnLoadId <= 0)
            {
                return;
            }

            counts.Remove(pawnLoadId);
        }

        internal static bool IsOvergnaw(int count)
        {
            return count >= OverCount;
        }

        internal static float ClampFood(float current, float max)
        {
            if (float.IsNaN(current) || float.IsInfinity(current) || float.IsNaN(max) || float.IsInfinity(max))
            {
                return current;
            }

            return current > max ? max : current;
        }
    }
}
