using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Identity;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.EventMgr
{
    internal static class RHAH_LongAdoptionFollow
    {
        internal static bool Ready(RHAH_LongFollowRecord r, Map map, int tick)
        {
            string id = RHAH_LongFollowRuntime.ParentId(r.DisplayId);
            Verse.Pawn subject = RHAH_LongFollowRuntime.Pawn(r.SubjectId, tick);
            Verse.Pawn visitor = r.VisitorIds.Count == 0 ? null : RHAH_LongFollowRuntime.Pawn(r.VisitorIds[0], tick);
            if (id == "I-013" || id == "I-019")
            {
                if (r.FoodAlternative) return true;
                if (r.Phase == 0)
                {
                    if (r.DepartedIds.Count == 0) return r.DueTick >= 0 && tick >= r.DueTick;
                    if (!RHAH_LongFollowRuntime.Alive(subject)) return true;
                    return tick >= r.DueTick && (id == "I-013" ? RHAH_LongFollowRuntime.Adult(subject) :
                        subject.DevelopmentalStage != DevelopmentalStage.Baby && RHAH_LongFollowRuntime.CanBring(subject, map));
                }
                if (id == "I-013")
                {
                    if (!RHAH_LongFollowRuntime.Alive(visitor) && r.Phase < 7) return true;
                    if (r.Phase == 5) return true;
                    return (r.Phase == 2 || r.Phase == 3 || r.Phase == 4 || r.Phase == 7) && tick >= r.DueTick;
                }
                if (!RHAH_LongFollowRuntime.Alive(subject)) return true;
                if (subject.MapHeld != map && subject.Faction != Faction.OfPlayer && r.Phase >= 6) return true;
                if (r.Phase == 5) return subject.MapHeld != map;
                return (r.Phase == 3 || r.Phase == 4 || r.Phase == 6 && !RHAH_LongFollowRuntime.MedicallyPaused(subject)) && tick >= r.DueTick;
            }
            if (id == "I-032")
            {
                if (r.Phase == 0)
                {
                    if (tick < r.DueTick) return false;
                    for (int i = 0; i < r.ChildIds.Count; i++)
                        if (RHAH_LongFollowRuntime.AtHome(RHAH_LongFollowRuntime.Pawn(r.ChildIds[i], tick), map)) return true;
                    return false;
                }
                if (r.Phase == 4) return RHAH_LongFollowRuntime.Adult(subject);
                if (r.Phase == 5) return tick >= r.DueTick && (!r.FollowupSelected || !RHAH_LongFollowRuntime.Alive(subject) || subject.MapHeld != map);
                if (r.Phase == 8) return tick >= r.DueTick && (!RHAH_LongFollowRuntime.Alive(subject) || RHAH_LongFollowRuntime.CanBring(subject, map));
                return false;
            }
            if (id != "I-033") return true;
            Verse.Pawn original = RHAH_LongFollowRuntime.Pawn(r.SourceAdultId == 0 ? r.ResponsibleId : r.SourceAdultId, tick);
            if (r.SourceAdultId == 0 && original != null) r.SourceAdultId = original.thingIDNumber;
            if (!RHAH_LongFollowRuntime.Alive(subject) || !RHAH_LongFollowRuntime.Alive(original)) return true;
            if (r.Phase == 0) return RHAH_LongFollowRuntime.AtHome(subject, map) && subject.DevelopmentalStage != DevelopmentalStage.Baby;
            if (r.Phase == 1 || r.Phase == 2)
            {
                if (!RHAH_LongFollowRuntime.AtHome(subject, map) || subject.IsPrisoner || subject.IsSlave || RHAH_LongFollowRuntime.Adult(subject)) return true;
                if (tick >= r.DueTick && (r.Phase == 2 || !r.LetterOpen)) return true;
                Verse.Pawn caregiver = r.RehomeCount >= 2 || r.Phase == 2 ? RHAH_LongFollowRuntime.Pawn(r.ResponsibleId, tick) : original;
                if (FollowReady(subject, caregiver, map)) return true;
                List<Thought_Memory> memories = subject.needs?.mood?.thoughts?.memories?.Memories;
                if (subject.needs?.food?.CurLevelPercentage >= 0.9f && memories != null)
                    for (int i = 0; i < memories.Count; i++)
                        if (memories[i].def.defName == "RHAH_LongBiteForFood") return true;
                return false;
            }
            if (r.Phase == 3) return tick >= r.DueTick && (subject.MapHeld == map || !r.FollowupSelected || RHAH_LongFollowRuntime.CanBring(subject, map));
            return r.Phase == 4 && tick >= r.DueTick;
        }

        static bool FollowReady(Verse.Pawn child, Verse.Pawn adult, Map map)
        {
            if (!RHAH_LongFollowRuntime.Alive(adult) || !child.Spawned || adult.MapHeld != map || child.Drafted || child.IsPrisoner || child.IsSlave || RHAH_LongFollowRuntime.MedicallyPaused(child)) return false;
            if (child.CurJobDef == JobDefOf.Ingest || child.CurJobDef == JobDefOf.Steal || child.CurJobDef == JobDefOf.TakeFromOtherInventory) return false;
            if (child.needs?.food?.CurLevelPercentage < 0.5f && adult.inventory != null)
            {
                ThingOwner<Thing> inventory = adult.inventory.innerContainer;
                for (int i = 0; i < inventory.Count; i++)
                    if (inventory[i].def.IsNutritionGivingIngestible && FoodUtility.WillEat(child, inventory[i])) return true;
            }
            return adult.InBed() && GenLocalDate.HourInteger(map) >= 20 || adult.Position.DistanceToSquared(child.Position) > 16 && child.CanReach(adult, PathEndMode.Touch, Danger.Some);
        }

        internal static bool Tick(RHAH_LongFollowRecord r, Map map, int tick)
        {
            switch (RHAH_LongFollowRuntime.ParentId(r.DisplayId))
            {
                case "I-013": return TickExchange(r, map, tick);
                case "I-019": return TickTaken(r, map, tick);
                case "I-032": return TickDrop(r, map, tick);
                case "I-033": return TickAttachment(r, map, tick);
                default: RHAH_LongFollowRuntime.Complete(r); return true;
            }
        }

        internal static IEnumerable<RHAH_LongOption> Options(RHAH_LongFollowRecord r, Map map)
        {
            string id = RHAH_LongFollowRuntime.ParentId(r.DisplayId);
            if (id == "I-013")
            {
                if (r.Phase == 1)
                {
                    foreach (Verse.Pawn adult in map.mapPawns.FreeColonistsSpawned)
                    {
                        if (!RHAH_LongFollowRuntime.Adult(adult)) continue;
                        yield return new RHAH_LongOption("recognize_feed", "RHAH_Long_RecognizeFeed", adult.thingIDNumber, RHAH_LongFollowRuntime.CanPay(map, r.FoodCount) ? null : "RHAH_Long_NoFood");
                        yield return new RHAH_LongOption("recognize", "RHAH_Long_Recognize", adult.thingIDNumber);
                    }
                    yield return new RHAH_LongOption("deny", "RHAH_Long_Deny");
                    yield return new RHAH_LongOption("capture", "RHAH_Long_Capture");
                }
                if (r.Phase == 8)
                {
                    yield return new RHAH_LongOption("pay_letter", "RHAH_Long_Pay", 0, RHAH_LongFollowRuntime.CanPay(map, r.FoodCount) ? null : "RHAH_Long_NoFood");
                    yield return new RHAH_LongOption("refuse_letter", "RHAH_Long_Refuse");
                }
            }
            else if (id == "I-019")
            {
                if (r.Phase == 1)
                {
                    yield return new RHAH_LongOption("welcome", "RHAH_Long_Welcome", 0, RHAH_LongFollowRuntime.CanPay(map, r.FoodCount) ? null : "RHAH_Long_NoFood");
                    yield return new RHAH_LongOption("reject", "RHAH_Long_RejectEntry");
                    yield return new RHAH_LongOption("capture", "RHAH_Long_Capture");
                }
                if (r.Phase == 2)
                {
                    foreach (Verse.Pawn adult in map.mapPawns.FreeColonistsSpawned)
                    {
                        if (RHAH_LongFollowRuntime.Adult(adult)) yield return new RHAH_LongOption("point", "RHAH_Long_Point", adult.thingIDNumber);
                    }
                    yield return new RHAH_LongOption("no_point", "RHAH_Long_NoPoint");
                }
                if (r.Phase == 7) yield return new RHAH_LongOption("ask_next", "RHAH_Long_AskNext");
                if (r.Phase == 9)
                {
                    if (r.Branch == 1) yield return new RHAH_LongOption("buy_direction", "RHAH_Long_Pay", 0, RHAH_LongFollowRuntime.CanPay(map, r.FoodCount) ? null : "RHAH_Long_NoFood");
                    yield return new RHAH_LongOption("end_direction", "RHAH_Long_StopAsking");
                }
            }
            else if (id == "I-032")
            {
                if (r.Phase == 1)
                {
                    yield return new RHAH_LongOption("match", "RHAH_Long_MatchCloth");
                    yield return new RHAH_LongOption("no_match", "RHAH_Long_DoNotMatch");
                    yield return new RHAH_LongOption("ask_child", "RHAH_Long_AskChild");
                    yield return new RHAH_LongOption("expel", "RHAH_Long_Expel");
                }
                if (r.Phase == 2)
                {
                    for (int i = 0; i < r.ChildIds.Count; i++)
                    {
                        Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(r.ChildIds[i], RHAH_LongFollowRuntime.Now);
                        if (RHAH_LongFollowRuntime.AtHome(child, map)) yield return new RHAH_LongOption("toggle_child", r.SelectedIds.Contains(child.thingIDNumber) ? "RHAH_Long_UnselectChild" : "RHAH_Long_SelectChild", child.thingIDNumber);
                    }
                    yield return new RHAH_LongOption("send", "RHAH_Long_SendSelected");
                    yield return new RHAH_LongOption("keep", "RHAH_Long_KeepAll");
                }
                if (r.Phase == 3)
                {
                    yield return new RHAH_LongOption("food", "RHAH_Long_GiveFood", 0, RHAH_LongFollowRuntime.CanPay(map, r.FoodCount) ? null : "RHAH_Long_NoFood");
                    yield return new RHAH_LongOption("no_food", "RHAH_Long_Refuse");
                }
                if (r.Phase == 7)
                {
                    yield return new RHAH_LongOption("return", "RHAH_Long_RequestReturn");
                    yield return new RHAH_LongOption("no_return", "RHAH_Long_NoReturn");
                }
            }
            else if (id == "I-033" && r.Phase == 1)
            {
                yield return new RHAH_LongOption("feed", "RHAH_Long_KeepFeeding", 0, RHAH_LongFollowRuntime.CanPay(map, 1) ? null : "RHAH_Long_NoFood");
                yield return new RHAH_LongOption("stop", "RHAH_Long_StopFeeding");
                foreach (Verse.Pawn adult in map.mapPawns.FreeColonistsSpawned)
                {
                    if (RHAH_LongFollowRuntime.Adult(adult) && adult.thingIDNumber != r.SourceAdultId) yield return new RHAH_LongOption("rehome", "RHAH_Long_Rehome", adult.thingIDNumber);
                }
                yield return new RHAH_LongOption("expel", "RHAH_Long_Expel");
            }
        }

        internal static bool Choose(RHAH_LongFollowRecord r, Map map, string action, int pawnId, int tick)
        {
            Verse.Pawn subject = RHAH_LongFollowRuntime.Pawn(r.SubjectId, tick);
            Verse.Pawn visitor = r.VisitorIds.Count == 0 ? null : RHAH_LongFollowRuntime.Pawn(r.VisitorIds[0], tick);
            string id = RHAH_LongFollowRuntime.ParentId(r.DisplayId);
            if (id == "I-013")
            {
                if (action == "recognize_feed")
                {
                    if (!RHAH_LongFollowRuntime.TryPay(map, r.FoodCount, recipient: visitor)) return false;
                    r.ResponsibleId = pawnId;
                    List<Verse.Pawn> adults = new List<Verse.Pawn>();
                    foreach (Verse.Pawn adult in map.mapPawns.FreeColonistsSpawned) if (RHAH_LongFollowRuntime.Adult(adult) && (adult.thingIDNumber == pawnId || adult.relations?.DirectRelationExists(PawnRelationDefOf.Parent, subject) == true)) adults.Add(adult);
                    RHAH_LongFollowRuntime.ShareMood(adults, "RHAH_LongExchangeAlive", -3);
                    r.Phase = 2; r.DueTick = RHAH_LongFollowRuntime.AddTicks(tick, 3 * GenDate.TicksPerDay);
                    RHAH_LongFollowRuntime.Say(r, "RHAH_Long_TraderDirection");
                }
                else if (action == "recognize")
                {
                    r.ResponsibleId = pawnId;
                    RHAH_LongFollowRuntime.Mood(RHAH_LongFollowRuntime.Pawn(pawnId, tick), "RHAH_LongRecognizedUnfed", -6);
                    RHAH_LongFollowRuntime.Leave(visitor); r.Phase = 5;
                }
                else if (action == "deny") { r.Phase = 3; r.DueTick = RHAH_LongFollowRuntime.AddTicks(tick, GenDate.TicksPerDay); }
                else if (action == "capture")
                {
                    if (!RHAH_LongFollowRuntime.Capture(r, map)) return false;
                    r.Phase = 4; r.DueTick = RHAH_LongFollowRuntime.AddTicks(tick, 2 * GenDate.TicksPerDay);
                }
                else if (action == "pay_letter")
                {
                    if (!RHAH_LongFollowRuntime.TryPay(map, r.FoodCount)) return false;
                    RHAH_LongFollowRuntime.Complete(r);
                    return true;
                }
                else if (action == "refuse_letter") { r.Phase = 9; r.DueTick = -1; }
                else return false;
                RHAH_LongFollowRuntime.CloseLetter(r);
                return true;
            }
            if (id == "I-019")
            {
                if (action == "welcome")
                {
                    if (!RHAH_LongFollowRuntime.TryPay(map, r.FoodCount, recipient: subject)) return false;
                    r.Phase = 2; RHAH_LongFollowRuntime.OpenLetter(r, map); return true;
                }
                if (action == "point")
                {
                    r.ResponsibleId = pawnId;
                    RHAH_LongFollowRuntime.Mood(RHAH_LongFollowRuntime.Pawn(pawnId, tick), "RHAH_LongYouHandedMeAway", -8, true);
                    RHAH_LongFollowRuntime.Mood(subject, "RHAH_LongFoundYou", 2);
                    r.Phase = 6; r.DueTick = RHAH_LongFollowRuntime.AddTicks(tick, 3 * GenDate.TicksPerDay);
                }
                else if (action == "no_point")
                {
                    Verse.Pawn wrong = null;
                    foreach (Verse.Pawn adult in map.mapPawns.FreeColonistsSpawned) if (RHAH_LongFollowRuntime.Adult(adult) && (wrong == null || adult.thingIDNumber < wrong.thingIDNumber)) wrong = adult;
                    r.Branch = Rand.Bool ? 1 : 0;
                    if (r.Branch == 1 && wrong != null) { r.ResponsibleId = wrong.thingIDNumber; RHAH_LongFollowRuntime.Mood(wrong, "RHAH_LongWhyMe", -3); }
                    r.Phase = 3; r.DueTick = RHAH_LongFollowRuntime.AddTicks(tick, GenDate.TicksPerDay);
                }
                else if (action == "reject") { r.Phase = 4; r.DueTick = RHAH_LongFollowRuntime.AddTicks(tick, (24 - GenLocalDate.HourInteger(map)) * GenDate.TicksPerHour); }
                else if (action == "capture")
                {
                    if (!RHAH_LongFollowRuntime.Capture(r, map)) return false;
                    r.Phase = 6; r.DueTick = RHAH_LongFollowRuntime.AddTicks(tick, 3 * GenDate.TicksPerDay);
                }
                else if (action == "ask_next") { r.Phase = 8; r.DueTick = -1; }
                else if (action == "buy_direction")
                {
                    if (!RHAH_LongFollowRuntime.TryPay(map, r.FoodCount)) return false;
                    RHAH_LongFollowRuntime.Say(r, r.FollowupSelected ? "RHAH_Long_DirectionMatches" : "RHAH_Long_DirectionDoesNotMatch");
                    RHAH_LongFollowRuntime.Complete(r); return true;
                }
                else if (action == "end_direction") { RHAH_LongFollowRuntime.Complete(r); return true; }
                else if (action != "point") return false;
                RHAH_LongFollowRuntime.CloseLetter(r); return true;
            }
            if (id == "I-032")
            {
                if (action == "match") { r.Phase = 2; RHAH_LongFollowRuntime.OpenLetter(r, map); return true; }
                if (action == "toggle_child")
                {
                    if (!r.SelectedIds.Remove(pawnId)) r.SelectedIds.Add(pawnId);
                    RHAH_LongFollowRuntime.OpenLetter(r, map); return true;
                }
                if (action == "send")
                {
                    if (r.SelectedIds.Count == 0) { RHAH_LongFollowRuntime.Leave(visitor); RHAH_LongFollowRuntime.Complete(r); return true; }
                    if (!RHAH_LongFollowRuntime.SendChildren(r, map, r.SelectedIds, visitor)) return false;
                    r.SubjectId = r.SelectedIds[0]; r.Phase = 5;
                    r.FollowupSelected = Rand.Bool; r.DueTick = RHAH_LongFollowRuntime.AddTicks(tick, 15 * GenDate.TicksPerDay);
                }
                else if (action == "keep") { RHAH_LongFollowRuntime.Leave(visitor); RHAH_LongFollowRuntime.Complete(r); return true; }
                else if (action == "no_match") { r.Phase = 3; RHAH_LongFollowRuntime.OpenLetter(r, map); return true; }
                else if (action == "food") { if (!RHAH_LongFollowRuntime.TryPay(map, r.FoodCount, recipient: visitor)) return false; RHAH_LongFollowRuntime.Leave(visitor); RHAH_LongFollowRuntime.Complete(r); return true; }
                else if (action == "no_food") { RHAH_LongFollowRuntime.Leave(visitor); RHAH_LongFollowRuntime.Complete(r); return true; }
                else if (action == "ask_child")
                {
                    r.Phase = 4;
                    RHAH_LongFollowRuntime.Say(r, "RHAH_Long_ChildCannotRemember");
                    RHAH_LongFollowRuntime.Leave(visitor);
                }
                else if (action == "expel")
                {
                    for (int i = 0; i < r.ChildIds.Count; i++)
                    {
                        Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(r.ChildIds[i], tick);
                        if (RHAH_LongFollowRuntime.AtHome(child, map) && child.DevelopmentalStage != DevelopmentalStage.Baby) RHAH_LongFollowRuntime.Mood(child, "RHAH_LongSomeoneCameForMe", -2);
                    }
                    RHAH_LongFollowRuntime.Leave(visitor); RHAH_LongFollowRuntime.Complete(r); return true;
                }
                else if (action == "return") { r.ReturnRequested = true; r.Phase = 8; r.DueTick = RHAH_LongFollowRuntime.AddTicks(tick, 5 * GenDate.TicksPerDay); }
                else if (action == "no_return") { RHAH_LongFollowRuntime.Complete(r); return true; }
                else return false;
                RHAH_LongFollowRuntime.CloseLetter(r); return true;
            }
            if (id == "I-033")
            {
                if (action == "feed") { if (!RHAH_LongFollowRuntime.TryPay(map, 1, recipient: subject)) return false; r.Branch = 0; RHAH_LongFollowRuntime.RemoveMood(subject, "RHAH_LongBiteForFood"); }
                else if (action == "stop")
                {
                    r.Branch = Rand.Bool ? 1 : 2;
                    Verse.Pawn adult = RHAH_LongFollowRuntime.Pawn(r.SourceAdultId, tick);
                    if (r.Branch == 1 && RHAH_LongFollowRuntime.Alive(adult) && RHAH_LongFollowRuntime.Alive(subject))
                    {
                        adult.TakeDamage(new DamageInfo(DamageDefOf.Bite, 3f, instigator: subject));
                        RHAH_LongFollowRuntime.Mood(subject, "RHAH_LongBiteForFood", 2, true);
                    }
                    else RHAH_LongFollowRuntime.StartFoodTheft(subject, map, false);
                }
                else if (action == "rehome")
                {
                    r.RehomeCount++;
                    r.ResponsibleId = pawnId;
                    r.Phase = 2; r.DueTick = RHAH_LongFollowRuntime.AddTicks(tick, GenDate.TicksPerDay);
                    RHAH_LongFollowRuntime.CloseLetter(r);
                    Follow(subject, RHAH_LongFollowRuntime.Pawn(pawnId, tick), map, tick);
                    return true;
                }
                else if (action == "expel")
                {
                    if (!RHAH_LongFollowRuntime.SpawnVisitors(r, map, 1)) return false;
                    r.SelectedIds.Clear(); r.SelectedIds.Add(r.SubjectId);
                    Verse.Pawn receiver = RHAH_LongFollowRuntime.Pawn(r.VisitorIds[r.VisitorIds.Count - 1], tick);
                    if (!RHAH_LongFollowRuntime.SendChildren(r, map, r.SelectedIds, receiver)) return false;
                    r.Phase = 3; r.FollowupSelected = Rand.Bool;
                    r.DueTick = RHAH_LongFollowRuntime.AddTicks(tick, 15 * GenDate.TicksPerDay);
                    RHAH_LongFollowRuntime.CloseLetter(r); return true;
                }
                else return false;
                RHAH_LongFollowRuntime.CloseLetter(r); r.DueTick = RHAH_LongFollowRuntime.AddTicks(tick, GenDate.TicksPerDay); return true;
            }
            return false;
        }

        static bool TickExchange(RHAH_LongFollowRecord r, Map map, int tick)
        {
            Verse.Pawn outgoing = RHAH_LongFollowRuntime.Pawn(r.SubjectId, tick);
            if (r.FoodAlternative) { RHAH_LongFollowRuntime.Complete(r); return true; }
            if (r.Phase == 0)
            {
                // 历史记录没有交出的孩子时不挑无关殖民者补身份
                if (r.DepartedIds.Count == 0) { if (r.DueTick >= 0 && tick >= r.DueTick) RHAH_LongFollowRuntime.Complete(r); return false; }
                if (!RHAH_LongFollowRuntime.Alive(outgoing)) { RHAH_LongFollowRuntime.Complete(r); return true; }
                if (!RHAH_LongFollowRuntime.Adult(outgoing) || tick < r.DueTick) return false;
                if (!RHAH_LongFollowRuntime.SpawnVisitors(r, map, 1)) return false;
                r.Phase = 1; r.Opened = true;
                RHAH_LongFollowRuntime.OpenLetter(r, map); return true;
            }
            Verse.Pawn visitor = r.VisitorIds.Count == 0 ? null : RHAH_LongFollowRuntime.Pawn(r.VisitorIds[0], tick);
            if (!RHAH_LongFollowRuntime.Alive(visitor) && r.Phase < 7) { RHAH_LongFollowRuntime.Complete(r); return true; }
            if (r.Phase == 2 && tick >= r.DueTick) { RHAH_LongFollowRuntime.Leave(visitor); r.Phase = 5; return true; }
            if (r.Phase == 3 && tick >= r.DueTick)
            {
                RHAH_LongFollowRuntime.StartFoodTheft(visitor, map); RHAH_LongFollowRuntime.Complete(r); return true;
            }
            if (r.Phase == 4 && tick >= r.DueTick && RHAH_LongFollowRuntime.Alive(visitor))
            {
                RHAH_LongFollowRuntime.Mood(visitor, "RHAH_LongFoodAnyway", 4);
                RHAH_LongFollowRuntime.Say(r, "RHAH_Long_ExchangeRetraction");
                RHAH_LongFollowRuntime.Complete(r); return true;
            }
            if (r.Phase == 5)
            {
                if (visitor.MapHeld == map) { RHAH_LongFollowRuntime.Leave(visitor); return false; }
                r.Phase = 7; r.FollowupSelected = Rand.Bool; r.DueTick = RHAH_LongFollowRuntime.AddTicks(tick, 15 * GenDate.TicksPerDay); return true;
            }
            if (r.Phase == 7 && tick >= r.DueTick)
            {
                if (!r.FollowupSelected) { RHAH_LongFollowRuntime.Complete(r); return true; }
                r.Phase = 8; RHAH_LongFollowRuntime.OpenLetter(r, map); return true;
            }
            return false;
        }

        static bool TickTaken(RHAH_LongFollowRecord r, Map map, int tick)
        {
            if (r.FoodAlternative) { RHAH_LongFollowRuntime.Complete(r); return true; }
            Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(r.SubjectId, tick);
            if (r.Phase == 0)
            {
                if (r.DepartedIds.Count == 0) { if (r.DueTick >= 0 && tick >= r.DueTick) RHAH_LongFollowRuntime.Complete(r); return false; }
                if (!RHAH_LongFollowRuntime.Alive(child)) { RHAH_LongFollowRuntime.Complete(r); return true; }
                if (child.DevelopmentalStage == DevelopmentalStage.Baby || tick < r.DueTick || !RHAH_LongFollowRuntime.Bring(child, map)) return false;
                r.VisitorIds.Clear(); r.VisitorIds.Add(child.thingIDNumber);
                r.Phase = 1; r.Opened = true; r.FollowupSelected = Rand.Bool;
                RHAH_LongFollowRuntime.OpenLetter(r, map); return true;
            }
            if (!RHAH_LongFollowRuntime.Alive(child)) { RHAH_LongFollowRuntime.Complete(r); return true; }
            if (child.MapHeld != map && child.Faction != Faction.OfPlayer && r.Phase >= 6)
            {
                RHAH_LongFollowRuntime.Mood(child, "RHAH_LongHandedAwayAgain", -10, true);
                RHAH_LongFollowRuntime.Complete(r); return true;
            }
            if (r.Phase == 3 && tick >= r.DueTick)
            {
                RHAH_LongFollowRuntime.Say(r, "RHAH_Long_CannotRecognize");
                r.Phase = 6; r.DueTick = RHAH_LongFollowRuntime.AddTicks(tick, 2 * GenDate.TicksPerDay); return true;
            }
            if (r.Phase == 4 && tick >= r.DueTick)
            {
                RHAH_LongFollowRuntime.StartFoodTheft(child, map); r.Phase = 5; return true;
            }
            if (r.Phase == 5 && child.MapHeld != map)
            {
                if (r.RevisitCount >= 1) { RHAH_LongFollowRuntime.Complete(r); return true; }
                r.RevisitCount++; r.Phase = 0; r.DueTick = RHAH_LongFollowRuntime.AddTicks(tick, 15 * GenDate.TicksPerDay); return true;
            }
            if (r.Phase == 6 && tick >= r.DueTick && !RHAH_LongFollowRuntime.MedicallyPaused(child))
            {
                r.Phase = 7; RHAH_LongFollowRuntime.OpenLetter(r, map); return true;
            }
            return false;
        }

        static bool TickDrop(RHAH_LongFollowRecord r, Map map, int tick)
        {
            if (r.Phase == 0)
            {
                Verse.Pawn focus = null;
                for (int i = 0; i < r.ChildIds.Count; i++)
                {
                    Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(r.ChildIds[i], tick);
                    if (RHAH_LongFollowRuntime.AtHome(child, map)) { focus = child; break; }
                }
                if (focus == null || tick < r.DueTick) return false;
                r.SubjectId = focus.thingIDNumber;
                if (!RHAH_LongFollowRuntime.SpawnVisitors(r, map, 1)) return false;
                Verse.Pawn visitor = RHAH_LongFollowRuntime.Pawn(r.VisitorIds[0], tick);
                Thing cloth = ThingMaker.MakeThing(ThingDefOf.Cloth); cloth.stackCount = 1;
                visitor.inventory.innerContainer.TryAdd(cloth);
                if (r.Keepsake == null) { r.Keepsake = ThingMaker.MakeThing(ThingDefOf.Cloth); r.Keepsake.stackCount = 1; focus.inventory.innerContainer.TryAdd(r.Keepsake); }
                r.Phase = 1; r.Opened = true; RHAH_LongFollowRuntime.OpenLetter(r, map); return true;
            }
            Verse.Pawn subject = RHAH_LongFollowRuntime.Pawn(r.SubjectId, tick);
            if (r.Phase == 4 && RHAH_LongFollowRuntime.Adult(subject))
            {
                RHAH_LongFollowRuntime.Say(r, "RHAH_Long_RememberedMeals"); RHAH_LongFollowRuntime.Complete(r); return true;
            }
            if (r.Phase == 5 && tick >= r.DueTick)
            {
                if (!r.FollowupSelected || !RHAH_LongFollowRuntime.Alive(subject)) { RHAH_LongFollowRuntime.Complete(r); return true; }
                if (subject.MapHeld == map) return false;
                r.Phase = 7; RHAH_LongFollowRuntime.OpenLetter(r, map); return true;
            }
            if (r.Phase == 8 && tick >= r.DueTick)
            {
                if (!RHAH_LongFollowRuntime.Alive(subject)) { RHAH_LongFollowRuntime.Complete(r); return true; }
                if (!RHAH_LongFollowRuntime.Bring(subject, map)) return false;
                if (subject.Faction != Faction.OfPlayer) subject.SetFaction(Faction.OfPlayer);
                CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(subject);
                if (comp != null) comp.State.lifecycle = RHAH_Lifecycle.Released;
                HungerAndHavoc.Pawn.RHAH_VisitorGroup.NotifyReleased(subject);
                RHAH_LongFollowRuntime.Unhold(subject);
                HediffDef malnutrition = HediffDefOf.Malnutrition;
                if (subject.health?.hediffSet.GetFirstHediffOfDef(malnutrition) == null) subject.health.AddHediff(malnutrition);
                if (subject.needs?.food != null) subject.needs.food.CurLevelPercentage = 0.1f;
                if (RHAH_LongFollowRuntime.Adult(subject)) RHAH_LongFollowRuntime.Mood(subject, "RHAH_LongTradedEverywhere", -6);
                RHAH_LongFollowRuntime.Say(r, "RHAH_Long_ReturnedThin"); RHAH_LongFollowRuntime.Complete(r); return true;
            }
            return false;
        }

        static bool TickAttachment(RHAH_LongFollowRecord r, Map map, int tick)
        {
            Verse.Pawn child = RHAH_LongFollowRuntime.Pawn(r.SubjectId, tick);
            Verse.Pawn original = RHAH_LongFollowRuntime.Pawn(r.SourceAdultId == 0 ? r.ResponsibleId : r.SourceAdultId, tick);
            if (r.SourceAdultId == 0 && original != null) r.SourceAdultId = original.thingIDNumber;
            if (child != null && child.Dead)
            {
                if (!RHAH_LongFollowRuntime.Adult(child)) RHAH_LongFollowRuntime.Mood(original, "RHAH_LongDoorChildGone", -3);
                RHAH_LongFollowRuntime.Complete(r); return true;
            }
            if (!RHAH_LongFollowRuntime.Alive(child) || !RHAH_LongFollowRuntime.Alive(original)) { RHAH_LongFollowRuntime.Complete(r); return true; }
            if (r.Phase == 0)
            {
                if (!RHAH_LongFollowRuntime.AtHome(child, map) || child.DevelopmentalStage == DevelopmentalStage.Baby) return false;
                r.Phase = 1; r.Opened = true; r.DueTick = tick;
                RHAH_LongFollowRuntime.OpenLetter(r, map);
            }
            if (r.Phase == 1 || r.Phase == 2)
            {
                if (!RHAH_LongFollowRuntime.AtHome(child, map) || child.IsPrisoner || child.IsSlave) { RHAH_LongFollowRuntime.Complete(r); return true; }
                if (RHAH_LongFollowRuntime.Adult(child))
                {
                    RHAH_LongFollowRuntime.Mood(child, r.RehomeCount >= 2 ? "RHAH_LongPushedAway" : "RHAH_LongMealsWereReal", r.RehomeCount >= 2 ? -6 : 4, r.RehomeCount >= 2);
                    if (r.RehomeCount < 2) RHAH_LongFollowRuntime.Mood(original, "RHAH_LongFollowedForYears", -2);
                    RHAH_LongFollowRuntime.Complete(r); return true;
                }
                if (r.Phase == 2 && tick >= r.DueTick)
                {
                    if (r.RehomeCount == 1) { r.ResponsibleId = r.SourceAdultId; r.Phase = 1; RHAH_LongFollowRuntime.Say(r, "RHAH_Long_CameBackNextDay"); RHAH_LongFollowRuntime.OpenLetter(r, map); }
                    else r.Phase = 1;
                }
                Verse.Pawn caregiver = r.RehomeCount >= 2 || r.Phase == 2 ? RHAH_LongFollowRuntime.Pawn(r.ResponsibleId, tick) : original;
                Follow(child, caregiver, map, tick);
                if (child.needs?.food?.CurLevelPercentage >= 0.9f) RHAH_LongFollowRuntime.RemoveMood(child, "RHAH_LongBiteForFood");
                if (!r.LetterOpen && tick >= r.DueTick) RHAH_LongFollowRuntime.OpenLetter(r, map);
                return true;
            }
            if (r.Phase == 3 && tick >= r.DueTick)
            {
                if (child.MapHeld == map) { RHAH_LongFollowRuntime.Leave(child); return false; }
                if (!r.FollowupSelected) { RHAH_LongFollowRuntime.Complete(r); return true; }
                if (!RHAH_LongFollowRuntime.CanBring(child, map)) return false;
                r.VisitorIds.Clear();
                if (!RHAH_LongFollowRuntime.SpawnVisitors(r, map, 2) || !RHAH_LongFollowRuntime.Bring(child, map)) return false;
                r.VisitorIds.Add(child.thingIDNumber);
                RHAH_LongFollowRuntime.Say(r, "RHAH_Long_CalledAgain");
                RHAH_LongFollowRuntime.Unhold(child);
                HungerAndHavoc.Pawn.RHAH_VisitorGroup.TryStart(Pawns(r, tick), map, child.Position, RHAH_PawnRole.Beggar);
                r.Phase = 4; r.DueTick = RHAH_LongFollowRuntime.AddTicks(tick, GenDate.TicksPerDay); return true;
            }
            if (r.Phase == 4 && tick >= r.DueTick)
            {
                for (int i = 0; i < r.VisitorIds.Count; i++) RHAH_LongFollowRuntime.Leave(RHAH_LongFollowRuntime.Pawn(r.VisitorIds[i], tick));
                RHAH_LongFollowRuntime.Complete(r); return true;
            }
            return false;
        }

        static List<Verse.Pawn> Pawns(RHAH_LongFollowRecord r, int tick)
        {
            List<Verse.Pawn> result = new List<Verse.Pawn>();
            for (int i = 0; i < r.VisitorIds.Count; i++) { Verse.Pawn p = RHAH_LongFollowRuntime.Pawn(r.VisitorIds[i], tick); if (RHAH_LongFollowRuntime.Alive(p)) result.Add(p); }
            return result;
        }

        static void Follow(Verse.Pawn child, Verse.Pawn adult, Map map, int tick)
        {
            if (!RHAH_LongFollowRuntime.Alive(child) || !RHAH_LongFollowRuntime.Alive(adult) || !child.Spawned || adult.MapHeld != map || child.Drafted || child.IsPrisoner || child.IsSlave || RHAH_LongFollowRuntime.MedicallyPaused(child)) return;
            if (child.CurJobDef == JobDefOf.Ingest || child.CurJobDef == JobDefOf.Steal || child.CurJobDef == JobDefOf.TakeFromOtherInventory) return;
            if (child.needs?.food?.CurLevelPercentage < 0.5f && adult.inventory != null)
            {
                ThingOwner<Thing> inventory = adult.inventory.innerContainer;
                for (int i = 0; i < inventory.Count; i++)
                {
                    Thing food = inventory[i];
                    if (!food.def.IsNutritionGivingIngestible || !FoodUtility.WillEat(child, food)) continue;
                    Job take = JobMaker.MakeJob(JobDefOf.TakeFromOtherInventory, food, adult); take.count = 1;
                    child.jobs.StartJob(take, JobCondition.InterruptForced); return;
                }
            }
            if (adult.InBed() && GenLocalDate.HourInteger(map) >= 20)
            {
                Building_Bed bed = adult.CurrentBed();
                IntVec3 spot = bed.Position;
                if (CellFinder.TryFindRandomCellNear(bed.Position, map, 3, c => c.Standable(map) && child.CanReach(c, PathEndMode.OnCell, Danger.None), out IntVec3 near)) spot = near;
                child.jobs.StartJob(JobMaker.MakeJob(JobDefOf.LayDown, spot), JobCondition.InterruptForced); return;
            }
            if (adult.Position.DistanceToSquared(child.Position) > 16 && child.CanReach(adult, PathEndMode.Touch, Danger.Some)) child.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto, adult), JobCondition.InterruptForced);
        }
    }
}
