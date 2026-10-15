using Verse;
using UnityEngine;

namespace LeadYourPet
{
    public class MouseEggInteractionAnimation
    {
        public Pawn Master;
        public Pawn Pet;
        public LeadYourPetInteractionKind Kind;
        public int StartTick;
        public int EndTick;
        public int NextCryTick;
        public IntVec3 TargetCell;
        public Vector3 StartDrawPos;
        public Vector3 TravelOffset;
        public float StartAngle;
        public float EndAngle;
        public bool PetUsesCustomRender;
        public bool PetLaying;
        public bool BlocksPetMovement;
        public bool FinalizeTeleport;
        public bool EmitCryMotes;
        public bool CustomFacingSouth = true;
    }
}
