using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    public sealed class GameComponent_MouseDisasterRemoval : GameComponent
    {
        private bool newContentDisabled;
        public bool NewContentDisabled => newContentDisabled;

        public GameComponent_MouseDisasterRemoval(Game game) { }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref newContentDisabled, "newContentDisabled", false);
        }

        public void DisableNewContent() { newContentDisabled = true; }

        public void ExportCleanSave()
        {
            Find.TickManager.Pause();
            newContentDisabled = true;
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string backup = "MouseDisaster-backup-" + stamp;
            string clean = "MouseDisaster-removed-" + stamp;
            try
            {
                string backupPath = GenFilePaths.FilePathForSavedGame(backup);
                SafeSaver.Save(backupPath, "savegame", delegate
                {
                    ScribeMetaHeaderUtility.WriteMetaHeader();
                    Game game = Current.Game;
                    Scribe_Deep.Look(ref game, "game");
                });
                XDocument document = XDocument.Load(backupPath);
                var cleanup = new MouseDisasterSaveCleanup(BuildPlan());
                cleanup.Clean(document);
                // CreateNew never replaces a player save, including a previous export.
                string cleanPath = GenFilePaths.FilePathForSavedGame(clean);
                string temporaryPath = cleanPath + ".tmp";
                try
                {
                    using (var stream = new FileStream(temporaryPath, FileMode.CreateNew))
                        document.Save(stream);
                    File.Move(temporaryPath, cleanPath);
                }
                finally
                {
                    try
                    {
                        if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                    }
                    catch (Exception cleanupException)
                    {
                        Log.Warning("[MouseDisaster] Could not remove temporary clean-save file " +
                            temporaryPath + ": " + cleanupException.Message);
                    }
                }
                Find.WindowStack.Add(new Dialog_MessageBox("MouseDisaster_RemovalDone".Translate(
                    clean, backup, cleanup.ReplacedDefs, cleanup.RemovedEntries)));
            }
            catch (Exception exception)
            {
                Log.Error("[MouseDisaster] Save cleanup failed: " + exception);
                Find.WindowStack.Add(new Dialog_MessageBox("MouseDisaster_RemovalFailed".Translate(backup, exception.Message)));
            }
        }

        private static MouseDisasterCleanupPlan BuildPlan()
        {
            ModContentPack content = LoadedModManager.GetMod<MouseDisasterMod>().Content;
            var plan = new MouseDisasterCleanupPlan { PackageId = content.PackageId };
            plan.LegacyPackageIds.UnionWith(new[]
            {
                "lezhizhong.mouse.disaster.famine",
                "local.mousedisaster.greatfamine"
            });
            foreach (Def def in content.AllDefs) plan.OwnedDefs.Add(def.defName);
            foreach (Type type in typeof(MouseDisasterMod).Assembly.GetTypes()) plan.OwnedClasses.Add(type.FullName);
            foreach (ThingDef def in content.AllDefs.OfType<ThingDef>()) plan.ThingDefs.Add(def.defName);
            foreach (PawnKindDef kind in content.AllDefs.OfType<PawnKindDef>())
            {
                PawnKindDef replacement = DefDatabase<PawnKindDef>.AllDefs
                    .Where(d => d.race == kind.race && !plan.OwnedDefs.Contains(d.defName))
                    .OrderBy(d => d.combatPower).ThenBy(d => d.defName, StringComparer.Ordinal).FirstOrDefault();
                if (replacement == null) throw new InvalidOperationException("No same-race replacement for " + kind.defName);
                plan.Replacements.Add(kind.defName, replacement.defName);
            }
            foreach (BackstoryDef story in content.AllDefs.OfType<BackstoryDef>())
            {
                BackstoryDef replacement = DefDatabase<BackstoryDef>.AllDefs
                    .Where(d => d.modContentPack?.IsCoreMod == true && d.slot == story.slot)
                    .OrderBy(d => d.workDisables == story.workDisables ? 0 : 1)
                    .ThenBy(d => DefDatabase<SkillDef>.AllDefs.Sum(skill => Math.Abs(
                        d.skillGains.Where(g => g.skill == skill).Sum(g => g.amount) -
                        story.skillGains.Where(g => g.skill == skill).Sum(g => g.amount))))
                    .ThenBy(d => d.defName, StringComparer.Ordinal).FirstOrDefault();
                if (replacement == null) throw new InvalidOperationException("No core backstory for " + story.defName);
                plan.Replacements.Add(story.defName, replacement.defName);
            }
            foreach (FactionDef def in content.AllDefs.OfType<FactionDef>())
                plan.Replacements.Add(def.defName, FactionDefOf.Ancients.defName);
            foreach (StorytellerDef def in content.AllDefs.OfType<StorytellerDef>())
                plan.Replacements.Add(def.defName, DefDatabase<StorytellerDef>.GetNamed("Randy").defName);
            foreach (XenotypeDef def in content.AllDefs.OfType<XenotypeDef>())
                plan.Replacements.Add(def.defName, XenotypeDefOf.Baseliner.defName);
            foreach (TraderKindDef def in content.AllDefs.OfType<TraderKindDef>())
                plan.Replacements.Add(def.defName, DefDatabase<TraderKindDef>.GetNamed("Caravan_Outlander_BulkGoods").defName);
            foreach (SitePartDef def in content.AllDefs.OfType<SitePartDef>())
                plan.Replacements.Add(def.defName, SitePartDefOf.PossibleUnknownThreatMarker.defName);
            return plan;
        }
    }
}
