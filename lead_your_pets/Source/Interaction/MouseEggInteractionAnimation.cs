using Verse;
using UnityEngine;

namespace LeadYourPet
{
    public class MouseEggInteractionAnimation
    {
        public Pawn Master;
        public Pawn Pet;
        internal Map StartMap;
        internal LeashLink StartLink;
        internal LeashAnchorMode StartAnchorMode;
        internal Thing StartAnchorThing;
        internal IntVec3 StartAnchorCell;
        internal IntVec3 StartPetCell;
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
