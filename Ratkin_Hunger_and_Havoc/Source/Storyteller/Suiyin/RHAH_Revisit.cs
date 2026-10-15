using System.Collections.Generic;
using HungerAndHavoc.Incidents;
using RimWorld;
using RimWorld.Planet;
using Verse;
using HungerAndHavoc.Narrative;

namespace HungerAndHavoc.Storyteller.Suiyin
{
    internal static class RHAH_Revisit
    {
        internal const int CheckInterval = 2500;
        internal const string LetterDefName = "RHAH_RevisitLetter";
        internal const string GloomDefName = "RHAH_Trait_FamineGloom";

        internal static void Tick(int tick)
        {
            if (!RHAH_NarrativePace.Due(tick, CheckInterval, RHAH_NarrativePace.Spread * 3) || Current.Game == null)
            {
                return;
            }

            SuiyinBook book = Current.Game.GetComponent<NarrativeState>()?.Book;
            if (book?.N004 == null)
            {
                return;
            }

            List<RimWorld.Planet.Caravan> caravans = PlayerCaravans();
            for (int i = 0; i < book.N004.Count; i++)
            {
                TryMeet(book.N004[i], caravans, tick);
            }
        }

        internal static bool TryBringHome(Verse.Pawn pawn, Map map)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.Spawned || map == null)
            {
                return false;
            }

            if (!CellFinder.TryFindRandomEdgeCellWith(cell => cell.Standable(map) && !cell.Fogged(map), map, CellFinder.EdgeRoadChance_Neutral, out IntVec3 edge))
            {
                return false;
            }

            if (pawn.holdingOwner != null)
            {
                pawn.holdingOwner.Remove(pawn);
            }

            GenSpawn.Spawn(pawn, edge, map);
            return pawn.Spawned;
        }

        internal static int CostFor(SuiyinN004Case record)
        {
            return record == null ? 0 : Cost();
        }

        internal static bool TryMeeting(SuiyinN004Case record, int pawnId, PlanetTile tile, int mapId, int targetCaravanId, out Verse.Pawn pawn, out RimWorld.Planet.Caravan caravan)
        {
            pawn = null;
            caravan = null;
            if (record == null || !tile.Valid || !IsTarget(record, pawnId))
            {
                return false;
            }

            Verse.Pawn target = FindPawn(pawnId);
            RimWorld.Planet.Caravan meeting = MeetingCaravan(record);
            RimWorld.Planet.Caravan targetCaravan = target == null ? null : target.GetCaravan();
            if (!Alive(target) || meeting == null || meeting.Tile != tile || target.Tile != tile
                || (target.MapHeld == null ? 0 : target.MapHeld.uniqueID) != mapId
                || (targetCaravan == null ? 0 : targetCaravan.ID) != targetCaravanId)
            {
                return false;
            }

            pawn = target;
            caravan = meeting;
            return true;
        }

        internal static bool CanPay(RimWorld.Planet.Caravan caravan)
        {
            return caravan != null && CountSilver(caravan) >= Cost();
        }

        internal static bool Spend(RimWorld.Planet.Caravan caravan)
        {
            int cost = Cost();
            if (caravan == null || CountSilver(caravan) < cost)
            {
                return false;
            }

            if (cost <= 0)
            {
                return true;
            }

            int left = cost;
            List<KeyValuePair<ThingOwner, Thing>> taken = new List<KeyValuePair<ThingOwner, Thing>>();
            List<Verse.Pawn> pawns = caravan.PawnsListForReading;
            for (int i = 0; i < pawns.Count && left > 0; i++)
            {
                ThingOwner inventory = pawns[i].inventory.innerContainer;
                for (int j = inventory.Count - 1; j >= 0 && left > 0; j--)
                {
                    Thing silver = inventory[j];
                    if (silver == null || silver.Destroyed || silver.def != ThingDefOf.Silver)
                    {
                        continue;
                    }

                    int amount = silver.stackCount < left ? silver.stackCount : left;
                    Thing payment = inventory.Take(silver, amount);
                    if (payment == null || payment.Destroyed)
                    {
                        continue;
                    }

                    taken.Add(new KeyValuePair<ThingOwner, Thing>(inventory, payment));
                    left -= payment.stackCount;
                }
            }

            if (left != 0)
            {
                for (int i = 0; i < taken.Count; i++)
                {
                    taken[i].Key.TryAdd(taken[i].Value);
                }

                return false;
            }

            for (int i = 0; i < taken.Count; i++)
            {
                taken[i].Value.Destroy();
            }

            return true;
        }

        internal static bool Kill(Verse.Pawn pawn)
        {
            if (!Alive(pawn))
            {
                return false;
            }

            pawn.Kill(null);
            return pawn.Dead;
        }

        internal static SuiyinN004Case FindCase(int caseId)
        {
            SuiyinBook book = Current.Game?.GetComponent<NarrativeState>()?.Book;
            if (book?.N004 == null || caseId <= 0)
            {
                return null;
            }

            for (int i = 0; i < book.N004.Count; i++)
            {
                SuiyinN004Case record = book.N004[i];
                if (record != null && record.Id == caseId)
                {
                    return record;
                }
            }

            return null;
        }
        internal static bool BrokeToday(Verse.Pawn pawn, int tick, int until)
        {
            if (pawn == null || until < 0 || tick > until || pawn.MentalStateDef == null)
            {
                return false;
            }

            return pawn.MentalStateDef.defName != "Jailbreaker";
        }

        internal static bool TryGloom(Verse.Pawn pawn)
        {
            if (pawn?.story?.traits == null)
            {
                return false;
            }

            TraitDef def = DefDatabase<TraitDef>.GetNamedSilentFail(GloomDefName);
            if (def == null || HasTrait(pawn, def))
            {
                return false;
            }

            int degree = 0;
            if (def.degreeDatas != null && def.degreeDatas.Count > 0 && def.degreeDatas[0] != null)
            {
                degree = def.degreeDatas[0].degree;
            }

            pawn.story.traits.GainTrait(new Trait(def, degree, false), false);
            return true;
        }

        static void TryMeet(SuiyinN004Case record, List<RimWorld.Planet.Caravan> caravans, int tick)
        {
            if (record == null || record.Outcome != SuiyinN004Outcome.Banished || record.RevisitSeen || record.Revisit != SuiyinN004Revisit.None)
            {
                return;
            }

            if (record.RevisitDeadline >= 0 && tick > record.RevisitDeadline)
            {
                return;
            }

            Verse.Pawn pawn = Survivor(record);
            if (pawn == null || !pawn.Tile.Valid)
            {
                return;
            }

            for (int i = 0; i < caravans.Count; i++)
            {
                RimWorld.Planet.Caravan caravan = caravans[i];
                if (caravan == null || caravan.Destroyed || !caravan.Spawned || caravan.Tile != pawn.Tile)
                {
                    continue;
                }

                record.RevisitSeen = true;
                record.MeetingCaravanId = caravan.ID;
                OpenLetter(record, pawn);
                return;
            }
        }

        static void OpenLetter(SuiyinN004Case record, Verse.Pawn pawn)
        {
            if (Find.LetterStack == null)
            {
                record.RevisitSeen = false;
                record.MeetingCaravanId = 0;
                return;
            }

            LetterDef def = DefDatabase<LetterDef>.GetNamedSilentFail(LetterDefName);
            if (def == null)
            {
                record.RevisitSeen = false;
                record.MeetingCaravanId = 0;
                return;
            }

            ChoiceLetter_RHAH_Revisit letter = (ChoiceLetter_RHAH_Revisit)LetterMaker.MakeLetter(def);
            letter.caseId = record.Id;
            letter.pawnId = pawn.thingIDNumber;
            letter.meetingTile = pawn.Tile;
            letter.meetingMapId = pawn.MapHeld == null ? 0 : pawn.MapHeld.uniqueID;
            letter.targetCaravanId = pawn.GetCaravan() == null ? 0 : pawn.GetCaravan().ID;
            letter.Label = "RHAH_Suiyin_N004Revisit_Label".Translate();
            SuiyinConfig config = Current.Game.GetComponent<NarrativeState>().Book.Config;
            letter.Text = "RHAH_Suiyin_N004Revisit_Text".Translate(config.RescueCost, config.RevisitYears, config.RescueReward);
            letter.lookTargets = new LookTargets(pawn);
            Find.LetterStack.ReceiveLetter(letter);
        }

        static Verse.Pawn Survivor(SuiyinN004Case record)
        {
            Verse.Pawn mother = FindPawn(record.MotherId);
            if (Alive(mother))
            {
                return mother;
            }

            if (record.Children == null)
            {
                return null;
            }

            for (int i = 0; i < record.Children.Count; i++)
            {
                Verse.Pawn child = FindPawn(record.Children[i] == null ? 0 : record.Children[i].LoadId);
                if (Alive(child))
                {
                    return child;
                }
            }

            return null;
        }

        static bool IsTarget(SuiyinN004Case record, int pawnId)
        {
            if (pawnId <= 0)
            {
                return false;
            }

            if (record.MotherId == pawnId)
            {
                return true;
            }

            if (record.Children != null)
            {
                for (int i = 0; i < record.Children.Count; i++)
                {
                    if (record.Children[i] != null && record.Children[i].LoadId == pawnId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        static bool Alive(Verse.Pawn pawn)
        {
            return pawn != null && !pawn.Dead && !pawn.Destroyed;
        }

        static RimWorld.Planet.Caravan MeetingCaravan(SuiyinN004Case record)
        {
            if (record == null || record.MeetingCaravanId <= 0 || Find.WorldObjects == null)
            {
                return null;
            }

            List<RimWorld.Planet.Caravan> caravans = Find.WorldObjects.Caravans;
            for (int i = 0; i < caravans.Count; i++)
            {
                RimWorld.Planet.Caravan caravan = caravans[i];
                if (caravan != null && caravan.ID == record.MeetingCaravanId && caravan.IsPlayerControlled
                    && !caravan.Destroyed && caravan.Spawned && caravan.PawnsListForReading.Count > 0)
                {
                    return caravan;
                }
            }

            return null;
        }

        static List<RimWorld.Planet.Caravan> PlayerCaravans()
        {
            List<RimWorld.Planet.Caravan> found = new List<RimWorld.Planet.Caravan>();
            if (Find.WorldObjects == null)
            {
                return found;
            }

            List<RimWorld.Planet.Caravan> caravans = Find.WorldObjects.Caravans;
            for (int i = 0; i < caravans.Count; i++)
            {
                RimWorld.Planet.Caravan caravan = caravans[i];
                if (caravan != null && caravan.IsPlayerControlled && caravan.PawnsListForReading.Count > 0)
                {
                    found.Add(caravan);
                }
            }

            return found;
        }

        static int CountSilver(RimWorld.Planet.Caravan caravan)
        {
            if (caravan == null || ThingDefOf.Silver == null)
            {
                return 0;
            }

            int count = 0;
            List<Thing> items = CaravanInventoryUtility.AllInventoryItems(caravan);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && !items[i].Destroyed && items[i].def == ThingDefOf.Silver
                    && items[i].holdingOwner != null)
                {
                    count += items[i].stackCount;
                }
            }

            return count;
        }

        static int Cost()
        {
            SuiyinBook book = Current.Game?.GetComponent<NarrativeState>()?.Book;
            if (Core.RHAH_Mod.Settings != null)
            {
                return Core.RHAH_Mod.Settings.narrativeRescueCost;
            }

            return book == null ? 250 : book.Config.RescueCost;
        }

        static bool HasTrait(Verse.Pawn pawn, TraitDef def)
        {
            List<Trait> traits = pawn.story.traits.allTraits;
            if (traits == null)
            {
                return false;
            }

            for (int i = 0; i < traits.Count; i++)
            {
                if (traits[i] != null && traits[i].def == def)
                {
                    return true;
                }
            }

            return false;
        }

        static Verse.Pawn FindPawn(int loadId)
        {
            int tick = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            return RHAH_PawnIndex.Find(loadId, tick);
        }
    }
}
