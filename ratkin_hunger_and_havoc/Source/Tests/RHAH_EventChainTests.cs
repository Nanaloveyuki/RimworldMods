using System;
using System.Collections.Generic;
using HungerAndHavoc.EventMgr;
using Oxygendsl.Core.Events;
using Verse;
using Xunit;

namespace HungerAndHavoc.Tests
{
    public class RHAH_EventChainTests : IDisposable
    {
        readonly Game previousGame;

        static RHAH_EventChainTests()
        {
            RimWorldAssemblies.EnsureResolved();
        }

        public RHAH_EventChainTests()
        {
            previousGame = Current.Game;
            Current.Game = null;
        }

        public void Dispose()
        {
            Current.Game = previousGame;
        }

        [Theory]
        [InlineData(EventChainMode.Active)]
        [InlineData(EventChainMode.Paused)]
        [InlineData(EventChainMode.Disabled)]
        public void PreparationRetiresKingdomInstancesBeforeActivationWithoutChangingOrdinaryEvents(EventChainMode mode)
        {
            EventChainReceipt receipt = new EventChainReceipt
            {
                Key = "short:3", ActionId = "short", Status = "applied", Data = "saved outcome",
                Reason = "saved reason", Revision = 3, Tick = 75
            };
            EventChainInstance follow = new EventChainInstance
            {
                Id = 7, DefinitionId = "I-002", DefinitionVersion = 1, SiteId = -9, Tile = 41,
                Stage = 2, StartedTick = 10, DeadlineTick = 9000, NextTick = -1, Revision = 3,
                Payload = "I-002|31|-9", PauseReason = "saved pause", End = EventChainEnd.None
            };
            follow.Receipts.Add(receipt);
            EventChainInstance arrival = new EventChainInstance
            {
                Id = 11, DefinitionId = RHAH_EventChainProvider.ArrivalId, SiteId = 0,
                Payload = "0", StartedTick = 0, NextTick = 120
            };
            EventChainProviderState state = new EventChainProviderState
            {
                ProviderId = RHAH_EventChainProvider.ProviderKey, NextId = 91,
                MigrationStatus = "completed", Mode = mode, PausedTick = mode == EventChainMode.Active ? -1 : 50,
                Reason = "saved provider reason",
                Instances = new List<EventChainInstance>
                {
                    new EventChainInstance { Id = 80, DefinitionId = "RHAH_KingdomInvitation", Payload = "obsolete invitation" },
                    follow,
                    new EventChainInstance { Id = 90, DefinitionId = "RHAH_KingdomCollaboration", End = EventChainEnd.Completed },
                    arrival
                }
            };
            EventChainRuntime runtime = new EventChainRuntime(new List<EventChainProviderState> { state });
            runtime.Register(new RHAH_EventChainProvider());

            RHAH_EventChainMigration.PrepareGame(runtime);
            RHAH_EventChainMigration.PrepareGame(runtime);
            runtime.Activate(100);

            Assert.Equal(new[] { follow, arrival }, state.Instances);
            Assert.Null(runtime.Find(RHAH_EventChainProvider.ProviderKey, 80));
            Assert.Null(runtime.Find(RHAH_EventChainProvider.ProviderKey, 90));
            Assert.Same(follow, runtime.Find(RHAH_EventChainProvider.ProviderKey, 7));
            Assert.Same(arrival, runtime.Find(RHAH_EventChainProvider.ProviderKey, 11));
            Assert.Equal(91, state.NextId);
            Assert.Equal(mode, state.Mode);
            Assert.Equal(mode == EventChainMode.Active ? -1 : 50, state.PausedTick);
            Assert.Equal("completed", state.MigrationStatus);
            Assert.Equal("saved provider reason", state.Reason);
            Assert.Equal("I-002", follow.DefinitionId);
            Assert.Equal(1, follow.DefinitionVersion);
            Assert.Equal(-9, follow.SiteId);
            Assert.Equal(41, follow.Tile);
            Assert.Equal(2, follow.Stage);
            Assert.Equal(10, follow.StartedTick);
            Assert.Equal(9000, follow.DeadlineTick);
            Assert.Equal(-1, follow.NextTick);
            Assert.Equal(3, follow.Revision);
            Assert.Equal("I-002|31|-9", follow.Payload);
            Assert.Equal("saved pause", follow.PauseReason);
            Assert.Equal(EventChainEnd.None, follow.End);
            Assert.Same(receipt, Assert.Single(follow.Receipts));
            Assert.Equal("short:3", receipt.Key);
            Assert.Equal("short", receipt.ActionId);
            Assert.Equal("applied", receipt.Status);
            Assert.Equal("saved outcome", receipt.Data);
            Assert.Equal("saved reason", receipt.Reason);
            Assert.Equal(3, receipt.Revision);
            Assert.Equal(75, receipt.Tick);
            Assert.Equal(120, arrival.NextTick);
            Assert.Equal("0", arrival.Payload);
            runtime.Tick(120);
            Assert.Equal(mode == EventChainMode.Active ? EventChainEnd.Completed : EventChainEnd.None, arrival.End);
        }

        [Fact]
        public void RetirementMatchesOnlyExactDefinitionsInTheOwnedPartitionEvenAfterFailedMigration()
        {
            EventChainInstance unrelated = new EventChainInstance { Id = 1, DefinitionId = "RHAH_KingdomInvitation_External" };
            EventChainInstance differentCase = new EventChainInstance { Id = 2, DefinitionId = "rhah_kingdomcollaboration" };
            EventChainInstance foreign = new EventChainInstance { Id = 1, DefinitionId = "RHAH_KingdomInvitation" };
            EventChainProviderState owned = new EventChainProviderState
            {
                ProviderId = RHAH_EventChainProvider.ProviderKey, NextId = 5, Mode = EventChainMode.Paused,
                MigrationStatus = "failed: retained originals",
                Instances = new List<EventChainInstance>
                {
                    unrelated, differentCase, null,
                    new EventChainInstance { Id = 3, DefinitionId = "RHAH_KingdomCollaboration" },
                    new EventChainInstance { Id = 4, DefinitionId = "RHAH_KingdomInvitation" }
                }
            };
            EventChainProviderState other = new EventChainProviderState
            {
                ProviderId = "another.mod", NextId = 2, Instances = new List<EventChainInstance> { foreign }
            };
            EventChainRuntime runtime = new EventChainRuntime(new List<EventChainProviderState> { owned, other });

            RHAH_EventChainMigration.PrepareGame(runtime);

            Assert.Equal(new[] { unrelated, differentCase, null }, owned.Instances);
            Assert.Same(foreign, Assert.Single(other.Instances));
            Assert.Equal(5, owned.NextId);
            Assert.Equal(EventChainMode.Paused, owned.Mode);
            Assert.Equal("failed: retained originals", owned.MigrationStatus);
        }

        [Theory]
        [InlineData(2, 1)]
        [InlineData(1, 2)]
        public void RetirementDoesNotInterpretUnknownProviderFormats(int schemaVersion, int stateVersion)
        {
            EventChainInstance instance = new EventChainInstance { Id = 1, DefinitionId = "RHAH_KingdomInvitation" };
            EventChainProviderState state = new EventChainProviderState
            {
                ProviderId = RHAH_EventChainProvider.ProviderKey, SchemaVersion = schemaVersion, StateVersion = stateVersion,
                NextId = 2, Instances = new List<EventChainInstance> { instance }
            };
            EventChainRuntime runtime = new EventChainRuntime(new List<EventChainProviderState> { state });

            RHAH_EventChainMigration.PrepareGame(runtime);

            Assert.Same(instance, Assert.Single(state.Instances));
            Assert.Equal(schemaVersion, state.SchemaVersion);
            Assert.Equal(stateVersion, state.StateVersion);
            Assert.Equal(2, state.NextId);
        }

        [Fact]
        public void MigrationPreservesSavedFactsAndDoesNotMutateTheLegacyRecords()
        {
            List<RHAH_EventChainRecord> records = new List<RHAH_EventChainRecord>
            {
                Record(41, "I-001", 4, 0, 10, -1),
                Record(7, "I-019", 4, 1, 20, 9000),
                Record(22, "I-002", 4, 2, 30, 8000),
                Record(8, "I-035", -9, 0, 40, 77, true, 0),
                Record(19, "I-012", 4, 1, 50, 78, true, 1),
                Record(3, "I-003", 4, 2, 60, 79, true, 2),
                Record(16, "I-004", 4, 0, 70, 80, true, 3),
                Record(12, "I-005", 4, 2, 80, 81, true, 4)
            };
            List<RHAH_EventChainRecord> originals = new List<RHAH_EventChainRecord>(records);
            List<RHAH_EventChainRecord> saved = Snapshot(records);
            int nextId = 100;

            List<EventChainInstance> imported = RHAH_EventChainMigration.Convert(records, nextId, 5000, out int importedNextId);

            Assert.Equal(100, nextId);
            Assert.Equal(100, importedNextId);
            Assert.Equal(records.Count, imported.Count);
            AssertOriginals(records, originals, saved);
            for (int i = 0; i < records.Count; i++)
            {
                RHAH_EventChainRecord original = records[i];
                EventChainInstance instance = imported[i];
                Assert.Equal(original.id, instance.Id);
                Assert.Equal(original.displayId, instance.DefinitionId);
                Assert.Equal(original.siteId, instance.SiteId);
                Assert.Equal(original.stage, instance.Stage);
                Assert.Equal(original.startedTick, instance.StartedTick);
                Assert.Equal(original.deadlineTick, instance.DeadlineTick);
                Assert.Equal(original.payload, instance.Payload);
                EventChainEnd expected = original.closed
                    ? (original.end == 0 ? EventChainEnd.Completed : (EventChainEnd)original.end)
                    : EventChainEnd.None;
                Assert.Equal(expected, instance.End);
                Assert.Empty(instance.Receipts);
            }

            Assert.Equal(5001, imported[0].NextTick);
            Assert.Equal(5007, imported[1].NextTick);
            Assert.Equal(-1, imported[2].NextTick);
            for (int i = 3; i < imported.Count; i++)
            {
                Assert.Equal(-1, imported[i].NextTick);
            }
        }

        [Fact]
        public void MigrationAdvancesOnlyAnObsoleteNextCounterWithoutRenumberingInstances()
        {
            List<RHAH_EventChainRecord> records = new List<RHAH_EventChainRecord>
            {
                Record(4, "I-002", 4, 2, 10, -1),
                Record(17, "I-001", 4, 0, 11, -1)
            };
            int nextId = 1;

            List<EventChainInstance> imported = RHAH_EventChainMigration.Convert(records, nextId, 20, out int importedNextId);

            Assert.Equal(1, nextId);
            Assert.Equal(18, importedNextId);
            Assert.Equal(4, imported[0].Id);
            Assert.Equal(17, imported[1].Id);
            Assert.Equal(4, records[0].id);
            Assert.Equal(17, records[1].id);
        }

        [Fact]
        public void EmptyLegacyStatePreservesTheNextCounter()
        {
            List<EventChainInstance> imported = RHAH_EventChainMigration.Convert(
                new List<RHAH_EventChainRecord>(), 37, 20, out int importedNextId);

            Assert.Empty(imported);
            Assert.Equal(37, importedNextId);
        }

        [Theory]
        [InlineData("null")]
        [InlineData("zero-id")]
        [InlineData("negative-id")]
        [InlineData("duplicate-id")]
        [InlineData("exhausted-id")]
        [InlineData("unknown-definition")]
        [InlineData("null-definition")]
        [InlineData("negative-stage")]
        [InlineData("unknown-stage")]
        [InlineData("unknown-end")]
        [InlineData("negative-end")]
        [InlineData("open-terminal")]
        [InlineData("null-payload")]
        [InlineData("malformed-payload")]
        [InlineData("wrong-definition-payload")]
        [InlineData("wrong-site-payload")]
        [InlineData("invalid-start")]
        [InlineData("invalid-deadline")]
        [InlineData("invalid-next-id")]
        [InlineData("closed-malformed-payload")]
        public void MalformedMigrationRejectsTheWholeInputWithoutRepairingOrDroppingOriginals(string fault)
        {
            RHAH_EventChainRecord broken = Record(8, "I-002", 4, 0, 10, -1);
            int nextId = 15;
            switch (fault)
            {
                case "null": broken = null; break;
                case "zero-id": broken.id = 0; break;
                case "negative-id": broken.id = -8; break;
                case "duplicate-id": broken.id = 3; break;
                case "exhausted-id": broken.id = int.MaxValue; break;
                case "unknown-definition": broken = Record(8, "I-055", 4, 0, 10, -1); break;
                case "null-definition": broken.displayId = null; break;
                case "negative-stage": broken.stage = -1; break;
                case "unknown-stage": broken.stage = 3; break;
                case "unknown-end": broken.closed = true; broken.end = 5; break;
                case "negative-end": broken.closed = true; broken.end = -1; break;
                case "open-terminal": broken.end = 1; break;
                case "null-payload": broken.payload = null; break;
                case "malformed-payload": broken.payload = "I-002|0|4"; break;
                case "wrong-definition-payload": broken.payload = "I-003|9|4"; break;
                case "wrong-site-payload": broken.payload = "I-002|9|5"; break;
                case "invalid-start": broken.startedTick = -1; break;
                case "invalid-deadline": broken.deadlineTick = -2; break;
                case "invalid-next-id": nextId = 0; break;
                case "closed-malformed-payload": broken.closed = true; broken.end = 1; broken.payload = "broken"; break;
            }

            List<RHAH_EventChainRecord> records = new List<RHAH_EventChainRecord>
            {
                Record(3, "I-001", 4, 0, 5, -1),
                broken
            };
            List<RHAH_EventChainRecord> originals = new List<RHAH_EventChainRecord>(records);
            List<RHAH_EventChainRecord> saved = Snapshot(records);
            int originalNextId = nextId;

            Assert.Throws<FormatException>(() => RHAH_EventChainMigration.Convert(records, nextId, 20, out int unused));

            Assert.Equal(originalNextId, nextId);
            AssertOriginals(records, originals, saved);
        }

        [Theory]
        [InlineData(0, 7, 7)]
        [InlineData(6, 7, 7)]
        [InlineData(7, 7, 2507)]
        [InlineData(2506, 7, 2507)]
        [InlineData(2507, 7, 5007)]
        [InlineData(2499, 2500, 2500)]
        [InlineData(2500, 2500, 5000)]
        [InlineData(5000, 5007, 5007)]
        [InlineData(2147483646, 1147, 2147483647)]
        [InlineData(2147483647, 1147, -1)]
        [InlineData(2147483647, 1, -1)]
        public void LongMigrationKeepsTheOriginalStrictlyFuturePhaseWithoutOverflow(int tick, int id, int expected)
        {
            Assert.Equal(expected, RHAH_EventChainMigration.NextLongTick(tick, id));
            if (expected >= 0)
            {
                Assert.True(expected > tick);
                Assert.Equal(id % 2500, expected % 2500);
                Assert.True((long)expected - tick <= 2500);
            }
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(10000)]
        public void PublishedLongOverrideWithoutFollowupStaysDormantOrExpiresWithoutBlockingTheProvider(int deadline)
        {
            List<RHAH_EventChainRecord> records = new List<RHAH_EventChainRecord>
            {
                Record(7, "I-001", 4, 1, 10, deadline),
                Record(8, "I-002", 4, 2, 10, -1)
            };
            List<EventChainInstance> imported = RHAH_EventChainMigration.Convert(records, 9, 5000, out int nextId);
            EventChainRuntime runtime = new EventChainRuntime(new List<EventChainProviderState>());
            runtime.Register(new RHAH_EventChainProvider());
            Assert.True(runtime.Import(RHAH_EventChainProvider.ProviderKey, imported, nextId, "completed", out string error), error);
            runtime.Activate(5000);
            runtime.Tick(5007);
            runtime.Tick(10000);
            EventChainInstance instance = runtime.Find(RHAH_EventChainProvider.ProviderKey, 7);
            Assert.Equal(1, instance.Stage);
            Assert.Equal(deadline, instance.DeadlineTick);
            Assert.Equal(deadline < 0 ? EventChainEnd.None : EventChainEnd.Expired, instance.End);
            Assert.Equal(-1, instance.NextTick);
            Assert.Empty(instance.Receipts);
            Assert.Equal("", instance.PauseReason);
            Assert.Equal(EventChainEnd.None, runtime.Find(RHAH_EventChainProvider.ProviderKey, 8).End);
            Assert.Equal(1, records[0].stage);
            Assert.Equal(deadline, records[0].deadlineTick);
        }

        [Fact]
        public void MigratedLongScheduleKeepsItsPhaseThroughOxygenActivationAndReload()
        {
            List<RHAH_EventChainRecord> records = new List<RHAH_EventChainRecord>
            {
                Record(7, "I-019", 4, 1, 10, 100000)
            };
            List<EventChainInstance> imported = RHAH_EventChainMigration.Convert(records, 50, 5000, out int nextId);
            EventChainRuntime runtime = new EventChainRuntime(new List<EventChainProviderState>());
            runtime.Register(new RHAH_EventChainProvider());
            Assert.True(runtime.Import(RHAH_EventChainProvider.ProviderKey, imported, nextId, "completed", out string error), error);
            runtime.Activate(5000);
            runtime.Tick(5006);
            Assert.Equal(0L, runtime.CallbackCount);
            runtime.Tick(5007);
            EventChainInstance instance = runtime.Find(RHAH_EventChainProvider.ProviderKey, 7);
            Assert.Equal(7507, instance.NextTick);
            Assert.Empty(instance.Receipts);

            EventChainProviderState saved = CopyState(runtime.GetProviderState(RHAH_EventChainProvider.ProviderKey));
            EventChainRuntime restored = new EventChainRuntime(new List<EventChainProviderState> { saved });
            restored.Register(new RHAH_EventChainProvider());
            restored.Activate(6000);
            restored.Tick(7506);
            Assert.Equal(0L, restored.CallbackCount);
            restored.Tick(7507);
            EventChainInstance loaded = restored.Find(RHAH_EventChainProvider.ProviderKey, 7);

            Assert.Equal(10007, loaded.NextTick);
            Assert.Equal(1, loaded.Stage);
            Assert.Equal(10, loaded.StartedTick);
            Assert.Equal(100000, loaded.DeadlineTick);
            Assert.Equal("I-019|9|4", loaded.Payload);
            Assert.Equal(EventChainEnd.None, loaded.End);
            Assert.Empty(loaded.Receipts);
            Assert.Equal(50, restored.GetProviderState(RHAH_EventChainProvider.ProviderKey).NextId);
        }

        [Fact]
        public void OxygenImportFailureKeepsThePartitionAbsentAndTheConvertedInputUntouched()
        {
            List<RHAH_EventChainRecord> records = new List<RHAH_EventChainRecord>
            {
                Record(3, "I-001", 4, 0, 10, -1),
                Record(8, "I-002", 4, 0, 10, -1)
            };
            List<RHAH_EventChainRecord> originals = new List<RHAH_EventChainRecord>(records);
            List<RHAH_EventChainRecord> saved = Snapshot(records);
            List<EventChainInstance> imported = RHAH_EventChainMigration.Convert(records, 20, 100, out int nextId);
            imported[1].Payload = "I-002|0|4";
            EventChainRuntime runtime = new EventChainRuntime(new List<EventChainProviderState>());
            runtime.Register(new RHAH_EventChainProvider());

            Assert.False(runtime.Import(RHAH_EventChainProvider.ProviderKey, imported, nextId, "completed", out string error));

            Assert.False(string.IsNullOrEmpty(error));
            Assert.False(runtime.HasProviderState(RHAH_EventChainProvider.ProviderKey));
            Assert.Empty(runtime.States);
            Assert.Equal(2, imported.Count);
            Assert.Null(imported[0].PreparedState);
            Assert.Null(imported[1].PreparedState);
            Assert.Equal("I-001|9|4", imported[0].Payload);
            Assert.Equal("I-002|0|4", imported[1].Payload);
            Assert.Equal(3, imported[0].Id);
            Assert.Equal(8, imported[1].Id);
            AssertOriginals(records, originals, saved);
        }

        [Fact]
        public void PersistedEmptyOxygenPartitionIsAuthoritativeAndCannotReimportLegacyRecords()
        {
            EventChainProviderState saved = new EventChainProviderState
            {
                ProviderId = RHAH_EventChainProvider.ProviderKey,
                NextId = 41,
                MigrationStatus = "completed"
            };
            EventChainRuntime runtime = new EventChainRuntime(new List<EventChainProviderState> { saved });
            runtime.Register(new RHAH_EventChainProvider());
            List<EventChainInstance> stale = RHAH_EventChainMigration.Convert(
                new List<RHAH_EventChainRecord> { Record(3, "I-001", 4, 0, 10, -1) }, 4, 100, out int nextId);

            Assert.False(runtime.Import(RHAH_EventChainProvider.ProviderKey, stale, nextId, "completed", out string error));
            Assert.False(string.IsNullOrEmpty(error));
            runtime.Activate(100);

            Assert.Empty(runtime.GetProviderState(RHAH_EventChainProvider.ProviderKey).Instances);
            Assert.Equal(41, runtime.GetProviderState(RHAH_EventChainProvider.ProviderKey).NextId);
            Assert.Equal("completed", runtime.GetProviderState(RHAH_EventChainProvider.ProviderKey).MigrationStatus);
            Assert.Null(runtime.Find(RHAH_EventChainProvider.ProviderKey, 3));
        }

        [Fact]
        public void MigratedDoneStageRemainsDormantAndNeverRerolls()
        {
            List<RHAH_EventChainRecord> records = new List<RHAH_EventChainRecord>
            {
                Record(4, "I-002", 4, 2, 10, -1)
            };
            List<EventChainInstance> imported = RHAH_EventChainMigration.Convert(records, 5, 100, out int nextId);
            EventChainRuntime runtime = new EventChainRuntime(new List<EventChainProviderState>());
            runtime.Register(new RHAH_EventChainProvider());
            Assert.True(runtime.Import(RHAH_EventChainProvider.ProviderKey, imported, nextId, "completed", out string error), error);
            runtime.Activate(100);

            runtime.Tick(101);
            runtime.Tick(2504);
            runtime.Tick(5004);

            EventChainInstance instance = runtime.Find(RHAH_EventChainProvider.ProviderKey, 4);
            Assert.Equal(2, instance.Stage);
            Assert.Equal(-1, instance.NextTick);
            Assert.Equal(EventChainEnd.None, instance.End);
            Assert.Empty(instance.Receipts);
            Assert.Equal(0L, runtime.CallbackCount);
        }

        [Fact]
        public void FirstMapArrivalImportsAndConsumesItsReceiptWithoutReplaying()
        {
            EventChainRuntime runtime = new EventChainRuntime(new List<EventChainProviderState>());
            runtime.Register(new RHAH_EventChainProvider());
            List<EventChainInstance> arrivals = new List<EventChainInstance>
            {
                new EventChainInstance
                {
                    Id = 1,
                    DefinitionId = RHAH_EventChainProvider.ArrivalId,
                    SiteId = 0,
                    Payload = "0",
                    NextTick = 120
                }
            };

            Assert.True(runtime.Import(RHAH_EventChainProvider.ProviderKey, arrivals, 2, "completed", out string error), error);
            runtime.Activate(0);
            runtime.Tick(119);
            Assert.Equal(0L, runtime.CallbackCount);
            runtime.Tick(120);

            EventChainInstance arrived = runtime.Find(RHAH_EventChainProvider.ProviderKey, 1);
            Assert.Equal(EventChainEnd.Completed, arrived.End);
            EventChainReceipt receipt = Assert.Single(arrived.Receipts);
            Assert.Equal("arrive_predator", receipt.ActionId);
            Assert.Equal("applied", receipt.Status);
            Assert.Equal("map_unavailable", receipt.Data);
            long callbacks = runtime.CallbackCount;
            runtime.Tick(121);
            Assert.Equal(callbacks, runtime.CallbackCount);
        }

        [Fact]
        public void OxygenArrivalQueueFiresAtExactly120TicksAndItsReceiptSurvivesRestart()
        {
            ArrivalProbe first = new ArrivalProbe();
            EventChainRuntime runtime = new EventChainRuntime(new List<EventChainProviderState>());
            runtime.Register(first);
            runtime.Activate(10);
            int id = runtime.Start(first.ProviderId, first.Id, 4, 0, 10, -1, "4", 0, 10 + 120);
            Assert.True(id > 0);

            runtime.Tick(129);
            Assert.Equal(0, first.Arrivals);
            EventChainProviderState waiting = CopyState(runtime.GetProviderState(first.ProviderId));
            ArrivalProbe atDue = new ArrivalProbe();
            EventChainRuntime resumed = new EventChainRuntime(new List<EventChainProviderState> { waiting });
            resumed.Register(atDue);
            resumed.Activate(129);
            resumed.Tick(129);
            Assert.Equal(0, atDue.Arrivals);
            resumed.Tick(130);
            Assert.Equal(1, atDue.Arrivals);
            EventChainInstance arrived = resumed.Find(first.ProviderId, id);
            Assert.Equal(EventChainEnd.Completed, arrived.End);
            EventChainReceipt receipt = Assert.Single(arrived.Receipts);
            Assert.Equal("arrive_predator", receipt.ActionId);
            Assert.Equal("applied", receipt.Status);
            Assert.Equal("arrived", receipt.Data);
            Assert.Equal(130, receipt.Tick);

            EventChainProviderState saved = CopyState(resumed.GetProviderState(first.ProviderId));
            ArrivalProbe afterLoad = new ArrivalProbe();
            EventChainRuntime restored = new EventChainRuntime(new List<EventChainProviderState> { saved });
            restored.Register(afterLoad);
            restored.Activate(130);
            restored.Tick(130);
            restored.Tick(131);
            restored.Tick(10000);

            Assert.Equal(0, afterLoad.Arrivals);
            EventChainInstance loaded = restored.Find(first.ProviderId, id);
            Assert.Equal(EventChainEnd.Completed, loaded.End);
            EventChainReceipt loadedReceipt = Assert.Single(loaded.Receipts);
            Assert.Equal(receipt.Key, loadedReceipt.Key);
            Assert.Equal(receipt.Revision, loadedReceipt.Revision);
            Assert.Equal(receipt.Tick, loadedReceipt.Tick);
            Assert.Equal(receipt.ActionId, loadedReceipt.ActionId);
            Assert.Equal(receipt.Status, loadedReceipt.Status);
            Assert.Equal(receipt.Data, loadedReceipt.Data);
            Assert.Equal(receipt.Reason, loadedReceipt.Reason);
            Assert.Equal(saved.NextId, restored.GetProviderState(first.ProviderId).NextId);
        }

        static RHAH_EventChainRecord Record(int id, string displayId, int siteId, int stage, int startedTick, int deadlineTick, bool closed = false, int end = 0)
        {
            return new RHAH_EventChainRecord
            {
                id = id,
                displayId = displayId,
                siteId = siteId,
                stage = stage,
                startedTick = startedTick,
                deadlineTick = deadlineTick,
                payload = RHAH_EventFollowRules.Payload(displayId, 9, siteId),
                closed = closed,
                end = end
            };
        }

        static List<RHAH_EventChainRecord> Snapshot(List<RHAH_EventChainRecord> records)
        {
            List<RHAH_EventChainRecord> saved = new List<RHAH_EventChainRecord>();
            for (int i = 0; i < records.Count; i++)
            {
                RHAH_EventChainRecord record = records[i];
                if (record == null)
                {
                    saved.Add(null);
                    continue;
                }

                RHAH_EventChainRecord copy = Record(record.id, record.displayId, record.siteId, record.stage, record.startedTick, record.deadlineTick, record.closed, record.end);
                copy.payload = record.payload;
                saved.Add(copy);
            }

            return saved;
        }

        static void AssertOriginals(List<RHAH_EventChainRecord> records, List<RHAH_EventChainRecord> originals, List<RHAH_EventChainRecord> saved)
        {
            Assert.Equal(saved.Count, records.Count);
            for (int i = 0; i < records.Count; i++)
            {
                Assert.Same(originals[i], records[i]);
                if (saved[i] == null)
                {
                    Assert.Null(records[i]);
                    continue;
                }

                Assert.Equal(saved[i].id, records[i].id);
                Assert.Equal(saved[i].displayId, records[i].displayId);
                Assert.Equal(saved[i].siteId, records[i].siteId);
                Assert.Equal(saved[i].stage, records[i].stage);
                Assert.Equal(saved[i].startedTick, records[i].startedTick);
                Assert.Equal(saved[i].deadlineTick, records[i].deadlineTick);
                Assert.Equal(saved[i].payload, records[i].payload);
                Assert.Equal(saved[i].closed, records[i].closed);
                Assert.Equal(saved[i].end, records[i].end);
            }
        }

        static EventChainProviderState CopyState(EventChainProviderState source)
        {
            EventChainProviderState copy = new EventChainProviderState
            {
                ProviderId = source.ProviderId,
                SchemaVersion = source.SchemaVersion,
                StateVersion = source.StateVersion,
                NextId = source.NextId,
                PausedTick = source.PausedTick,
                Reason = source.Reason,
                MigrationStatus = source.MigrationStatus,
                Mode = source.Mode
            };
            for (int i = 0; i < source.Instances.Count; i++)
            {
                EventChainInstance instance = source.Instances[i];
                EventChainInstance saved = new EventChainInstance
                {
                    Id = instance.Id,
                    DefinitionId = instance.DefinitionId,
                    DefinitionVersion = instance.DefinitionVersion,
                    SiteId = instance.SiteId,
                    Tile = instance.Tile,
                    Stage = instance.Stage,
                    StartedTick = instance.StartedTick,
                    DeadlineTick = instance.DeadlineTick,
                    NextTick = instance.NextTick,
                    Revision = instance.Revision,
                    Payload = instance.Payload,
                    PauseReason = instance.PauseReason,
                    End = instance.End
                };
                for (int j = 0; j < instance.Receipts.Count; j++)
                {
                    EventChainReceipt receipt = instance.Receipts[j];
                    saved.Receipts.Add(new EventChainReceipt
                    {
                        Key = receipt.Key,
                        ActionId = receipt.ActionId,
                        Status = receipt.Status,
                        Data = receipt.Data,
                        Reason = receipt.Reason,
                        Revision = receipt.Revision,
                        Tick = receipt.Tick
                    });
                }

                copy.Instances.Add(saved);
            }

            return copy;
        }

        sealed class ArrivalProbe : IEventChainProvider, IEventChainDefinition
        {
            readonly IReadOnlyList<IEventChainDefinition> definitions;
            internal int Arrivals;

            internal ArrivalProbe()
            {
                definitions = new IEventChainDefinition[] { this };
            }

            public string ProviderId => "tests.predation.queue";
            public int ApiVersion => 1;
            public int StateVersion => 1;
            public IReadOnlyList<IEventChainDefinition> Definitions => definitions;
            public string Id => "arrival";
            public int Version => 1;

            public void Prepare(EventChainInstance instance)
            {
            }

            public EventChainDecision Evaluate(EventChainInstance instance, int tick)
            {
                return tick < instance.NextTick
                    ? EventChainDecision.Wait(instance.NextTick)
                    : EventChainDecision.Action("arrive_predator", -1, 2);
            }

            public EventChainActionResult Apply(EventChainInstance instance, int tick, string actionId)
            {
                Assert.Equal("arrive_predator", actionId);
                Arrivals++;
                return EventChainActionResult.Applied("arrived", EventChainEnd.Completed);
            }
        }
    }
}
