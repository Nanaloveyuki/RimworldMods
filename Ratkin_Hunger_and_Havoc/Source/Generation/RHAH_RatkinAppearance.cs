using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Generation
{
    internal static class RHAH_RatkinAppearance
    {
        const string RaceDefName = "Ratkin";
        const string HairTag = "RK_Style";
        static void ApplyHair(Verse.Pawn pawn, IList<string> hairTags)
        {
            List<string> tags = Names(hairTags);
            if (tags.Count == 0)
            {
                Log.Error("RHAH ratkin hair tags are missing");
                return;
            }

            List<HairDef> choices = new List<HairDef>();
            List<HairDef> all = DefDatabase<HairDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                HairDef hair = all[i];
                if (HairAllowed(hair, pawn == null ? Gender.None : pawn.gender, tags) &&
                    PawnStyleItemChooser.AgeAppropriateHairStyle(pawn, hair))
                {
                    choices.Add(hair);
                }
            }

            if (choices.Count == 0)
            {
                Log.Error("RHAH ratkin hair defs are missing");
                return;
            }

            pawn.story.hairDef = choices[Rand.Range(0, choices.Count)];
        }

        internal static bool HairAllowed(bool hasTag, StyleGender styleGender, Gender pawnGender)
        {
            if (!hasTag)
            {
                return false;
            }

            if (pawnGender == Gender.Female)
            {
                return styleGender != StyleGender.Male;
            }

            if (pawnGender == Gender.Male)
            {
                return styleGender != StyleGender.Female;
            }

            return true;
        }

        static bool HairAllowed(HairDef hair, Gender pawnGender, IList<string> tags)
        {
            if (hair == null || tags == null)
            {
                return false;
            }

            bool tagged = false;
            for (int i = 0; i < tags.Count; i++)
            {
                if (HasTag(hair, tags[i]))
                {
                    tagged = true;
                    break;
                }
            }

            return HairAllowed(tagged, hair.styleGender, pawnGender);
        }

        const string NoStyleTag = "alienNoStyle";
        internal static bool UsesRatkinAppearance(string raceDefName)
        {
            return string.Equals(raceDefName, RaceDefName, StringComparison.OrdinalIgnoreCase);
        }

        internal static void Apply(Verse.Pawn pawn, bool keepExplicitApparel)
        {
            if (pawn?.story == null || !UsesRatkinAppearance(pawn.kindDef?.race?.defName))
            {
                return;
            }

            RHAH_RatkinStyle style = Read(pawn.kindDef.race);
            Apply(pawn, style, keepExplicitApparel);
        }

        internal static void Apply(Verse.Pawn pawn, RHAH_RatkinStyle style, bool keepExplicitApparel)
        {
            if (pawn?.story == null || style == null)
            {
                return;
            }

            ApplyHead(pawn, style.HeadNames);
            ApplyHair(pawn, style.HairTags);
            ApplyBeard(pawn, style.BeardsDisabled);
            ApplyTattoos(pawn, style.TattoosDisabled);
            if (HungerAndHavoc.Pawn.RHAH_VisitorRules.StripsRaceApparel(ApparelMode(), keepExplicitApparel))
            {
                StripDisallowedApparel(pawn, style.ApparelNames, style.WhiteApparelNames);
            }

            if (HungerAndHavoc.Pawn.RHAH_VisitorRules.ClearsApparel(ApparelMode(), keepExplicitApparel) && pawn.apparel != null)
            {
                pawn.apparel.DestroyAll();
            }
        }

        internal static RHAH_RatkinStyle Read(object race)
        {
            return new RHAH_RatkinStyle(
                HeadNames(race),
                HairTags(race),
                BeardsDisabled(race),
                TattoosDisabled(race),
                ApparelNames(race, "apparelList"),
                ApparelNames(race, "whiteApparelList"));
        }
        static int ApparelMode()
        {
            Core.RHAH_Settings settings = Core.RHAH_Mod.Settings;
            return settings == null ? 0 : settings.apparelMode;
        }


        internal static bool StyleAllowed(string itemTypeName, bool hasStyle, IList<string> styleTagsOverride, IList<string> styleTags)
        {
            if (itemTypeName == nameof(BeardDef) || itemTypeName == nameof(TattooDef))
            {
                return !hasStyle && styleTags != null && styleTags.Contains(NoStyleTag);
            }

            if (itemTypeName != nameof(HairDef) || !hasStyle)
            {
                return false;
            }

            return styleTags != null &&
                styleTagsOverride != null &&
                styleTagsOverride.Contains(HairTag) &&
                styleTags.Contains(HairTag);
        }

        internal static bool ApparelAllowed(string defName, ICollection<string> apparelList, ICollection<string> whiteApparelList)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return false;
            }

            return (apparelList != null && apparelList.Contains(defName)) ||
                (whiteApparelList != null && whiteApparelList.Contains(defName));
        }

        static void ApplyHead(Verse.Pawn pawn, IList<string> allowedNames)
        {
            List<string> allowed = Names(allowedNames);
            if (allowed.Count == 0)
            {
                Log.Error("RHAH ratkin head list is missing");
                return;
            }

            if (pawn.story.headType != null && allowed.Contains(pawn.story.headType.defName))
            {
                return;
            }

            List<HeadTypeDef> choices = new List<HeadTypeDef>();
            List<HeadTypeDef> all = DefDatabase<HeadTypeDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && allowed.Contains(all[i].defName))
                {
                    choices.Add(all[i]);
                }
            }

            if (choices.Count == 0)
            {
                Log.Error("RHAH ratkin head defs are missing");
                return;
            }

            pawn.story.headType = choices[Rand.Range(0, choices.Count)];
        }


        static void ApplyBeard(Verse.Pawn pawn, bool disabled)
        {
            if (pawn.style == null)
            {
                return;
            }

            if (!disabled)
            {
                Log.Error("RHAH ratkin beard setting is missing");
                return;
            }

            pawn.style.beardDef = BeardDefOf.NoBeard;
        }

        static void ApplyTattoos(Verse.Pawn pawn, bool disabled)
        {
            if (pawn.style == null || !ModsConfig.IdeologyActive)
            {
                return;
            }

            if (!disabled)
            {
                Log.Error("RHAH ratkin tattoo setting is missing");
                return;
            }

            pawn.style.FaceTattoo = TattooDefOf.NoTattoo_Face;
            pawn.style.BodyTattoo = TattooDefOf.NoTattoo_Body;
        }

        static void StripDisallowedApparel(Verse.Pawn pawn, ICollection<string> apparel, ICollection<string> white)
        {
            if (pawn.apparel == null)
            {
                return;
            }

            if ((apparel == null || apparel.Count == 0) && (white == null || white.Count == 0))
            {
                Log.Error("RHAH ratkin apparel lists are missing");
                return;
            }

            List<Apparel> worn = pawn.apparel.WornApparel;
            for (int i = worn.Count - 1; i >= 0; i--)
            {
                Apparel item = worn[i];
                if (item?.def != null && !ApparelAllowed(item.def.defName, apparel, white))
                {
                    pawn.apparel.Remove(item);
                    item.Destroy();
                }
            }
        }

        static List<string> Names(IList<string> names)
        {
            List<string> copy = new List<string>();
            if (names == null)
            {
                return copy;
            }

            for (int i = 0; i < names.Count; i++)
            {
                if (!string.IsNullOrEmpty(names[i]))
                {
                    copy.Add(names[i]);
                }
            }

            return copy;
        }
        static List<string> HeadNames(object race)
        {
            object settings = Member(Member(race, "alienRace"), "generalSettings");
            object generator = Member(settings, "alienPartGenerator");
            return DefNames(Member(generator, "headTypes"));
        }

        static List<string> HairTags(object race)
        {
            object settings = Style(race, "HairDef");
            return Strings(Member(settings, "styleTagsOverride"));
        }

        static bool BeardsDisabled(object race)
        {
            object settings = Style(race, "BeardDef");
            return settings != null && Member(settings, "hasStyle") is bool hasStyle && !hasStyle;
        }

        static bool TattoosDisabled(object race)
        {
            object settings = Style(race, "TattooDef");
            return settings != null && Member(settings, "hasStyle") is bool hasStyle && !hasStyle;
        }

        static object Style(object race, string typeName)
        {
            object settings = Member(Member(race, "alienRace"), "styleSettings");
            IDictionary dictionary = settings as IDictionary;
            if (dictionary == null)
            {
                return null;
            }

            foreach (object key in dictionary.Keys)
            {
                Type type = key as Type;
                if (type != null && type.Name == typeName)
                {
                    return dictionary[key];
                }
            }

            return null;
        }

        static HashSet<string> ApparelNames(object race, string fieldName)
        {
            object restriction = Member(Member(race, "alienRace"), "raceRestriction");
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            List<string> defs = DefNames(Member(restriction, fieldName));
            for (int i = 0; i < defs.Count; i++)
            {
                names.Add(defs[i]);
            }

            return names;
        }
        static List<string> DefNames(object value)
        {
            List<string> names = new List<string>();
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null || value is string)
            {
                return names;
            }

            foreach (object item in enumerable)
            {
                string name = DefName(item);
                if (!string.IsNullOrEmpty(name))
                {
                    names.Add(name);
                }
            }

            return names;
        }

        static string DefName(object item)
        {
            Def def = item as Def;
            if (def != null)
            {
                return def.defName;
            }

            FieldInfo field = item?.GetType().GetField("defName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(item) as string;
        }

        static List<string> Strings(object value)
        {
            List<string> names = new List<string>();
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null || value is string)
            {
                return names;
            }

            foreach (object item in enumerable)
            {
                string name = item as string;
                if (!string.IsNullOrEmpty(name))
                {
                    names.Add(name);
                }
            }

            return names;
        }

        static bool HasTag(StyleItemDef style, string tag)
        {
            return style?.styleTags != null && style.styleTags.Contains(tag);
        }

        static object Member(object target, string name)
        {
            if (target == null)
            {
                return null;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo field = target.GetType().GetField(name, flags);
            if (field != null)
            {
                return field.GetValue(target);
            }

            PropertyInfo property = target.GetType().GetProperty(name, flags);
            return property == null ? null : property.GetValue(target, null);
        }
    }


    internal sealed class RHAH_RatkinStyle
    {
        internal RHAH_RatkinStyle(
            List<string> headNames,
            List<string> hairTags,
            bool beardsDisabled,
            bool tattoosDisabled,
            HashSet<string> apparelNames,
            HashSet<string> whiteApparelNames)
        {
            HeadNames = headNames ?? new List<string>();
            HairTags = hairTags ?? new List<string>();
            BeardsDisabled = beardsDisabled;
            TattoosDisabled = tattoosDisabled;
            ApparelNames = apparelNames ?? new HashSet<string>();
            WhiteApparelNames = whiteApparelNames ?? new HashSet<string>();
        }

        internal List<string> HeadNames { get; }
        internal List<string> HairTags { get; }
        internal bool BeardsDisabled { get; }
        internal bool TattoosDisabled { get; }
        internal HashSet<string> ApparelNames { get; }
        internal HashSet<string> WhiteApparelNames { get; }

        internal bool Complete =>
            HeadNames.Count > 0 &&
            HairTags.Contains("RK_Style") &&
            BeardsDisabled &&
            TattoosDisabled &&
            (ApparelNames.Count > 0 || WhiteApparelNames.Count > 0);
    }
}
