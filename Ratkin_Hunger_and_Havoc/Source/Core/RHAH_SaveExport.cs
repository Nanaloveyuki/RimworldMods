using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Core
{
    internal static class RHAH_SaveExport
    {
        internal static void Export()
        {
            if (Find.TickManager != null)
            {
                Find.TickManager.Pause();
            }


            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string backup = UniqueName("RHAH-backup-" + stamp);
            string clean = UniqueName("RHAH-removed-" + stamp);
            string backupPath = null;
            try
            {
                backupPath = GenFilePaths.FilePathForSavedGame(backup);
                SafeSaver.Save(backupPath, "savegame", () =>
                {
                    ScribeMetaHeaderUtility.WriteMetaHeader();
                    Scribe.EnterNode("game");
                    try
                    {
                        Current.Game.ExposeData();
                    }
                    finally
                    {
                        Scribe.ExitNode();
                    }
                });
                Current.Game?.GetComponent<GameComponent_RHAH_Game>()?.DisableNewContent();
                XDocument document = XDocument.Load(backupPath);
                RHAH_SaveCleanup cleanup = new RHAH_SaveCleanup(BuildPlan());
                cleanup.Clean(document);
                string cleanPath = GenFilePaths.FilePathForSavedGame(clean);
                WriteClean(document, cleanPath);
                Find.WindowStack.Add(new Dialog_MessageBox("RHAH_RemovalDone".Translate(
                    clean, backup, cleanup.ReplacedDefs, cleanup.RemovedEntries)));
            }
            catch (Exception exception)
            {
                Log.Error("[RHAH] Save cleanup failed: " + exception);
                Find.WindowStack.Add(new Dialog_MessageBox("RHAH_RemovalFailed".Translate(
                    backup, exception.Message)));
            }
        }

        static void WriteClean(XDocument document, string cleanPath)
        {
            string temporary = cleanPath + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew))
                {
                    document.Save(stream);
                }

                File.Move(temporary, cleanPath);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    try
                    {
                        File.Delete(temporary);
                    }
                    catch (Exception exception)
                    {
                        Log.Warning("[RHAH] Could not remove temporary clean-save file " + temporary + ": " + exception.Message);
                    }
                }
            }
        }

        static string UniqueName(string name)
        {
            string candidate = name;
            int suffix = 2;
            while (File.Exists(GenFilePaths.FilePathForSavedGame(candidate)))
            {
                candidate = name + "-" + suffix;
                suffix++;
            }

            return candidate;
        }

        internal static RHAH_SaveCleanupPlan BuildPlan()
        {
            ModContentPack content = LoadedModManager.GetMod<RHAH_Mod>().Content;
            RHAH_SaveCleanupPlan plan = new RHAH_SaveCleanupPlan { PackageId = content.PackageId };
            foreach (Def def in content.AllDefs)
            {
                plan.OwnedDefs.Add(def.defName);
            }

            foreach (Type type in typeof(RHAH_Mod).Assembly.GetTypes())
            {
                plan.OwnedClasses.Add(type.FullName);
            }

            foreach (ThingDef def in content.AllDefs.OfType<ThingDef>())
            {
                plan.ThingDefs.Add(def.defName);
            }

            foreach (GeneDef def in content.AllDefs.OfType<GeneDef>())
            {
                plan.GeneDefs.Add(def.defName);
            }

            foreach (ThoughtDef def in content.AllDefs.OfType<ThoughtDef>())
            {
                for (int i = 0; i < (def.stages?.Count ?? 0); i++)
                {
                    plan.ThoughtStageKeys.Add(def.defName + "_" + i);
                }
            }

            plan.CampObjectDefs.Add("RHAH_RefugeeCamp");
            plan.CampObjectDefs.Add("RHAH_RecordSite");
            plan.CampObjectDefs.Add("RHAH_Interception");
            plan.CampObjectDefs.Add("RHAH_KingdomSite");
            plan.CampObjectClasses.Add("HungerAndHavoc.Incidents.WorldObject_RHAH_RefugeeCamp");
            plan.CampObjectClasses.Add("HungerAndHavoc.Incidents.WorldObject_RHAH_Interception");
            WorldObjectDef site = RequireDef<WorldObjectDef>("Site");
            if (content.AllDefs.Contains(site))
            {
                throw new InvalidOperationException("Site belongs to this mod.");
            }

            MapGeneratorDef siteGenerator = site.mapGenerator ?? RequireDef<MapGeneratorDef>("Encounter");
            if (siteGenerator.modContentPack == content || content.AllDefs.Contains(siteGenerator))
            {
                throw new InvalidOperationException("Site map generator belongs to this mod.");
            }

            foreach (MapGeneratorDef def in content.AllDefs.OfType<MapGeneratorDef>())
            {
                plan.MapGeneratorReplacements.Add(def.defName, siteGenerator.defName);
            }

            plan.Replacements.Add("RHAH_Suiyin", RequireDef<StorytellerDef>("Randy").defName);
            plan.Replacements.Add("RHAH_Xenotype_Ratkin", RequireDef<XenotypeDef>("Baseliner").defName);
            plan.Replacements.Add("RHAH_LongKeepsake", RequireDef<ThingDef>("Cloth").defName);
            plan.Replacements.Add("RHAH_Trader_Famine", RequireDef<TraderKindDef>("Visitor_Outlander_Standard").defName);
            FactionDef ancients = RequireDef<FactionDef>("Ancients");
            if (content.AllDefs.Contains(ancients))
            {
                throw new InvalidOperationException("Ancients belongs to this mod.");
            }

            foreach (FactionDef def in content.AllDefs.OfType<FactionDef>())
            {
                plan.Replacements.Add(def.defName, ancients.defName);
            }

            PawnKindDef kind = RequireSameRaceKind(content);
            plan.Replacements.Add("RHAH_PawnKind_Ratkin", kind.defName);
            foreach (BackstoryDef story in content.AllDefs.OfType<BackstoryDef>())
            {
                plan.Replacements.Add(story.defName, RequireBackstory(content, story.slot).defName);
            }

            return plan;
        }

        static PawnKindDef RequireSameRaceKind(ModContentPack content)
        {
            ThingDef race = DefDatabase<ThingDef>.GetNamedSilentFail("Ratkin");
            if (race == null)
            {
                throw new InvalidOperationException("Ratkin race is not loaded.");
            }

            List<PawnKindDef> kinds = new List<PawnKindDef>();
            foreach (PawnKindDef def in DefDatabase<PawnKindDef>.AllDefs)
            {
                if (def.race == race && def.modContentPack != content)
                {
                    kinds.Add(def);
                }
            }

            kinds.Sort((left, right) => string.CompareOrdinal(left.defName, right.defName));
            if (kinds.Count == 0)
            {
                throw new InvalidOperationException("No same-race replacement for RHAH_PawnKind_Ratkin.");
            }

            return kinds[0];
        }

        static BackstoryDef RequireBackstory(ModContentPack content, BackstorySlot slot)
        {
            List<BackstoryDef> stories = new List<BackstoryDef>();
            foreach (BackstoryDef def in DefDatabase<BackstoryDef>.AllDefs)
            {
                if (def.slot == slot && def.modContentPack != content)
                {
                    stories.Add(def);
                }
            }

            stories.Sort((left, right) => string.CompareOrdinal(left.defName, right.defName));
            if (stories.Count == 0)
            {
                throw new InvalidOperationException("No backstory replacement for slot " + slot + ".");
            }

            return stories[0];
        }

        static T RequireDef<T>(string defName) where T : Def
        {
            T def = DefDatabase<T>.GetNamedSilentFail(defName);
            if (def == null)
            {
                throw new InvalidOperationException(typeof(T).Name + " " + defName + " is not loaded.");
            }

            return def;
        }
    }
}
