using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Linq;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Identity;
using HungerAndHavoc.Pawn;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public sealed class RHAH_CampResidentsTests : IDisposable
    {
        readonly RHAH_AttackChoiceTests fixture = new RHAH_AttackChoiceTests();
        readonly HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("rhah.camp.resident.tests");
        readonly object previousPrefs = Field(typeof(Prefs), "data").GetValue(null);
        readonly DutyDef previousDefend = DutyDefOf.Defend;
        readonly DutyDef previousAssault = DutyDefOf.AssaultColony;
        readonly Map map = Find.Maps[0];
        readonly Faction neutral;
        int nextId = 7000;

        public RHAH_CampResidentsTests()
        {
            Rand.PushState(731);
            Current.Game.uniqueIDsManager = new UniqueIDsManager();
            Current.Game.signalManager = new SignalManager();
            Field(typeof(Prefs), "data").SetValue(null, new PrefsData { adaptiveTrainingEnabled = false, devMode = false });
            DutyDefOf.Defend = new DutyDef { defName = "Defend" };
            DutyDefOf.AssaultColony = new DutyDef { defName = "AssaultColony" };
            map.info = new MapInfo { Size = new IntVec3(20, 1, 20) };
            map.lordManager = new LordManager(map);
            map.pawnDestinationReservationManager = Uninitialized<PawnDestinationReservationManager>();
            map.reservationManager = new ReservationManager(map);
            map.enrouteManager = new EnrouteManager(map);
            map.physicalInteractionReservationManager = Uninitialized<PhysicalInteractionReservationManager>();
            map.attackTargetReservationManager = new AttackTargetReservationManager(map);
            Field(typeof(PawnDestinationReservationManager), "reservedDestinations").SetValue(
                map.pawnDestinationReservationManager, new Dictionary<Faction, PawnDestinationReservationManager.PawnDestinationSet>());
            Field(typeof(PhysicalInteractionReservationManager), "reservations").SetValue(
                map.physicalInteractionReservationManager, new List<PhysicalInteractionReservationManager.PhysicalInteractionReservation>());
            harmony.Patch(typeof(Pawn_JobTracker).GetMethod("TryFindAndStartJob", BindingFlags.Instance | BindingFlags.NonPublic),
                prefix: new HarmonyLib.HarmonyMethod(typeof(RHAH_CampResidentsTests), nameof(SkipJobSearch)));
            neutral = new Faction { def = new FactionDef { hidden = true, autoFlee = true }, loadID = 72 };
            Find.FactionManager.AllFactionsListForReading.Add(neutral);
            RHAH_DefOf.RHAH_Faction_Neutral = neutral.def;
        }

        public void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            DutyDefOf.Defend = previousDefend;
            DutyDefOf.AssaultColony = previousAssault;
            Field(typeof(Prefs), "data").SetValue(null, previousPrefs);
            fixture.Dispose();
            Rand.PopState();
        }

        [Fact]
        public void ReconcileTransfersStoredResidentsFromVisitorLordsAndIsIdempotent()
        {
            Verse.Pawn first = Resident();
            Verse.Pawn second = Resident();
            Lord oldFirst = StaleVisitor(first);
            Lord oldSecond = StaleVisitor(second);
            CompRHAH_Pawn.TryGet(first).State.lifecycle = RHAH_Lifecycle.Leaving;
            CompRHAH_Pawn.TryGet(first).State.leaveAfterGameTick = 1;
            List<Verse.Pawn> residents = new List<Verse.Pawn> { first, second };
            first.jobs.jobQueue.EnqueueLast(new Job { def = new JobDef { defName = "Goto" }, exitMapOnArrival = true });

            RHAH_CampResidents.Reconcile(map, residents);

            Lord camp = first.GetLord();
            Assert.IsType<LordJob_RHAH_Camp>(camp.LordJob);
            Assert.Same(camp, second.GetLord());
            Assert.Equal(2, camp.ownedPawns.Count);
            Assert.DoesNotContain(oldFirst, map.lordManager.lords);
            Assert.DoesNotContain(oldSecond, map.lordManager.lords);
            Assert.Single(map.lordManager.lords);
            Assert.IsType<LordToil_DefendPoint>(camp.CurLordToil);
            Assert.Single(camp.Graph.lordToils);
            Assert.False(camp.LordJob.AddFleeToil);
            Assert.Equal(RHAH_Lifecycle.SeekingFood, RHAH_Api.Get(first).Lifecycle);
            Assert.Equal(-1, CompRHAH_Pawn.TryGet(first).State.leaveAfterGameTick);
            Assert.Equal(0, first.jobs.jobQueue.Count);
            Assert.False(RHAH_BatchAttitude.CanOrderLeave(first));
            Assert.Null(JobGiver_RHAH_Leave.TryCreate(first));
            Assert.False(new JobGiver_RHAH_Visitor().TryIssueJobPackage(first, default).IsValid);
            Assert.Same(DutyDefOf.Defend, first.mindState.duty.def);

            RHAH_CampResidents.Reconcile(map, residents);

            Assert.Same(camp, first.GetLord());
            Assert.Single(map.lordManager.lords);
        }

        [Theory]
        [InlineData("player")]
        [InlineData("prisoner")]
        [InlineData("slave")]
        [InlineData("released")]
        [InlineData("departed")]
        [InlineData("dead")]
        public void ReconciliationDoesNotReclaimIneligibleResidents(string status)
        {
            Verse.Pawn resident = Resident();
            Verse.Pawn excluded = Resident();
            if (status == "player") excluded.SetFactionDirect(Faction.OfPlayer);
            else if (status == "released") CompRHAH_Pawn.TryGet(excluded).State.lifecycle = RHAH_Lifecycle.Released;
            else if (status == "departed") Field(typeof(Thing), "mapIndexOrState").SetValue(excluded, (sbyte)-1);
            else if (status == "dead") excluded.health.SetDead();
            else
            {
                excluded.guest = new Pawn_GuestTracker(excluded);
                excluded.guest.guestStatusInt = status == "prisoner" ? GuestStatus.Prisoner : GuestStatus.Slave;
            }

            RHAH_CampResidents.Reconcile(map, new List<Verse.Pawn> { resident, excluded });

            Assert.Single(resident.GetLord().ownedPawns);
            Assert.Null(excluded.GetLord());
            Assert.Null(excluded.mindState.duty);
        }

        [Fact]
        public void DowningRecoveryAndCasualtyKeepSurvivorsInCampWithoutVisitorFallback()
        {
            Verse.Pawn first = Resident();
            Verse.Pawn second = Resident();
            Lord camp = RHAH_CampResidents.Assign(map, new List<Verse.Pawn> { first, second }, map.Center);
            Field(typeof(Pawn_HealthTracker), "healthState").SetValue(first.health, PawnHealthState.Down);

            camp.Notify_PawnLost(first, PawnLostCondition.Incapped);
            camp.Notify_PawnDowned(first);

            Assert.Same(camp, first.GetLord());
            Field(typeof(Pawn_HealthTracker), "healthState").SetValue(first.health, PawnHealthState.Mobile);
            camp.Notify_PawnUndowned(first);
            Assert.Same(DutyDefOf.Defend, first.mindState.duty.def);
            first.health.SetDead();
            camp.Notify_PawnLost(first, PawnLostCondition.Killed);

            Assert.Null(first.GetLord());
            Assert.Same(camp, second.GetLord());
            Assert.IsType<LordToil_DefendPoint>(camp.CurLordToil);
            Assert.Single(camp.Graph.lordToils);
            Assert.Null(JobGiver_RHAH_Leave.TryCreate(second));
        }

        [Theory]
        [InlineData(PawnLostCondition.ChangedFaction)]
        [InlineData(PawnLostCondition.MadePrisoner)]
        [InlineData(PawnLostCondition.MadeSlave)]
        public void OrdinaryFactionAndCustodyChangesStillRemoveCampOwnership(PawnLostCondition reason)
        {
            Verse.Pawn first = Resident();
            Verse.Pawn second = Resident();
            Lord camp = RHAH_CampResidents.Assign(map, new List<Verse.Pawn> { first, second }, map.Center);

            camp.Notify_PawnLost(first, reason);

            Assert.Null(first.GetLord());
            Assert.Same(camp, second.GetLord());
        }

        [Theory]
        [InlineData(0f, true, true, true)]
        [InlineData(1f, false, true, true)]
        [InlineData(1f, true, false, true)]
        [InlineData(1f, true, true, false)]
        public void OnlyActualPlayerExternalHealthDamageProvokesCamp(float damage, bool player, bool health, bool violence)
        {
            Verse.Pawn resident = Resident();
            Lord camp = RHAH_CampResidents.Assign(map, new List<Verse.Pawn> { resident }, map.Center);
            RHAH_AttitudeHarmPatch.Prefix(resident, out RHAH_AttitudeHarmPatch.HarmContext context);
            Thing instigator = new Thing { def = new ThingDef { category = ThingCategory.Building } };
            instigator.SetFactionDirect(player ? Faction.OfPlayer : neutral);
            DamageDef def = new DamageDef { harmsHealth = health };
            Field(typeof(DamageDef), "externalViolence").SetValue(def, violence);

            RHAH_AttitudeHarmPatch.Postfix(resident, new DamageInfo(def, 1f, angle: 0f, instigator: instigator),
                new DamageWorker.DamageResult { totalDamageDealt = damage }, context);

            Assert.Same(camp, resident.GetLord());
            Assert.Same(neutral, resident.Faction);
            Assert.Equal(RHAH_Attitude.Neutral, RHAH_Api.Get(resident).Attitude);
        }

        [Fact]
        public void LethalFirstHitUsesCapturedCampAndReestablishesSharedAssaultDuties()
        {
            Verse.Pawn victim = Resident();
            Verse.Pawn survivor = Resident();
            Lord camp = RHAH_CampResidents.Assign(map, new List<Verse.Pawn> { victim, survivor }, map.Center);
            // 原版 SetFaction 的完整引擎路径另由实机覆盖 此处隔离致死命中后丢失 Lord 的边界
            Faction hostile = RHAH_AttitudeFactions.Resolve(RHAH_Attitude.Hostile);
            survivor.SetFactionDirect(hostile);
            RHAH_AttitudeHarmPatch.Prefix(victim, out RHAH_AttitudeHarmPatch.HarmContext context);
            victim.health.SetDead();
            camp.Notify_PawnLost(victim, PawnLostCondition.Killed);
            Thing instigator = new Thing { def = new ThingDef { category = ThingCategory.Building } };
            instigator.SetFactionDirect(Faction.OfPlayer);
            DamageDef def = new DamageDef { harmsHealth = true };
            Field(typeof(DamageDef), "externalViolence").SetValue(def, true);

            RHAH_AttitudeHarmPatch.Postfix(victim, new DamageInfo(def, 100f, angle: 0f, instigator: instigator),
                new DamageWorker.DamageResult { totalDamageDealt = 100f }, context);

            Assert.True(victim.Dead);
            Assert.Null(victim.GetLord());
            Lord assault = survivor.GetLord();
            Assert.NotSame(camp, assault);
            Assert.IsType<LordJob_RHAH_Camp>(assault.LordJob);
            Assert.IsType<LordToil_AssaultColony>(assault.CurLordToil);
            Assert.Same(DutyDefOf.AssaultColony, survivor.mindState.duty.def);
            Assert.Same(hostile, assault.faction);
            Assert.Equal(RHAH_Attitude.Hostile, RHAH_Api.Get(survivor).Attitude);
            Assert.Single(map.lordManager.lords);
            Assert.False(RHAH_BatchAttitude.CanOrderLeave(survivor));
        }

        [Fact]
        public void UnloadRemovesTheWholeCampLordAndItsReferences()
        {
            RHAH_SaveCleanupPlan plan = new RHAH_SaveCleanupPlan();
            plan.OwnedClasses.Add(typeof(LordJob_RHAH_Camp).FullName);
            XDocument document = XDocument.Parse(@"<savegame><game><maps><li>
<lordManager><lords><li><loadID>3</loadID><ownedPawns><li>Thing_1</li></ownedPawns>
<lordJob Class=""HungerAndHavoc.Pawn.LordJob_RHAH_Camp""><point>(1, 0, 1)</point></lordJob></li></lords></lordManager>
<things><li><def>Human</def><id>1</id><lord>Lord_3</lord></li></things>
</li></maps></game></savegame>");

            new RHAH_SaveCleanup(plan).Clean(document);

            Assert.Empty(document.Descendants("lords").Single().Elements());
            Assert.DoesNotContain(document.Descendants(), node => node.Value == "Lord_3");
            Assert.Equal("1", document.Descendants("id").Single().Value);
        }

        Verse.Pawn Resident()
        {
            Verse.Pawn pawn = (Verse.Pawn)typeof(RHAH_AttackChoiceTests)
                .GetMethod("Visitor", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(fixture, new object[] { nextId++ });
            pawn.SetFactionDirect(neutral);
            pawn.kindDef = new PawnKindDef { defendPointRadius = -1f };
            pawn.jobs = new Pawn_JobTracker(pawn);
            CompRHAH_Pawn.TryGet(pawn).ApplySeed(new RHAH_PawnSeed("I-051", 7, 7,
                RHAH_PawnRole.Refugee, RHAH_Lifecycle.Arriving));
            return pawn;
        }

        Lord StaleVisitor(Verse.Pawn pawn)
        {
            Lord lord = new Lord { faction = neutral };
            Field(typeof(Lord), "curJob").SetValue(lord, new LordJob_RHAH_Visitor());
            map.lordManager.AddLord(lord);
            lord.ownedPawns.Add(pawn);
            pawn.lord = lord;
            return lord;
        }

        static bool SkipJobSearch()
        {
            return false;
        }

        static T Uninitialized<T>() where T : class
        {
            return (T)FormatterServices.GetUninitializedObject(typeof(T));
        }

        static FieldInfo Field(Type type, string name)
        {
            return type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(type.FullName + "." + name);
        }
    }
}
