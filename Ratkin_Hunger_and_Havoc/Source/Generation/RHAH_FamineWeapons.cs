using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Pawn;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Generation
{
    internal static class RHAH_FamineWeapons
    {
        static readonly string[] Weapons = { "MeleeWeapon_Club", "MeleeWeapon_Knife", "MeleeWeapon_Ikwa", "Gun_Revolver", "Gun_Autopistol", "Gun_BoltActionRifle" };

        internal static bool CanEquip(bool enabled, bool marked, bool player, string source, bool adult, bool violent)
        {
            return enabled && marked && !player && RHAH_ArmedDemand.IsSiege(source) && adult && violent;
        }

        internal static QualityCategory Quality(int roll)
        {
            return (QualityCategory)System.Math.Max(0, System.Math.Min((int)QualityCategory.Normal, roll));
        }

        internal static void Apply(Verse.Pawn pawn)
        {
            IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
            bool enabled = RHAH_Mod.Settings == null || RHAH_Mod.Settings.armedWeaponsEnabled;
            if (!CanEquip(enabled, snapshot != null, pawn.Faction?.IsPlayer == true, snapshot?.SourceIncidentDisplayId,
                pawn.DevelopmentalStage.Adult(), !pawn.WorkTagIsDisabled(WorkTags.Violent)) ||
                pawn.equipment == null || !RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Fight)) return;
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(Weapons[Rand.Range(0, Weapons.Length)]);
            if (def == null || def.modContentPack?.IsCoreMod != true || !def.IsWeapon) return;
            Equip(pawn, def);
        }

        internal static bool TryEquipRequired(Verse.Pawn pawn)
        {
            if (pawn?.equipment == null || !pawn.DevelopmentalStage.Adult() || pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                return false;
            }
            ThingOwner<Thing> inventory = pawn.inventory?.innerContainer;
            if (inventory != null)
            {
                for (int i = inventory.Count - 1; i >= 0; i--)
                {
                    if (inventory[i].def.IsWeapon) inventory[i].Destroy(DestroyMode.Vanish);
                }
            }
            int start = Rand.Range(0, Weapons.Length);
            for (int i = 0; i < Weapons.Length; i++)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(Weapons[(start + i) % Weapons.Length]);
                if (def?.modContentPack?.IsCoreMod == true && def.IsWeapon) return Equip(pawn, def);
            }
            Log.Error("[RHAH] I-054 requires a vanilla weapon from the famine weapon pool");
            return false;
        }

        static bool Equip(Verse.Pawn pawn, ThingDef def)
        {
            ThingDef stuff = def.MadeFromStuff ? (def.defName == "MeleeWeapon_Club" ? ThingDefOf.WoodLog : ThingDefOf.Steel) : null;
            ThingWithComps weapon = ThingMaker.MakeThing(def, stuff) as ThingWithComps;
            if (weapon == null) return false;
            weapon.TryGetComp<CompQuality>()?.SetQuality(Quality(Rand.RangeInclusive(0, (int)QualityCategory.Normal)), ArtGenerationContext.Outsider);
            pawn.equipment.DestroyAllEquipment();
            pawn.equipment.AddEquipment(weapon);
            return pawn.equipment.Primary == weapon;
        }
    }
}
