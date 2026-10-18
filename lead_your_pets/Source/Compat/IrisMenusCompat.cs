using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace LeadYourPet
{
    // No compile reference to IrisMenus. Registration requires support for the running game version.
    internal static class IrisMenusCompat
    {
        internal const string PackageId = "Nanaloveyuki.IrisMenus";

        static bool registered;
        static MethodInfo registerPage;
        static MethodInfo registerSearch;
        static Type searchResult;
        static MethodInfo anchor;
        static MethodInfo section;
        static MethodInfo checkbox;
        static MethodInfo number;
        static MethodInfo select;
        static ConstructorInfo searchEntry;
        static MethodInfo registerSubItem;
        static Type scrollType;

        internal static void TryRegister(LeadYourPetMod owner)
        {
            if (registered || owner == null)
            {
                return;
            }

            ModMetaData meta = ModLister.GetActiveModWithIdentifier(PackageId, false);
            if (meta == null)
            {
                Log.Message("[LeadYourPet] IrisMenus is not active. Menu pages were not registered.");
                return;
            }

            Version currentVersion = VersionControl.CurrentVersion;
            bool supported = meta.SupportedVersionsReadOnly != null &&
                meta.SupportedVersionsReadOnly.Any(item => item != null &&
                    item.Major == currentVersion.Major && item.Minor == currentVersion.Minor);
            if (!supported)
            {
                Log.Warning($"[LeadYourPet] IrisMenus does not list RimWorld {currentVersion.Major}.{currentVersion.Minor}. Menu pages were not registered.");
                return;
            }

            try
            {
                if (!Bind())
                {
                    Log.Warning("[LeadYourPet] IrisMenus public menu API was not found. Menu pages were not registered.");
                    return;
                }

                new IrisMenusPages().Register(owner);
                registered = true;
                Log.Message($"[LeadYourPet] IrisMenus {currentVersion.Major}.{currentVersion.Minor} pages registered.");
            }
            catch (Exception exception)
            {
                Log.Error("[LeadYourPet] IrisMenus registration failed. The mod will keep loading without menu pages.\n" +
                    exception);
            }
        }

        internal static void ResetForTests()
        {
            registered = false;
        }

        static Type TypeByName(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(name, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        static bool Bind()
        {
            Type registry = TypeByName("IrisMenus.MenuRegistry");
            Type controls = TypeByName("IrisMenus.MenuControls");
            Type entry = TypeByName("IrisMenus.MenuSearchEntry");
            scrollType = TypeByName("IrisMenus.MenuScrollView");
            if (registry == null || controls == null || entry == null)
            {
                return false;
            }

            registerPage = OptionalModApi.Resolve(registry, "RegisterSubItemListing", typeof(Mod), typeof(string), typeof(Func<string>), typeof(Action<Listing_Standard>));
            registerSubItem = OptionalModApi.Resolve(registry, "RegisterSubItem", typeof(Mod), typeof(string), typeof(Func<string>), typeof(Action<UnityEngine.Rect>));
            registerSearch = registry.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                {
                    if (method.Name != "RegisterSearchProvider" || method.ContainsGenericParameters)
                    {
                        return false;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length < 3 || parameters[0].ParameterType != typeof(Mod) || parameters[1].ParameterType != typeof(string))
                    {
                        return false;
                    }

                    Type callback = parameters[2].ParameterType;
                    return callback.IsGenericType && callback.GetGenericTypeDefinition() == typeof(Func<>) &&
                        parameters.Skip(3).All(parameter => parameter.IsOptional);
                });
            Type provider = registerSearch?.GetParameters()[2].ParameterType;
            searchResult = provider?.GetGenericArguments()[0];

            anchor = OptionalModApi.Resolve(controls, "Anchor", typeof(Listing_Standard), typeof(string));
            section = OptionalModApi.Resolve(controls, "Section", typeof(Listing_Standard), typeof(string));
            checkbox = OptionalModApi.Resolve(controls, "Checkbox", typeof(Listing_Standard), typeof(string), typeof(bool).MakeByRefType());
            number = controls.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                {
                    if (method.Name != "Number")
                    {
                        return false;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length >= 4 && parameters[2].ParameterType == typeof(int).MakeByRefType();
                });
            select = controls.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method => method.Name == "Select" && method.IsGenericMethodDefinition);
            searchEntry = entry.GetConstructor(new[] { typeof(string), typeof(Func<string>), typeof(Func<string>), typeof(Func<string>) });
            return registerPage != null && registerSubItem != null && scrollType != null && registerSearch != null && searchResult != null && anchor != null &&
                section != null && checkbox != null && number != null && select != null && searchEntry != null;
        }

        internal static void RegisterPage(Mod owner, string pageId, string titleKey, Action<Listing_Standard> draw, Func<IEnumerable<object>> search)
        {
            OptionalModApi.Invoke(registerPage, owner, pageId, (Func<string>)(() => titleKey.Translate()), draw);
            OptionalModApi.Invoke(registerSearch, owner, pageId, BindSearch(searchResult, search));
        }

        internal static void RegisterFeedingPage(Mod owner)
        {
            var page = new LeadYourPetFeedingPage();
            object scroll = Activator.CreateInstance(scrollType);
            MethodInfo draw = scrollType.GetMethod("Draw");
            MethodInfo focus = scrollType.GetMethod("Focus");
            Action<UnityEngine.Rect> render = rect => draw.Invoke(scroll, new object[] { rect, (Action<Listing_Standard>)(list => page.Draw(list, true)) });
            OptionalModApi.Invoke(registerSubItem, owner, "feeding", (Func<string>)(() => "LeadYourPet_Menu_Feeding".Translate()), render);
            Action<string> reveal = id =>
            {
                page.ClearSearch();
                focus.Invoke(scroll, new object[] { id });
            };
            OptionalModApi.Invoke(registerSearch, owner, "feeding", BindSearch(searchResult, page.SearchEntries), reveal);
        }

        internal static object FoodEntry(ThingDef food)
        {
            return searchEntry.Invoke(new object[]
            {
                "food-" + food.defName,
                (Func<string>)(() => food.LabelCap),
                (Func<string>)(() => food.defName + " " + food.modContentPack?.PackageId),
                (Func<string>)(() => food.modContentPack?.Name ?? string.Empty)
            });
        }

        internal static Delegate BindSearch(Type sequence, Func<IEnumerable<object>> search)
        {
            Type element = sequence.IsGenericType ? sequence.GetGenericArguments()[0] : sequence;
            MethodInfo entries = typeof(SearchAdapter).GetMethod("Entries", BindingFlags.Instance | BindingFlags.NonPublic)
                .MakeGenericMethod(element);
            return Delegate.CreateDelegate(typeof(Func<>).MakeGenericType(entries.ReturnType), new SearchAdapter(search), entries);
        }

        sealed class SearchAdapter
        {
            readonly Func<IEnumerable<object>> search;

            internal SearchAdapter(Func<IEnumerable<object>> search)
            {
                this.search = search;
            }

            IEnumerable<T> Entries<T>()
            {
                foreach (object item in search())
                {
                    yield return (T)item;
                }
            }
        }

        internal static void Section(Listing_Standard list, string title)
        {
            OptionalModApi.Invoke(section, list, title);
        }

        internal static void Anchor(Listing_Standard list, string id, float height = 30f)
        {
            OptionalModApi.Invoke(anchor, list, id, height);
        }

        internal static void Checkbox(Listing_Standard list, string label, ref bool value, string tooltip)
        {
            object[] arguments = { list, label, value, tooltip };
            checkbox.Invoke(null, arguments);
            value = (bool)arguments[2];
        }

        internal static void Number(Listing_Standard list, string label, ref int value, ref string buffer, int min, int max)
        {
            object[] arguments = { list, label, value, buffer, min, max };
            number.Invoke(null, arguments);
            value = (int)arguments[2];
            buffer = (string)arguments[3];
        }

        internal static void SelectExit(Listing_Standard list, string label, ref LeashedPawnMapExitBehavior value)
        {
            MethodInfo closed = select.MakeGenericMethod(typeof(LeashedPawnMapExitBehavior));
            LeashedPawnMapExitBehavior[] choices =
            {
                LeashedPawnMapExitBehavior.StayInPlace,
                LeashedPawnMapExitBehavior.LeaveMap,
                LeashedPawnMapExitBehavior.Disappear
            };
            LeashedPawnMapExitBehavior selected = value;
            Action<LeashedPawnMapExitBehavior> chosen = next => selected = next;
            closed.Invoke(null, new object[] { list, label, value, choices, (Func<LeashedPawnMapExitBehavior, string>)ExitLabel, chosen });
            value = selected;
        }

        internal static void SelectBoughtStatus(Listing_Standard list, string label, ref BoughtYoungRatkinStatus value)
        {
            MethodInfo closed = select.MakeGenericMethod(typeof(BoughtYoungRatkinStatus));
            BoughtYoungRatkinStatus[] choices = ModsConfig.IdeologyActive
                ? new[] { BoughtYoungRatkinStatus.Colonist, BoughtYoungRatkinStatus.Prisoner, BoughtYoungRatkinStatus.Slave }
                : new[] { BoughtYoungRatkinStatus.Colonist, BoughtYoungRatkinStatus.Prisoner };
            BoughtYoungRatkinStatus selected = value;
            Action<BoughtYoungRatkinStatus> chosen = next => selected = next;
            closed.Invoke(null, new object[] { list, label, value, choices, (Func<BoughtYoungRatkinStatus, string>)BoughtStatusLabel, chosen });
            value = selected;
        }

        static string BoughtStatusLabel(BoughtYoungRatkinStatus value)
        {
            switch (value)
            {
                case BoughtYoungRatkinStatus.Colonist:
                    return "LeadYourPet_Settings_BoughtYoungRatkinStatus_Colonist".Translate();
                case BoughtYoungRatkinStatus.Slave:
                    return "LeadYourPet_Settings_BoughtYoungRatkinStatus_Slave".Translate();
                default:
                    return "LeadYourPet_Settings_BoughtYoungRatkinStatus_Prisoner".Translate();
            }
        }

        internal static object Entry(string id, string titleKey, string keywords, string contextKey = null)
        {
            return searchEntry.Invoke(new object[]
            {
                id,
                (Func<string>)(() => titleKey.Translate()),
                keywords == null ? null : (Func<string>)(() => keywords),
                contextKey == null ? null : (Func<string>)(() => contextKey.Translate())
            });
        }

        static string ExitLabel(LeashedPawnMapExitBehavior value)
        {
            switch (value)
            {
                case LeashedPawnMapExitBehavior.LeaveMap:
                    return "LeadYourPet_Settings_LeashedPawnMapExitBehavior_LeaveMap".Translate();
                case LeashedPawnMapExitBehavior.Disappear:
                    return "LeadYourPet_Settings_LeashedPawnMapExitBehavior_Disappear".Translate();
                default:
                    return "LeadYourPet_Settings_LeashedPawnMapExitBehavior_StayInPlace".Translate();
            }
        }
    }

    internal sealed class IrisMenusPages
    {
        string lengthBuffer = string.Empty;
        string distanceBuffer = string.Empty;
        string minAgeBuffer = string.Empty;
        string maxAgeBuffer = string.Empty;
        string travelCountBuffer = string.Empty;
        bool lengthVisible;
        bool travelCountVisible;

        internal void Register(Mod owner)
        {
            IrisMenusCompat.RegisterPage(owner, "leash", "LeadYourPet_Menu_Leash", DrawLeash, SearchLeash);
            IrisMenusCompat.RegisterPage(owner, "mouse-egg", "LeadYourPet_Menu_MouseEgg", DrawMouseEgg, SearchMouseEgg);
            IrisMenusCompat.RegisterPage(owner, "age", "LeadYourPet_Menu_Age", DrawAge, SearchAge);
            IrisMenusCompat.RegisterPage(owner, "visitors", "LeadYourPet_Menu_Visitors", DrawVisitors, SearchVisitors);
            IrisMenusCompat.RegisterPage(owner, "map-exit", "LeadYourPet_Menu_MapExit", DrawMapExit, SearchMapExit);
            IrisMenusCompat.RegisterFeedingPage(owner);
        }

        void DrawLeash(Listing_Standard list)
        {
            LeadYourPetSettings settings = LeadYourPetMod.Settings;
            if (settings == null)
            {
                return;
            }

            lengthVisible = !settings.infiniteLeash;
            IrisMenusCompat.Section(list, "LeadYourPet_Menu_Leash".Translate());
            IrisMenusCompat.Anchor(list, "infinite");
            IrisMenusCompat.Checkbox(list, "LeadYourPet_Settings_InfiniteLeash".Translate(), ref settings.infiniteLeash,
                "LeadYourPet_Settings_InfiniteLeash_Tooltip".Translate());
            IrisMenusCompat.Anchor(list, "text");
            IrisMenusCompat.Checkbox(list, "LeadYourPet_Settings_ShowInteractionText".Translate(), ref settings.showInteractionText,
                "LeadYourPet_Settings_ShowInteractionText_Tooltip".Translate());
            IrisMenusCompat.Anchor(list, "animals");
            IrisMenusCompat.Checkbox(list, "LeadYourPet_Settings_AllowAnimalPets".Translate(), ref settings.allowAnimalPets,
                "LeadYourPet_Settings_AllowAnimalPets_Tooltip".Translate());
            IrisMenusCompat.Anchor(list, "hostile-attack");
            IrisMenusCompat.Checkbox(list, "LeadYourPet_Settings_LeashedHostilesCanAttack".Translate(), ref settings.leashedHostilesCanAttack,
                "LeadYourPet_Settings_LeashedHostilesCanAttack_Tooltip".Translate());
            if (!lengthVisible)
            {
                return;
            }

            IrisMenusCompat.Anchor(list, "length", 34f);
            if (lengthBuffer.Length == 0)
            {
                lengthBuffer = settings.maxLeashLength.ToString();
            }

            IrisMenusCompat.Number(list, "LeadYourPet_Settings_MaxLeashLength".Translate(), ref settings.maxLeashLength,
                ref lengthBuffer, 3, 30);
            settings.Clamp();
        }

        void DrawMouseEgg(Listing_Standard list)
        {
            LeadYourPetSettings settings = LeadYourPetMod.Settings;
            if (settings == null)
            {
                return;
            }
            IrisMenusCompat.Section(list, "LeadYourPet_Menu_MouseEgg".Translate());
            IrisMenusCompat.Anchor(list, "mouse-eggs");
            IrisMenusCompat.Checkbox(list, "LeadYourPet_Settings_AllowRatkinYoungPets".Translate(), ref settings.allowRatkinYoungPets,
                "LeadYourPet_Settings_AllowRatkinYoungPets_Tooltip".Translate());
            IrisMenusCompat.Anchor(list, "distance", 34f);
            if (distanceBuffer.Length == 0)
            {
                distanceBuffer = settings.maxMouseEggPetLeashStartDistance.ToString();
            }

            IrisMenusCompat.Number(list, "LeadYourPet_Settings_MouseEggLeashStartDistance".Translate(),
                ref settings.maxMouseEggPetLeashStartDistance, ref distanceBuffer, 1, 30);
            settings.Clamp();
        }

        void DrawAge(Listing_Standard list)
        {
            LeadYourPetSettings settings = LeadYourPetMod.Settings;
            if (settings == null)
            {
                return;
            }

            IrisMenusCompat.Section(list, "LeadYourPet_Menu_Age".Translate());
            IrisMenusCompat.Anchor(list, "ignore-age");
            IrisMenusCompat.Checkbox(list, "LeadYourPet_Settings_IgnorePetAge".Translate(), ref settings.ignorePetAge,
                "LeadYourPet_Settings_IgnorePetAge_Tooltip".Translate());
            list.Label("LeadYourPet_Settings_PetAge_Note".Translate());
            IrisMenusCompat.Anchor(list, "min-age", 34f);
            if (minAgeBuffer.Length == 0)
            {
                minAgeBuffer = settings.minPetAgeYears.ToString();
            }

            IrisMenusCompat.Number(list, "LeadYourPet_Settings_MinPetAge".Translate(settings.minPetAgeYears),
                ref settings.minPetAgeYears, ref minAgeBuffer, 0, LeadYourPetSettings.AbsoluteMaxPetAgeYears);
            if (settings.maxPetAgeYears < settings.minPetAgeYears)
            {
                settings.maxPetAgeYears = settings.minPetAgeYears;
                maxAgeBuffer = settings.maxPetAgeYears.ToString();
            }

            IrisMenusCompat.Anchor(list, "max-age", 34f);
            if (maxAgeBuffer.Length == 0)
            {
                maxAgeBuffer = settings.maxPetAgeYears.ToString();
            }

            IrisMenusCompat.Number(list, "LeadYourPet_Settings_MaxPetAge".Translate(settings.maxPetAgeYears),
                ref settings.maxPetAgeYears, ref maxAgeBuffer, settings.minPetAgeYears, LeadYourPetSettings.AbsoluteMaxPetAgeYears);
            settings.Clamp();
        }

        void DrawVisitors(Listing_Standard list)
        {
            LeadYourPetSettings settings = LeadYourPetMod.Settings;
            if (settings == null)
            {
                return;
            }

            travelCountVisible = settings.visitorsLeadRatkinYoung;
            IrisMenusCompat.Section(list, "LeadYourPet_Menu_Visitors".Translate());
            IrisMenusCompat.Anchor(list, "visitors-lead");
            IrisMenusCompat.Checkbox(list, "LeadYourPet_Settings_VisitorsLeadRatkinYoung".Translate(), ref settings.visitorsLeadRatkinYoung,
                "LeadYourPet_Settings_VisitorsLeadRatkinYoung_Tooltip".Translate());
            if (travelCountVisible)
            {
                IrisMenusCompat.Anchor(list, "travel-count", 34f);
                if (travelCountBuffer.Length == 0)
                {
                    travelCountBuffer = settings.ordinaryTravelRatkinYoungCount.ToString();
                }

                IrisMenusCompat.Number(list, "LeadYourPet_Settings_OrdinaryTravelRatkinYoungCount".Translate(settings.ordinaryTravelRatkinYoungCount),
                    ref settings.ordinaryTravelRatkinYoungCount, ref travelCountBuffer, 0, LeadYourPetSettings.MaxOrdinaryTravelRatkinYoungCount);
                settings.Clamp();
            }

            IrisMenusCompat.Anchor(list, "bought-status");
            IrisMenusCompat.SelectBoughtStatus(list, "LeadYourPet_Settings_BoughtYoungRatkinStatus".Translate(),
                ref settings.boughtYoungRatkinStatus);
        }

        void DrawMapExit(Listing_Standard list)
        {
            LeadYourPetSettings settings = LeadYourPetMod.Settings;
            if (settings == null)
            {
                return;
            }

            IrisMenusCompat.Section(list, "LeadYourPet_Menu_MapExit".Translate());
            IrisMenusCompat.Anchor(list, "exit");
            IrisMenusCompat.SelectExit(list, "LeadYourPet_Settings_LeashedPawnMapExitBehavior".Translate(),
                ref settings.leashedPawnMapExitBehavior);
        }

        IEnumerable<object> SearchLeash()
        {
            yield return IrisMenusCompat.Entry("infinite", "LeadYourPet_Settings_InfiniteLeash", "leash length infinite",
                "LeadYourPet_Settings_InfiniteLeash_Tooltip");
            yield return IrisMenusCompat.Entry("text", "LeadYourPet_Settings_ShowInteractionText", "mote text",
                "LeadYourPet_Settings_ShowInteractionText_Tooltip");
            yield return IrisMenusCompat.Entry("animals", "LeadYourPet_Settings_AllowAnimalPets", "animal pet",
                "LeadYourPet_Settings_AllowAnimalPets_Tooltip");
            yield return IrisMenusCompat.Entry("hostile-attack", "LeadYourPet_Settings_LeashedHostilesCanAttack", "hostile enemy faction attack 敌对 派系 攻击",
                "LeadYourPet_Settings_LeashedHostilesCanAttack_Tooltip");
            if (lengthVisible)
            {
                yield return IrisMenusCompat.Entry("length", "LeadYourPet_Settings_MaxLeashLength", "max leash length");
            }
        }

        IEnumerable<object> SearchMouseEgg()
        {
            yield return IrisMenusCompat.Entry("mouse-eggs", "LeadYourPet_Settings_AllowRatkinYoungPets", "humanlike adult prisoner young ratkin leash 智人 成年 囚犯 牵引 鼠蛋",
                "LeadYourPet_Settings_AllowRatkinYoungPets_Tooltip");
            yield return IrisMenusCompat.Entry("distance", "LeadYourPet_Settings_MouseEggLeashStartDistance", "humanlike leash start distance 智人 牵引 起始 距离");
        }

        IEnumerable<object> SearchAge()
        {
            yield return IrisMenusCompat.Entry("ignore-age", "LeadYourPet_Settings_IgnorePetAge", "adult prisoner age limit 成年 囚犯 年龄",
                "LeadYourPet_Settings_IgnorePetAge_Tooltip");
            yield return IrisMenusCompat.Entry("min-age", "LeadYourPet_Settings_MinPetAge", "minimum age years",
                "LeadYourPet_Settings_MinPetAge_Tooltip");
            yield return IrisMenusCompat.Entry("max-age", "LeadYourPet_Settings_MaxPetAge", "maximum age years",
                "LeadYourPet_Settings_MaxPetAge_Tooltip");
        }

        IEnumerable<object> SearchVisitors()
        {
            yield return IrisMenusCompat.Entry("visitors-lead", "LeadYourPet_Settings_VisitorsLeadRatkinYoung", "visitor trader caravan young ratkin",
                "LeadYourPet_Settings_VisitorsLeadRatkinYoung_Tooltip");
            if (travelCountVisible)
            {
                yield return IrisMenusCompat.Entry("travel-count", "LeadYourPet_Settings_OrdinaryTravelRatkinYoungCount", "caravan young ratkin count");
            }
            yield return IrisMenusCompat.Entry("bought-status", "LeadYourPet_Settings_BoughtYoungRatkinStatus", "buy colonist prisoner slave trade 购买 殖民者 囚犯 奴隶",
                "LeadYourPet_Settings_BoughtYoungRatkinStatus_Tooltip");
        }

        IEnumerable<object> SearchMapExit()
        {
            yield return IrisMenusCompat.Entry("exit", "LeadYourPet_Settings_LeashedPawnMapExitBehavior", "map exit stay leave disappear");
        }
    }
}
