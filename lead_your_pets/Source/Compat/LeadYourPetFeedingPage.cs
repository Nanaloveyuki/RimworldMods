using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace LeadYourPet
{
    internal sealed class LeadYourPetFeedingPage
    {
        private enum Grouping { Mod, Alphabet, Vanilla }
        private sealed class FoodRow
        {
            internal ThingDef Food;
            internal string Group;
        }

        private string query = string.Empty;
        private string cachedQuery;
        private Grouping grouping;
        private Grouping cachedGrouping;
        private List<ThingDef> foods;
        private List<FoodRow> rows;

        internal void ClearSearch() { query = string.Empty; }

        private void EnsureFoods()
        {
            if (foods == null)
                foods = DefDatabase<ThingDef>.AllDefsListForReading.Where(LeadYourPetFeedFood.IsUsable).ToList();
        }

        private string GroupLabel(ThingDef food)
        {
            switch (grouping)
            {
                case Grouping.Alphabet:
                    char first = char.ToUpperInvariant(food.defName[0]);
                    return first >= 'A' && first <= 'Z' ? first.ToString() : "#";
                case Grouping.Vanilla:
                    return (LeadYourPetFeedFood.IsVanilla(food) ? "LeadYourPet_Feeding_Vanilla" : "LeadYourPet_Feeding_Modded").Translate();
                default:
                    return food.modContentPack?.Name ?? "LeadYourPet_Feeding_UnknownMod".Translate().ToString();
            }
        }

        private static bool Contains(string text, string term)
        {
            return text != null && text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RefreshRows()
        {
            EnsureFoods();
            if (rows != null && cachedQuery == query && cachedGrouping == grouping) return;
            string term = query.Trim();
            rows = foods.Where(food => term.Length == 0 || Contains(food.label, term) || Contains(food.defName, term)
                    || Contains(food.modContentPack?.Name, term) || Contains(food.modContentPack?.PackageId, term))
                .Select(food => new FoodRow { Food = food, Group = GroupLabel(food) })
                .OrderBy(row => row.Group, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(row => row.Food.label, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(row => row.Food.defName, StringComparer.Ordinal).ToList();
            cachedQuery = query;
            cachedGrouping = grouping;
        }

        internal void Draw(Listing_Standard list, bool iris)
        {
            LeadYourPetSettings settings = LeadYourPetMod.Settings;
            if (settings == null) return;
            list.Label("LeadYourPet_Feeding_Note".Translate());
            list.Label("LeadYourPet_Feeding_Search".Translate());
            query = list.TextEntry(query);
            list.Label("LeadYourPet_Feeding_Grouping".Translate());
            if (list.ButtonText(("LeadYourPet_Feeding_Group_" + grouping).Translate()))
            {
                var options = new List<FloatMenuOption>();
                foreach (Grouping choice in Enum.GetValues(typeof(Grouping)))
                {
                    Grouping captured = choice;
                    options.Add(new FloatMenuOption(("LeadYourPet_Feeding_Group_" + choice).Translate(), () => grouping = captured));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            RefreshRows();
            if (rows.Count == 0)
            {
                list.Label("LeadYourPet_Feeding_NoResults".Translate());
                return;
            }
            string previousGroup = null;
            foreach (FoodRow row in rows)
            {
                if (previousGroup != row.Group)
                {
                    list.GapLine();
                    list.Label(row.Group);
                    previousGroup = row.Group;
                }
                if (iris) IrisMenusCompat.Anchor(list, "food-" + row.Food.defName);
                bool allowed = settings.AllowsFeedFood(row.Food);
                bool before = allowed;
                list.CheckboxLabeled(row.Food.LabelCap, ref allowed, row.Food.description + "\n" + row.Food.modContentPack?.Name + "\n" + row.Food.defName);
                if (allowed != before) settings.SetFeedFoodAllowed(row.Food, allowed);
            }
        }

        internal IEnumerable<object> SearchEntries()
        {
            EnsureFoods();
            foreach (ThingDef food in foods) yield return IrisMenusCompat.FoodEntry(food);
        }
    }

    internal sealed class Dialog_LeadYourPetFeeding : Window
    {
        private readonly LeadYourPetFeedingPage page = new LeadYourPetFeedingPage();
        private Vector2 position;
        private float height;
        public override Vector2 InitialSize => new Vector2(760f, 700f);

        internal Dialog_LeadYourPetFeeding()
        {
            doCloseButton = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect viewport = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 50f);
            Rect content = new Rect(0f, 0f, viewport.width - 20f, Mathf.Max(viewport.height, height));
            Widgets.BeginScrollView(viewport, ref position, content);
            var list = new Listing_Standard { maxOneColumn = true };
            list.Begin(content);
            try { page.Draw(list, false); }
            finally
            {
                height = list.CurHeight + 4f;
                list.End();
                Widgets.EndScrollView();
            }
        }

        public override void PostClose()
        {
            base.PostClose();
            LeadYourPetMod.Settings.Write();
        }
    }
}
