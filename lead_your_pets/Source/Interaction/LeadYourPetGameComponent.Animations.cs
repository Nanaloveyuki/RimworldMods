using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LeadYourPet
{
    public partial class LeadYourPetGameComponent
    {
        public MouseEggInteractionAnimation GetInteractionAnimationForPet(Pawn pet)
        {
            return pet != null && animationByPet.TryGetValue(pet, out MouseEggInteractionAnimation anim) ? anim : null;
        }

        public bool HasBlockingInteractionAnimation(Pawn pet)
        {
            MouseEggInteractionAnimation anim = GetInteractionAnimationForPet(pet);
            return anim != null && anim.BlocksPetMovement;
        }

        public bool HasLayingInteractionAnimation(Pawn pet)
        {
            MouseEggInteractionAnimation anim = GetInteractionAnimationForPet(pet);
            return anim != null && anim.PetUsesCustomRender && anim.PetLaying;
        }

        public bool TryGetInteractionAnimationRenderData(Pawn pet, out Vector3 drawPos, out float angle, out bool laying, out Rot4 facing)
        {
            drawPos = default(Vector3);
            angle = 0f;
            laying = false;
            facing = Rot4.South;

            MouseEggInteractionAnimation anim = GetInteractionAnimationForPet(pet);
            if (anim == null || !anim.PetUsesCustomRender || !CanContinueInteractionAnimation(anim))
            {
                return false;
            }

            float pct = AnimationProgress(anim);
            drawPos = anim.StartDrawPos + (anim.TravelOffset * pct);
            drawPos.y = anim.PetLaying ? AltitudeLayer.LayingPawn.AltitudeFor() : pet.DrawPos.y;
            angle = Mathf.Lerp(anim.StartAngle, anim.EndAngle, pct);
            laying = anim.PetLaying;
            facing = anim.CustomFacingSouth ? Rot4.South : pet.Rotation;
            return true;
        }

        private void TickAnimations()
        {
            for (int i = activeAnimations.Count - 1; i >= 0; i--)
            {
                MouseEggInteractionAnimation anim = activeAnimations[i];
                if (!CanContinueInteractionAnimation(anim))
                {
                    RemoveInteractionAnimationAt(i);
                    continue;
                }
                if (Find.TickManager.TicksGame > anim.EndTick)
                {
                    FinalizeInteractionAnimation(anim);
                    RemoveInteractionAnimationAt(i);
                    continue;
                }

                if (anim.EmitCryMotes && Find.TickManager.TicksGame >= anim.NextCryTick)
                {
                    EmitCryingMotes(anim.Pet);
                    anim.NextCryTick = Find.TickManager.TicksGame + 35;
                }

                if (!YayoAnimationCompat.Active)
                {
                    continue;
                }

                float pct = (Find.TickManager.TicksGame - anim.StartTick) / (float)Mathf.Max(1, anim.EndTick - anim.StartTick);
                float wave = Mathf.Sin(pct * Mathf.PI * 2f);
                float angle = wave * 8f;
                Vector3 petOffset = new Vector3(-wave * 0.02f, 0f, 0.02f * Mathf.Abs(wave));

                switch (anim.Kind)
                {
                    case LeadYourPetInteractionKind.Whirl:
                        petOffset = Vector3.zero;
                        angle = 0f;
                        break;
                    case LeadYourPetInteractionKind.Kick:
                    case LeadYourPetInteractionKind.KickButt:
                    case LeadYourPetInteractionKind.Slap:
                        petOffset = new Vector3(-wave * 0.06f, 0f, 0f);
                        angle = wave * 12f;
                        break;
                    case LeadYourPetInteractionKind.TapHead:
                    case LeadYourPetInteractionKind.PullEar:
                    case LeadYourPetInteractionKind.PullTail:
                    case LeadYourPetInteractionKind.Pat:
                        petOffset = new Vector3(0f, 0f, -0.02f * Mathf.Abs(wave));
                        angle = wave * 5f;
                        break;
                    case LeadYourPetInteractionKind.Observe:
                        petOffset = new Vector3(wave * 0.02f, 0f, 0.03f * Mathf.Abs(wave));
                        angle = wave * 12f;
                        break;
                    case LeadYourPetInteractionKind.WatchWork:
                        petOffset = new Vector3(wave * 0.03f, 0f, 0.02f * Mathf.Abs(wave));
                        angle = wave * 7f;
                        break;
                    case LeadYourPetInteractionKind.Fed:
                        petOffset = new Vector3(0f, 0f, 0.035f * Mathf.Abs(wave));
                        angle = wave * 5f;
                        break;
                    case LeadYourPetInteractionKind.RestAtFeet:
                        petOffset = new Vector3(0f, 0f, 0.012f * Mathf.Abs(wave));
                        angle = wave * 2f;
                        break;
                    case LeadYourPetInteractionKind.IdleNearOwner:
                        petOffset = new Vector3(wave * 0.018f, 0f, 0f);
                        angle = wave * 4f;
                        break;
                    case LeadYourPetInteractionKind.StudyFloor:
                        petOffset = new Vector3(0f, 0f, -0.025f * Mathf.Abs(wave));
                        angle = wave * 9f;
                        break;
                    case LeadYourPetInteractionKind.SillySmile:
                        petOffset = new Vector3(wave * 0.02f, 0f, 0.018f * Mathf.Abs(wave));
                        angle = wave * 15f;
                        break;
                    case LeadYourPetInteractionKind.TailPetting:
                        petOffset = new Vector3(0f, 0f, 0.025f * Mathf.Abs(wave));
                        angle = wave * 10f;
                        break;
                    case LeadYourPetInteractionKind.EarPetting:
                        petOffset = new Vector3(0f, 0f, 0.022f * Mathf.Abs(wave));
                        angle = wave * 11f;
                        break;
                    case LeadYourPetInteractionKind.OwnerWaited:
                        petOffset = new Vector3(0f, 0f, 0.02f * Mathf.Abs(wave));
                        angle = wave * 6f;
                        break;
                    case LeadYourPetInteractionKind.Snack:
                        petOffset = new Vector3(0f, 0f, -0.02f * Mathf.Abs(wave));
                        angle = wave * 8f;
                        break;
                }

                if (!anim.PetUsesCustomRender)
                {
                    YayoAnimationCompat.Apply(anim.Pet, petOffset, -angle, Find.TickManager.TicksGame + 2, anim.Pet.CurJob?.def?.defName);
                }
            }
        }

        private bool CanContinueInteractionAnimation(MouseEggInteractionAnimation anim)
        {
            if (anim?.Master == null || anim.Pet == null || anim.StartMap == null || anim.StartLink == null
                || !CanAnimateInteraction(anim.Master, anim.Pet, out LeashLink link)
                || link != anim.StartLink || anim.Pet.Map != anim.StartMap
                || link.AnchorMode != anim.StartAnchorMode || link.AnchorThing != anim.StartAnchorThing
                || link.AnchorCell != anim.StartAnchorCell
                || (anim.BlocksPetMovement && anim.Pet.Position != anim.StartPetCell)
                || !CanRunInteractionAnimation(anim.Master, anim.Pet, link, anim.Kind))
            {
                return false;
            }

            return !anim.FinalizeTeleport || IsLegalKnockbackCell(anim.Master, anim.Pet, anim.TargetCell);
        }

        private bool CanAnimateInteraction(Pawn master, Pawn pet, out LeashLink link)
        {
            link = GetLinkForPet(pet);
            return master != null && pet != null && link != null && link.Master == master
                && master.Spawned && pet.Spawned && !master.Dead && !pet.Dead && master.Map == pet.Map
                && !GodHandsCompat.IsGrabbed(master) && !GodHandsCompat.IsGrabbed(pet)
                && !(LeadYourPetUtility.GetCurrentFollowThing(link) is Pawn anchor && GodHandsCompat.IsGrabbed(anchor))
                && !LeadYourPetUtility.ShouldPreserveMapExitMovement(master)
                && !LeadYourPetUtility.ShouldPreserveMapExitMovement(pet)
                && !ShouldFollowDuringDeparture(link);
        }

        private static bool CanRunInteractionAnimation(Pawn master, Pawn pet, LeashLink link, LeadYourPetInteractionKind kind)
        {
            return kind == LeadYourPetInteractionKind.Drag
                || (!master.Downed && master.Awake() && !LeadYourPetUtility.MasterSleeping(master)
                    && !LeadYourPetUtility.MasterEating(master)
                    && !pet.Downed && pet.Awake() && !LeadYourPetUtility.HasLeashCareJob(link));
        }

        private static void ClearInteractionAnimation(MouseEggInteractionAnimation anim)
        {
            YayoAnimationCompat.Clear(anim?.Pet);
            YayoAnimationCompat.Clear(anim?.Master);
        }

        private void RemoveInteractionAnimationAt(int index)
        {
            MouseEggInteractionAnimation anim = activeAnimations[index];
            ClearInteractionAnimation(anim);
            if (anim?.Pet != null) animationByPet.Remove(anim.Pet);
            activeAnimations.RemoveAt(index);
        }

        private void RegisterInteractionAnimation(MouseEggInteractionAnimation anim)
        {
            activeAnimations.Add(anim);
            animationByPet.Add(anim.Pet, anim);
        }

        private void StartInteractionAnimation(Pawn master, Pawn pet, LeadYourPetInteractionKind kind)
        {
            if (!TryBuildInteractionAnimation(master, pet, kind, out MouseEggInteractionAnimation anim))
            {
                return;
            }

            for (int i = activeAnimations.Count - 1; i >= 0; i--)
            {
                MouseEggInteractionAnimation previous = activeAnimations[i];
                if (previous == null || previous.Master == null || previous.Pet == null
                    || previous.Master == master || previous.Pet == pet || previous.Master == pet || previous.Pet == master)
                {
                    RemoveInteractionAnimationAt(i);
                }
            }
            RegisterInteractionAnimation(anim);
        }
        private void StopInteractionAnimation(Pawn pet, bool finalizeTeleport = true)
        {
            if (pet == null)
            {
                return;
            }

            for (int i = activeAnimations.Count - 1; i >= 0; i--)
            {
                MouseEggInteractionAnimation anim = activeAnimations[i];
                if (anim?.Pet != pet)
                {
                    continue;
                }

                if (finalizeTeleport)
                {
                    FinalizeInteractionAnimation(anim);
                }
                RemoveInteractionAnimationAt(i);
            }
        }


        private bool TryBuildInteractionAnimation(Pawn master, Pawn pet, LeadYourPetInteractionKind kind, out MouseEggInteractionAnimation anim)
        {
            anim = null;
            if (!CanAnimateInteraction(master, pet, out LeashLink link)
                || !CanRunInteractionAnimation(master, pet, link, kind))
            {
                return false;
            }

            int startTick = Find.TickManager.TicksGame;
            int duration = AnimationDurationFor(kind);
            anim = new MouseEggInteractionAnimation
            {
                Master = master,
                Pet = pet,
                StartMap = pet.Map,
                StartLink = link,
                StartAnchorMode = link.AnchorMode,
                StartAnchorThing = link.AnchorThing,
                StartAnchorCell = link.AnchorCell,
                StartPetCell = pet.Position,
                Kind = kind,
                StartTick = startTick,
                EndTick = startTick + duration,
                NextCryTick = startTick + 35,
                TargetCell = pet.Position,
                StartDrawPos = pet.DrawPos,
                TravelOffset = Vector3.zero,
                StartAngle = 0f,
                EndAngle = 0f,
                PetUsesCustomRender = false,
                PetLaying = false,
                BlocksPetMovement = false,
                FinalizeTeleport = false,
                EmitCryMotes = false,
                CustomFacingSouth = false
            };

            switch (kind)
            {
                case LeadYourPetInteractionKind.Kick:
                    ConfigureKnockbackAnimation(anim, master, pet, 3, 0f, false);
                    return true;
                case LeadYourPetInteractionKind.PullTail:
                case LeadYourPetInteractionKind.PullEar:
                    ConfigureLayingCryAnimation(anim, pet, duration);
                    return true;
                case LeadYourPetInteractionKind.Whirl:
                    anim.PetUsesCustomRender = true;
                    anim.PetLaying = true;
                    anim.BlocksPetMovement = true;
                    anim.StartAngle = 0f;
                    anim.EndAngle = 3600f;
                    anim.CustomFacingSouth = true;
                    return true;
                case LeadYourPetInteractionKind.Slap:
                    anim.PetUsesCustomRender = true;
                    anim.PetLaying = false;
                    anim.BlocksPetMovement = true;
                    anim.StartAngle = 0f;
                    anim.EndAngle = 1800f;
                    anim.EmitCryMotes = true;
                    anim.CustomFacingSouth = false;
                    return true;
                case LeadYourPetInteractionKind.KickButt:
                    ConfigureKnockbackAnimation(anim, master, pet, 5, 360f, false);
                    return true;
                default:
                    return true;
            }
        }

        private void ConfigureLayingCryAnimation(MouseEggInteractionAnimation anim, Pawn pet, int duration)
        {
            anim.PetUsesCustomRender = true;
            anim.PetLaying = true;
            anim.BlocksPetMovement = true;
            anim.EmitCryMotes = true;
            anim.CustomFacingSouth = true;
            anim.EndTick = anim.StartTick + duration;
            anim.StartDrawPos = pet.DrawPos;
        }

        private void ConfigureKnockbackAnimation(MouseEggInteractionAnimation anim, Pawn master, Pawn pet, int distance, float spinDegrees, bool laying)
        {
            IntVec3 target = ResolveKnockbackCell(master, pet, distance);
            anim.TargetCell = target;
            anim.PetUsesCustomRender = true;
            anim.PetLaying = laying;
            anim.BlocksPetMovement = true;
            anim.FinalizeTeleport = target != pet.Position;
            anim.StartAngle = 0f;
            anim.EndAngle = spinDegrees;
            anim.StartDrawPos = pet.DrawPos;
            anim.TravelOffset = new Vector3(target.x - pet.Position.x, 0f, target.z - pet.Position.z);
            anim.CustomFacingSouth = laying;
        }

        private IntVec3 ResolveKnockbackCell(Pawn master, Pawn pet, int distance)
        {
            if (!CanAnimateInteraction(master, pet, out _))
            {
                return pet?.Position ?? IntVec3.Invalid;
            }

            IntVec3 direction = pet.Position - master.Position;
            direction = new IntVec3(Mathf.Clamp(direction.x, -1, 1), 0, Mathf.Clamp(direction.z, -1, 1));
            if (direction == IntVec3.Zero)
            {
                direction = master.Rotation.FacingCell;
            }

            IntVec3 best = pet.Position;
            for (int i = 1; i <= distance; i++)
            {
                IntVec3 candidate = pet.Position + (direction * i);
                if (!IsLegalKnockbackCell(master, pet, candidate))
                {
                    break;
                }
                best = candidate;
            }

            if (best == pet.Position)
            {
                IntVec3 root = pet.Position + direction;
                if (CellFinder.TryFindRandomCellNear(root, pet.Map, Mathf.Max(1, distance),
                    candidate => candidate.InHorDistOf(root, Mathf.Max(1, distance)) && IsLegalKnockbackCell(master, pet, candidate),
                    out IntVec3 fallback, maxTries: 100))
                {
                    best = fallback;
                }
            }

            return best;
        }

        private bool IsLegalKnockbackCell(Pawn master, Pawn pet, IntVec3 candidate)
        {
            if (!CanAnimateInteraction(master, pet, out LeashLink link))
            {
                return false;
            }

            Map map = pet.Map;
            Thing anchor = LeadYourPetUtility.GetCurrentFollowThing(link);
            IntVec3 anchorCell = anchor != null ? anchor.Position : LeadYourPetUtility.GetAnchorCell(link);
            if ((anchor != null && (!anchor.Spawned || anchor.Map != map || anchor == pet))
                || !anchorCell.IsValid || !anchorCell.InBounds(map)
                || !candidate.IsValid || !candidate.InBounds(map)
                || candidate.DistanceTo(anchorCell) > LeadYourPetUtility.MaxLeashLength - 0.1f
                || !candidate.Standable(map))
            {
                return false;
            }

            IntVec3 previous = pet.Position;
            foreach (IntVec3 cell in GenSight.PointsOnLineOfSight(pet.Position, candidate))
            {
                if (cell == pet.Position)
                {
                    continue;
                }
                if (!cell.InBounds(map) || !cell.Standable(map))
                {
                    return false;
                }
                if (cell.x != previous.x && cell.z != previous.z
                    && (!new IntVec3(cell.x, 0, previous.z).Standable(map)
                        || !new IntVec3(previous.x, 0, cell.z).Standable(map)))
                {
                    return false;
                }
                previous = cell;
            }

            return true;
        }

        private float AnimationProgress(MouseEggInteractionAnimation anim)
        {
            return Mathf.Clamp01((Find.TickManager.TicksGame - anim.StartTick) / (float)Mathf.Max(1, anim.EndTick - anim.StartTick));
        }

        private void FinalizeInteractionAnimation(MouseEggInteractionAnimation anim)
        {
            if (anim == null || !anim.FinalizeTeleport || !CanContinueInteractionAnimation(anim)
                || anim.TargetCell == anim.Pet.Position)
            {
                return;
            }

            anim.Pet.pather?.StopDead();
            anim.Pet.Position = anim.TargetCell;
            anim.Pet.Notify_Teleported(endCurrentJob: false, resetTweenedPos: false);
        }

        private void EmitCryingMotes(Pawn pet)
        {
            if (pet?.Map == null || !pet.Spawned || pet.Position.Fogged(pet.Map))
            {
                return;
            }

            float bodyAngle = pet.Drawer?.renderer?.BodyAngle(PawnRenderFlags.None) ?? 0f;
            MoteMaker.MakeAttachedOverlay(pet, ThingDefOf.Mote_BabyCryingDots, new Vector3(0.27f, 0f, 0.066f).RotatedBy(bodyAngle)).exactRotation = Rand.Value * 180f;
            MoteMaker.MakeAttachedOverlay(pet, ThingDefOf.Mote_BabyCryingDots, new Vector3(-0.27f, 0f, 0.066f).RotatedBy(bodyAngle)).exactRotation = Rand.Value * 180f;
            ApplyCryingNearbyMood(pet);
        }

        private void ApplyCryingNearbyMood(Pawn pet)
        {
            if (pet?.Map?.mapPawns?.AllPawnsSpawned == null)
            {
                return;
            }

            IReadOnlyList<Pawn> pawns = pet.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn == pet || pawn.Dead || !pawn.RaceProps.Humanlike)
                {
                    continue;
                }

                if (!pawn.Position.InHorDistOf(pet.Position, 8f))
                {
                    continue;
                }

                if (!LeadYourPetUtility.ShouldApplyMouseEggCryingNearbyMood(pet, pawn))
                {
                    continue;
                }

                pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(LeadYourPetDefOf.LeadYourPet_MouseEggCryingNearbyMood, pet);
            }
        }

        private int AnimationDurationFor(LeadYourPetInteractionKind kind)
        {
            return LeadYourPetRules.GetInteractionAnimationDuration(kind);
        }
    }
}
