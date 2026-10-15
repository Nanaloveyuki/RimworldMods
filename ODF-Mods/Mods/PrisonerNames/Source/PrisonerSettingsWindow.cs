using System;
using System.Collections.Generic;
using IrisMenus;
using UnityEngine;
using Verse;

namespace Odf.PrisonerNames
{
    static class PrisonerSettingsWindow
    {
        static NamePolicy draft;
        static Game bound;
        static NamePolicy appliedPolicy;
        static string scoreBuffer = "0";
        static string resistanceBuffer = "0";
        static string custodyBuffer = "0";
        static string breakBuffer = "0";
        static float threshold;
        static string thresholdBuffer = "0";
        static string gradeBuffer = string.Empty;
        static string prefixRace = "Human";
        static string prefixBuffer = string.Empty;
        static string preview = string.Empty;
        static string previewError = string.Empty;
        static string saveError = string.Empty;
        static bool dirty;
        static int removeRace = -1;
        static int removeGrade = -1;

        internal static void Register(Mod owner)
        {
            MenuRegistry.RegisterModNameAlias(owner, "ChineseSimplified", "ODF 囚犯代号");
            MenuRegistry.RegisterModNameAlias(owner, "ChineseSimplified (简体中文)", "ODF 囚犯代号");
            MenuRegistry.RegisterModNameAlias(owner, "English", "ODF Prisoner Names");
            MenuRegistry.RegisterSubItemListing(owner, "names", () => "OPN_PolicyPage".Translate(), DrawNames);
            MenuRegistry.RegisterSubItemListing(owner, "scores", () => "OPN_ScoresPage".Translate(), DrawScores);
            MenuRegistry.RegisterSubItemListing(owner, "grades", () => "OPN_Grades".Translate(), DrawGradePage);
            IMarkdown guide = Markdown.Dynamic(() => "OPN_UserGuide".Translate().ToString());
            MenuRegistry.RegisterSubItemListing(owner, "guide", () => "OPN_UserGuideTitle".Translate(),
                listing => MenuControls.Markdown(listing, guide));
        }

        static void DrawNames(Listing_Standard listing)
        {
            GameComponent_PrisonerNames runtime = Prepare(listing, "OPN_PolicyIntro");
            if (runtime == null) return;
            Section(listing, "OPN_SectionGeneral");
            bool enabled = runtime.Enabled;
            MenuControls.Checkbox(listing, "OPN_EnabledToggle".Translate(), ref enabled);
            if (enabled != runtime.Enabled) runtime.SetEnabled(enabled);
            if (listing.ButtonText("OPN_OpenWindow".Translate())) PrisonerNamesApi.OpenWindow();
            listing.Gap(8f);
            listing.Label("OPN_SourceMode".Translate() + ": " + (runtime.CustomMode ? "OPN_CustomSource".Translate() : "OPN_ConfigSource".Translate()));
            if (!runtime.CustomMode && listing.ButtonText("OPN_UseCustom".Translate())) runtime.UseCustom(runtime.ActiveSource);
            if (runtime.CustomMode)
            {
                listing.Label("OPN_CustomSettingsHint".Translate());
                if (listing.ButtonText("OPN_UsePattern".Translate())) UseSettings(runtime, draft);
            }
            listing.Gap(8f);
            if (listing.ButtonText("OPN_NameStyle".Translate() + ": " + ("OPN_Style_" + NamePolicy.StyleName(draft.Style)).Translate()))
            {
                draft.Style = (NameStyle)(((int)draft.Style + 1) % 5);
                Changed();
            }
            Section(listing, "OPN_SectionAge");
            listing.Label("OPN_Placeholders".Translate());
            PrisonerAgeSettings.Draw(listing, draft, Changed, RefreshPreview);
            if (!draft.UseAgePatterns)
                DrawText(listing, "OPN_Pattern".Translate(), NamingLanguage.Chinese ? draft.ChinesePattern : draft.Pattern, value =>
                {
                    if (NamingLanguage.Chinese) draft.ChinesePattern = value;
                    else draft.Pattern = value;
                });
            Section(listing, "OPN_SectionRace");
            DrawRaces(listing);
            Section(listing, "OPN_SectionPresets");
            for (int i = 0; i < NamePresets.All.Count; i++)
            {
                NamePresets.Preset preset = NamePresets.All[i];
                if (!listing.ButtonText(("OPN_Preset_" + preset.Id).Translate())) continue;
                NamePolicy candidate = draft.Clone();
                candidate.Pattern = preset.Pattern;
                candidate.ChinesePattern = preset.ChinesePattern;
                candidate.Style = preset.Style;
                candidate.UseAgePatterns = false;
                UseSettings(runtime, candidate);
            }
            AutoSave(runtime);
            Section(listing, "OPN_SectionPreview");
            DrawPreview(listing);
        }

        static GameComponent_PrisonerNames Prepare(Listing_Standard listing, string intro)
        {
            GameComponent_PrisonerNames runtime = GameComponent_PrisonerNames.Current;
            listing.Label(intro.Translate());
            if (runtime == null)
            {
                draft = null;
                listing.Label("OPN_NoGame".Translate());
                return null;
            }
            EnsureDraft(runtime);
            listing.Label("OPN_AutoSaveHint".Translate());
            return runtime;
        }

        static void DrawScores(Listing_Standard listing)
        {
            GameComponent_PrisonerNames runtime = Prepare(listing, "OPN_ScoresIntro");
            if (runtime == null) return;
            Section(listing, "OPN_SectionScores");
            DrawScoreControls(listing);
            AutoSave(runtime);
            Section(listing, "OPN_SectionPreview");
            DrawPreview(listing);
        }

        static void DrawScoreControls(Listing_Standard listing)
        {
            float neutral = (float)draft.NeutralScore;
            MenuControls.Number(listing, "OPN_NeutralScore".Translate(), ref neutral, ref scoreBuffer, -100000f, 100000f);
            if (draft.NeutralScore != neutral)
            {
                draft.NeutralScore = neutral;
                Changed();
            }
            float resistance = (float)draft.ResistancePerPoint;
            MenuControls.Number(listing, "OPN_ResistanceScore".Translate(), ref resistance, ref resistanceBuffer, -100000f, 100000f);
            if (draft.ResistancePerPoint != resistance) { draft.ResistancePerPoint = resistance; Changed(); }
            float custody = (float)draft.CustodyDayScore;
            MenuControls.Number(listing, "OPN_CustodyScore".Translate(), ref custody, ref custodyBuffer, -100000f, 100000f);
            if (draft.CustodyDayScore != custody) { draft.CustodyDayScore = custody; Changed(); }
            float prisonBreak = (float)draft.PrisonBreakScore;
            MenuControls.Number(listing, "OPN_PrisonBreakScore".Translate(), ref prisonBreak, ref breakBuffer, -100000f, 100000f);
            if (draft.PrisonBreakScore != prisonBreak) { draft.PrisonBreakScore = prisonBreak; Changed(); }
        }

        static void DrawGradePage(Listing_Standard listing)
        {
            GameComponent_PrisonerNames runtime = Prepare(listing, "OPN_GradesIntro");
            if (runtime == null) return;
            Section(listing, "OPN_SectionGrades");
            DrawGrades(listing);
            AutoSave(runtime);
            Section(listing, "OPN_SectionPreview");
            DrawPreview(listing);
        }

        static void DrawPreview(Listing_Standard listing)
        {
            if (!string.IsNullOrEmpty(saveError)) listing.Label("OPN_PolicyFailed".Translate() + ": " + saveError);
            if (!string.IsNullOrEmpty(previewError)) listing.Label("OPN_PolicyPreviewFailed".Translate() + ": " + previewError);
            else listing.Label("OPN_PolicyPreview".Translate() + ": " + preview);
        }

        static void EnsureDraft(GameComponent_PrisonerNames runtime)
        {
            if (draft != null && ReferenceEquals(bound, Current.Game) && ReferenceEquals(appliedPolicy, runtime.Policy)) return;
            draft = runtime.Policy.Clone();
            bound = Current.Game;
            appliedPolicy = runtime.Policy;
            scoreBuffer = NamePolicy.FormatNumber(draft.NeutralScore);
            resistanceBuffer = NamePolicy.FormatNumber(draft.ResistancePerPoint);
            custodyBuffer = NamePolicy.FormatNumber(draft.CustodyDayScore);
            breakBuffer = NamePolicy.FormatNumber(draft.PrisonBreakScore);
            dirty = false;
            saveError = string.Empty;
            threshold = 0f;
            thresholdBuffer = "0";
            RefreshPreview();
        }

        static void Section(Listing_Standard listing, string title)
        {
            listing.Gap(16f);
            MenuControls.Section(listing, title.Translate());
            listing.Gap(8f);
        }

        static void Changed()
        {
            dirty = true;
            RefreshPreview();
        }

        static void AutoSave(GameComponent_PrisonerNames runtime)
        {
            if (!dirty) return;
            dirty = false;
            SavePolicy(runtime, draft, false);
        }

        static void UseSettings(GameComponent_PrisonerNames runtime, NamePolicy candidate)
        {
            if (runtime.CustomMode)
            {
                NamePolicy snapshot = candidate.Clone();
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("OPN_UsePatternConfirm".Translate(),
                    () => { if (ReferenceEquals(runtime, GameComponent_PrisonerNames.Current)) SavePolicy(runtime, snapshot, true); }));
                return;
            }
            SavePolicy(runtime, candidate, true);
        }

        static void SavePolicy(GameComponent_PrisonerNames runtime, NamePolicy candidate, bool replaceNamingRule)
        {
            if (!runtime.TrySetPolicy(candidate, out string error, replaceNamingRule))
            {
                saveError = error ?? "OPN_PolicyFailed".Translate().ToString();
                return;
            }
            appliedPolicy = runtime.Policy;
            draft = candidate;
            dirty = false;
            saveError = string.Empty;
            RefreshPreview();
        }

        static void DrawText(Listing_Standard listing, string label, string value, Action<string> assign)
        {
            listing.Label(label);
            string edited = Widgets.TextField(listing.GetRect(28f), value ?? string.Empty);
            if (!string.Equals(edited, value ?? string.Empty, StringComparison.Ordinal))
            {
                assign(edited);
                Changed();
            }
        }

        static void DrawRaces(Listing_Standard listing)
        {
            listing.Label("OPN_HumanlikeRacesHint".Translate());
            for (int i = 0; i < draft.Races.Count; i++)
            {
                RacePrefix race = draft.Races[i];
                listing.Label(PrisonerRaceNames.Label(race.Race) + " (" + race.Race + ")");
                Rect row = listing.GetRect(30f);
                string edited = Widgets.TextField(new Rect(row.x, row.y, Math.Max(20f, row.width - 104f), row.height), race.Prefix ?? string.Empty);
                if (edited != (race.Prefix ?? string.Empty)) { race.Prefix = edited; Changed(); }
                if (Widgets.ButtonText(new Rect(row.xMax - 96f, row.y, 96f, row.height), "OPN_Remove".Translate())) removeRace = i;
                listing.Gap(8f);
            }
            if (removeRace >= 0 && removeRace < draft.Races.Count)
            {
                draft.Races.RemoveAt(removeRace);
                Changed();
            }
            removeRace = -1;
            listing.Gap(8f);
            IReadOnlyList<ThingDef> available = PrisonerRaceNames.All;
            ThingDef selected = null;
            for (int i = 0; i < available.Count; i++)
                if (available[i].defName == prefixRace) { selected = available[i]; break; }
            if (selected == null && available.Count > 0) { selected = available[0]; prefixRace = selected.defName; }
            MenuControls.Select(listing, "OPN_Race".Translate(), selected, available,
                race => race == null ? "OPN_NoHumanlikeRaces".Translate().ToString() : PrisonerRaceNames.Label(race.defName) + " (" + race.defName + ")",
                race => prefixRace = race.defName);
            bool exists = false;
            for (int i = 0; i < draft.Races.Count; i++) exists |= draft.Races[i].Race == prefixRace;
            if (exists) { listing.Label("OPN_RacePrefixExists".Translate()); return; }
            if (selected == null) return;
            listing.Label("OPN_Prefix".Translate());
            prefixBuffer = Widgets.TextField(listing.GetRect(30f), prefixBuffer ?? string.Empty);
            listing.Gap(8f);
            if (listing.ButtonText("OPN_AddRace".Translate()))
            {
                draft.Races.Add(new RacePrefix { Race = prefixRace, Prefix = prefixBuffer ?? string.Empty });
                prefixBuffer = string.Empty;
                Changed();
            }
        }

        static void DrawGrades(Listing_Standard listing)
        {
            for (int i = 0; i < draft.Grades.Count; i++)
            {
                string minimum = double.IsNegativeInfinity(draft.Grades[i].Minimum) ? "OPN_BaseGrade".Translate().ToString() : NamePolicy.FormatNumber(draft.Grades[i].Minimum);
                listing.Label(minimum + "  " + draft.Grades[i].Name);
                if (listing.ButtonText("OPN_Remove".Translate())) removeGrade = i;
            }
            if (removeGrade >= 0 && removeGrade < draft.Grades.Count)
            {
                draft.Grades.RemoveAt(removeGrade);
                Changed();
            }
            removeGrade = -1;
            listing.Gap(16f);
            MenuControls.Number(listing, "OPN_Threshold".Translate(), ref threshold, ref thresholdBuffer, -100000f, 100000f);
            listing.Label("OPN_GradeName".Translate());
            gradeBuffer = Widgets.TextField(listing.GetRect(28f), gradeBuffer ?? string.Empty);
            if (listing.ButtonText("OPN_AddGrade".Translate()) && !string.IsNullOrWhiteSpace(gradeBuffer))
            {
                draft.Grades.Add(new GradeBand { Minimum = threshold, Name = gradeBuffer.Trim() });
                gradeBuffer = string.Empty;
                Changed();
            }
        }

        static void RefreshPreview()
        {
            preview = string.Empty;
            previewError = string.Empty;
            try
            {
                NameTokens sample = Sample(draft);
                preview = NameFormatter.ApplyStyle(draft.Style,
                    NameFormatter.Format(draft.PatternFor(sample.Race, PrisonerAgeSettings.SampleAge, NamingLanguage.Chinese), sample), sample);
            }
            catch (Exception exception)
            {
                previewError = exception.Message;
            }
        }


        static NameTokens Sample(NamePolicy policy)
        {
            return new NameTokens
            {
                Number = 1,
                NumberText = "001",
                Score = policy.NeutralScore,
                Grade = policy.GradeFor(policy.NeutralScore),
                Race = PrisonerAgeSettings.SampleRace,
                RaceLabel = PrisonerRaceNames.Label(PrisonerAgeSettings.SampleRace),
                RacePrefix = policy.PrefixFor(PrisonerAgeSettings.SampleRace, NamingLanguage.Chinese),
                OriginalName = "Ada",
                OriginalShort = "Ada",
                OriginalFullName = "Ada Sample",
                FirstName = "Ada",
                LastName = "Sample",
                InteractionLabel = "recruit"
            };
        }
    }
}
