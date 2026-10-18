using System.Collections.Generic;
using System;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using Verse;

namespace HungerAndHavoc.Identity
{
    internal sealed class RHAH_ApiHost : IRHAH_ApiHost
    {
        public IRHAH_Pawn Get(Verse.Pawn pawn)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            if (comp == null)
            {
                return null;
            }

            return comp.ToSnapshot();
        }

        public bool IsRatkin(Verse.Pawn pawn)
        {
            return RHAH_Race.IsRatkin(pawn?.def);
        }

        public IRHAH_Pawn TryMarkOrigin(Verse.Pawn pawn, RHAH_PawnSeed seed)
        {
            if (pawn?.health == null || seed == null)
            {
                return null;
            }

            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            if (comp == null)
            {
                HediffDef def = DefDatabase<HediffDef>.GetNamed("RHAH_HungerMark", false);
                if (def == null)
                {
                    return null;
                }

                pawn.health.AddHediff(HediffMaker.MakeHediff(def, pawn));
                comp = CompRHAH_Pawn.TryGet(pawn);
                if (comp == null)
                {
                    return null;
                }
            }

            comp.ApplySeed(seed);
            IRHAH_Pawn snapshot = comp.ToSnapshot();
            RHAH_Api.RaiseOriginMarked(pawn, snapshot);
            return snapshot;
        }

        public bool ReleaseToColony(Verse.Pawn pawn, RHAH_ReleaseReason reason)
        {
            if (!CanReleaseToColony(pawn, reason)) return false;
            CommitReleaseToColony(pawn, reason);
            return true;
        }

        internal static bool CanReleaseToColony(Verse.Pawn pawn, RHAH_ReleaseReason reason)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            if (comp == null) return false;
            if (comp.State.IsReleased) return true;
            if (!RHAH_PawnState.IsLegalTransition(comp.State.lifecycle, RHAH_Lifecycle.Released)) return false;
            IRHAH_Pawn current = comp.ToSnapshot();
            return RHAH_PawnBehaviors.Query(handler => handler.ShouldReleaseToColony(pawn, current, reason)) != false;
        }

        // Caller commits only after every participant has passed the release preflight.
        internal static void CommitReleaseToColony(Verse.Pawn pawn, RHAH_ReleaseReason reason)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            if (comp.State.IsReleased) return;
            ChangeLifecycle(pawn, RHAH_Lifecycle.Released);
            global::HungerAndHavoc.Pawn.RHAH_VisitorGroup.NotifyReleased(pawn);
            global::HungerAndHavoc.Pawn.Compat.RHAH_LeashBridge.ClearDeparture(pawn);
            RHAH_Api.RaiseReleasedToColony(pawn, comp.ToSnapshot(), reason);
            if ((reason == RHAH_ReleaseReason.Imprisoned || reason == RHAH_ReleaseReason.Enslaved) &&
                !Incidents.RHAH_Interception.IsInterception(comp.State.spawnBatchId))
            {
                global::HungerAndHavoc.Pawn.RHAH_SuiyinTrust.Note(1, global::HungerAndHavoc.Pawn.RHAH_SuiyinTrust.Value(global::HungerAndHavoc.Pawn.RHAH_SuiyinTrust.Hold, Core.RHAH_Mod.Settings == null ? global::HungerAndHavoc.Pawn.RHAH_SuiyinTrust.Hold : Core.RHAH_Mod.Settings.trustHold));
            }
        }

        public void SetLifecycle(Verse.Pawn pawn, RHAH_Lifecycle lifecycle)
        {
            ChangeLifecycle(pawn, lifecycle);
        }

        static void ChangeLifecycle(Verse.Pawn pawn, RHAH_Lifecycle lifecycle)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            if (comp == null)
            {
                return;
            }

            if (!comp.State.TrySetLifecycle(lifecycle))
            {
                return;
            }

            if (lifecycle == RHAH_Lifecycle.Released || lifecycle == RHAH_Lifecycle.Dead)
            {
                global::HungerAndHavoc.Pawn.RHAH_VisitorStay.UnlockShortStayApparel(pawn, comp);
                comp.SetStay(0, -1, 0);
            }

            RHAH_Api.RaiseLifecycleChanged(pawn, comp.ToSnapshot(), lifecycle);
        }

        public bool Allows(Verse.Pawn pawn, RHAH_BehaviorGate gate)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            if (comp == null)
            {
                return false;
            }

            IRHAH_Pawn snapshot = comp.ToSnapshot();
            bool? gateOverride = comp.State.GetGateOverride(gate);
            bool allowed;
            if (gateOverride.HasValue)
            {
                allowed = gateOverride.Value;
            }
            else
            {
                bool? behavior = RHAH_PawnBehaviors.Query(
                    handler => handler.Allows(pawn, snapshot, gate));
                allowed = behavior ?? RHAH_PawnDefaults.Allows(snapshot, gate);
            }

            RHAH_Api.RaiseGateQueried(pawn, snapshot, gate, allowed);
            return allowed;
        }

        internal static bool CanTransfer(Verse.Pawn pawn)
        {
            if (!RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Transfer)) return false;
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            if (comp == null) return false;
            bool? exitOverride = comp.State.GetGateOverride(RHAH_BehaviorGate.ExitMap);
            if (exitOverride.HasValue) return exitOverride.Value;
            IRHAH_Pawn current = comp.ToSnapshot();
            // 转送先下离场令，预检其后的离图权限，保留外部留驻政策。
            IRHAH_Pawn leaving = new RHAH_PawnSnapshot(current.SourceIncidentDisplayId,
                current.SpawnBatchId, current.RelationshipGroupId, current.Role, RHAH_Lifecycle.Leaving,
                current.HasBeenFed, current.LeaveAfterGameTick, current.CarriesPlague,
                current.AttitudeAtArrival, current.Attitude, current.ParentPawnLoadId, current.ChildPawnLoadIds);
            bool? behavior = RHAH_PawnBehaviors.Query(handler => handler.Allows(pawn, leaving, RHAH_BehaviorGate.ExitMap));
            return behavior ?? RHAH_PawnDefaults.Allows(leaving, RHAH_BehaviorGate.ExitMap);
        }

        public void SetGate(Verse.Pawn pawn, RHAH_BehaviorGate gate, bool? allowed)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            if (comp == null)
            {
                return;
            }

            comp.State.SetGate(gate, allowed);
        }

        public void SetExtra(Verse.Pawn pawn, string key, string value)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            if (comp == null)
            {
                return;
            }

            comp.State.SetExtra(key, value);
        }

        public bool TryGetExtra(Verse.Pawn pawn, string key, out string value)
        {
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            if (comp == null)
            {
                value = null;
                return false;
            }

            return comp.State.TryGetExtra(key, out value);
        }

        public void RegisterRatkinMatcher(Func<ThingDef, bool> matcher)
        {
            RHAH_Race.Register(matcher);
        }
        public bool IsOwnedHistory(string backstoryDefName)
        {
            return Data.RHAH_ContentCatalog.IsOwnedHistory(backstoryDefName);
        }

        public bool IsOwnedTrait(string traitDefName)
        {
            return Data.RHAH_ContentCatalog.IsOwnedTrait(traitDefName);
        }

        public bool TryGetHistory(string displayId, out string backstoryDefName)
        {
            Data.RHAH_HistoryRecord record = Data.RHAH_ContentCatalog.FindHistory(displayId);
            backstoryDefName = record?.BackstoryDefName;
            return record != null;
        }

        public bool TryGetTrait(string displayId, out string traitDefName)
        {
            Data.RHAH_TraitRecord record = Data.RHAH_ContentCatalog.FindTrait(displayId);
            traitDefName = record?.TraitDefName;
            return record != null;
        }

        public void CopyHistoryIds(List<string> destination)
        {
            if (destination == null)
            {
                return;
            }

            IReadOnlyList<Data.RHAH_HistoryRecord> records = Data.RHAH_ContentCatalog.Histories;
            for (int i = 0; i < records.Count; i++)
            {
                destination.Add(records[i].DisplayId);
            }
        }

        public void CopyTraitIds(List<string> destination)
        {
            if (destination == null)
            {
                return;
            }

            IReadOnlyList<Data.RHAH_TraitRecord> records = Data.RHAH_ContentCatalog.Traits;
            for (int i = 0; i < records.Count; i++)
            {
                destination.Add(records[i].DisplayId);
            }
        }
    }
}
