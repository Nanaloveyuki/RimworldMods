using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LeadYourPet
{
    public partial class LeadYourPetGameComponent
    {
        public MouseEggInteractionAnimation GetInteractionAnimationForPet(Pawn pet)
        {
            return activeAnimations.FirstOrDefault(x => x?.Pet == pet);
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
            if (anim == null || !anim.PetUsesCustomRender || pet == null
                || GodHandsCompat.IsGrabbed(pet) || GodHandsCompat.IsGrabbed(anim.Master)
                || LeadYourPetUtility.ShouldPreserveMapExitMovement(pet))
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
                if (GodHandsCompat.IsGrabbed(anim.Pet) || GodHandsCompat.IsGrabbed(anim.Master))
                {
                    YayoAnimationCompat.Clear(anim.Pet);
                    YayoAnimationCompat.Clear(anim.Master);
                    activeAnimations.RemoveAt(i);
                    continue;
                }
                if (anim.Master == null || anim.Pet == null || Find.TickManager.TicksGame > anim.EndTick)
                {
                    FinalizeInteractionAnimation(anim);

                    if (anim?.Master != null)
                    {
                        YayoAnimationCompat.Clear(anim.Master);
                    }

                    if (anim?.Pet != null)
                    {
                        YayoAnimationCompat.Clear(anim.Pet);
                    }

                    activeAnimations.RemoveAt(i);
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

        private void StartInteractionAnimation(Pawn master, Pawn pet, LeadYourPetInteractionKind kind)
        {
            if (!TryBuildInteractionAnimation(master, pet, kind, out MouseEggInteractionAnimation anim))
            {
                return;
            }

            activeAnimations.RemoveAll(x => x == null || x.Master == null || x.Pet == null || x.Master == master || x.Pet == pet || x.Master == pet || x.Pet == master);
            activeAnimations.Add(anim);
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
                YayoAnimationCompat.Clear(pet);
                YayoAnimationCompat.Clear(anim.Master);
                activeAnimations.RemoveAt(i);
            }
        }


        private bool TryBuildInteractionAnimation(Pawn master, Pawn pet, LeadYourPetInteractionKind kind, out MouseEggInteractionAnimation anim)
        {
            anim = null;
            if (master == null || pet == null || !pet.Spawned)
            {
                return false;
            }

            int startTick = Find.TickManager.TicksGame;
            int duration = AnimationDurationFor(kind);
            anim = new MouseEggInteractionAnimation
            {
                Master = master,
                Pet = pet,
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
            if (pet?.Map == null)
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
                if (!candidate.InBounds(pet.Map) || !candidate.Standable(pet.Map))
                {
                    break;
                }

                if (candidate.DistanceTo(master.Position) > LeadYourPetUtility.MaxLeashLength - 0.1f)
                {
                    break;
                }

                best = candidate;
            }

            if (best == pet.Position)
            {
                IntVec3 fallback = CellFinder.RandomClosewalkCellNear(pet.Position + direction, pet.Map, Mathf.Max(1, distance));
                if (fallback.IsValid && fallback.InBounds(pet.Map) && fallback.Standable(pet.Map))
                {
                    best = fallback;
                }
            }

            return best;
        }

        private float AnimationProgress(MouseEggInteractionAnimation anim)
        {
            return Mathf.Clamp01((Find.TickManager.TicksGame - anim.StartTick) / (float)Mathf.Max(1, anim.EndTick - anim.StartTick));
        }

        private void FinalizeInteractionAnimation(MouseEggInteractionAnimation anim)
        {
            if (anim?.Pet == null || !anim.FinalizeTeleport || !anim.Pet.Spawned || anim.TargetCell == anim.Pet.Position || !anim.TargetCell.IsValid || !anim.TargetCell.InBounds(anim.Pet.Map))
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
