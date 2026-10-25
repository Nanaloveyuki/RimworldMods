using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LeadYourPet
{
    public partial class LeadYourPetGameComponent : GameComponent
    {
        private static readonly List<LeashLink> EmptyLinks = new List<LeashLink>(0);
        private static readonly List<Pawn> SinglePawnBuffer = new List<Pawn>(1);
        private List<LeashLink> links = new List<LeashLink>();
        private List<YoungRatkinState> youngRatkinStates = new List<YoungRatkinState>();
        private readonly List<YoungRatkinInteractionAnimation> activeAnimations = new List<YoungRatkinInteractionAnimation>();
        private readonly Dictionary<Pawn, YoungRatkinInteractionAnimation> animationByPet = new Dictionary<Pawn, YoungRatkinInteractionAnimation>();
        private readonly HashSet<int> processedTravelLords = new HashSet<int>();
        private readonly List<LeashLink> linkSnapshot = new List<LeashLink>();
        private readonly Dictionary<Pawn, LeashLink> linkByPet = new Dictionary<Pawn, LeashLink>();
        private readonly Dictionary<Pawn, List<LeashLink>> linksByMaster = new Dictionary<Pawn, List<LeashLink>>();
        private readonly Dictionary<Pawn, YoungRatkinState> youngRatkinStateByPawn = new Dictionary<Pawn, YoungRatkinState>();
        private readonly Dictionary<int, int> departurePreparedTicks = new Dictionary<int, int>();

        public LeadYourPetGameComponent(Game game)
        {
            GodHandsCompat.Reset();
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref youngRatkinStates, "mouseEggStates", LookMode.Deep);
            Scribe_Collections.Look(ref links, "links", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                GodHandsCompat.Reset();
                if (youngRatkinStates == null)
                {
                    youngRatkinStates = new List<YoungRatkinState>();
                }

                if (links == null)
                {
                    links = new List<LeashLink>();
                }

                youngRatkinStates.RemoveAll(x => x == null || x.Pawn == null);
                youngRatkinStates.RemoveAll(x => x != null && !x.IsPet && x.CurrentMaster == null && !x.IsTravelStock);
                links.RemoveAll(x => x == null || x.Master == null || x.Pet == null);
                RebuildCaches();
            }
        }

        public override void GameComponentTick()
        {
            int ticksGame = Find.TickManager.TicksGame;
            if (ticksGame % 6 != 0)
            {
                return;
            }

            linkSnapshot.Clear();
            linkSnapshot.AddRange(links);
            for (int i = 0; i < linkSnapshot.Count; i++)
            {
                LeashLink link = linkSnapshot[i];
                if (link == null)
                {
                    continue;
                }

                if (!LeadYourPetRules.ShouldProcessLinkOnTick(
                    currentTick: ticksGame,
                    bucketCount: 4,
                    linkBucket: link.UpdateBucket,
                    forceImmediate: link.ForceImmediateUpdate))
                {
                    continue;
                }

                ProcessLink(link, ticksGame);
            }

            TickAnimations();
        }

        public LeashLink GetLinkForPet(Pawn pet)
        {
            if (pet == null)
            {
                return null;
            }

            linkByPet.TryGetValue(pet, out LeashLink link);
            return link;
        }

        public bool HasAnyLeash()
        {
            return links.Count > 0;
        }

        public bool HasAnyLeashForPawn(Pawn pawn)
        {
            return pawn != null && (linkByPet.ContainsKey(pawn) || linksByMaster.ContainsKey(pawn));
        }

        public LeashLink GetLinkForMaster(Pawn master)
        {
            if (master == null)
            {
                return null;
            }

            return linksByMaster.TryGetValue(master, out List<LeashLink> masterLinks) && masterLinks.Count > 0
                ? masterLinks[0]
                : null;
        }

        public List<LeashLink> GetLinksForMaster(Pawn master)
        {
            if (master == null)
            {
                return EmptyLinks;
            }

            return linksByMaster.TryGetValue(master, out List<LeashLink> masterLinks)
                ? masterLinks
                : EmptyLinks;
        }

        public YoungRatkinState GetYoungRatkinState(Pawn pawn)
        {
            if (pawn == null)
            {
                return null;
            }

            youngRatkinStateByPawn.TryGetValue(pawn, out YoungRatkinState state);
            return state;
        }

        public YoungRatkinState EnsureYoungRatkinState(Pawn pawn)
        {
            YoungRatkinState state = GetYoungRatkinState(pawn);
            if (state != null)
            {
                return state;
            }

            state = new YoungRatkinState
            {
                Pawn = pawn
            };
            youngRatkinStates.Add(state);
            RegisterYoungRatkinState(state);
            return state;
        }





        public bool HasRatkinMotherLeash(Pawn baby)
        {
            LeashLink link = GetLinkForPet(baby);
            return link != null && link.Kind == LeashLinkKind.RatkinMotherBaby;
        }

        public bool AnyLeashedPetFor(Pawn pawn)
        {
            return GetLinkForMaster(pawn) != null;
        }

        public bool AnyYoungRatkinPetFor(Pawn pawn)
        {
            List<LeashLink> masterLinks = GetLinksForMaster(pawn);
            for (int i = 0; i < masterLinks.Count; i++)
            {
                if (masterLinks[i].Kind == LeashLinkKind.YoungRatkinPet)
                {
                    return true;
                }
            }

            return false;
        }

        public bool AnyColonistYoungRatkinPetFor(Pawn pawn)
        {
            List<LeashLink> masterLinks = GetLinksForMaster(pawn);
            for (int i = 0; i < masterLinks.Count; i++)
            {
                LeashLink link = masterLinks[i];
                if (link.Kind == LeashLinkKind.YoungRatkinPet && LeadYourPetUtility.IsColonistYoungRatkin(link.Pet))
                {
                    return true;
                }
            }

            return false;
        }

        public bool AnyRatkinMotherBabyFor(Pawn pawn)
        {
            List<LeashLink> masterLinks = GetLinksForMaster(pawn);
            for (int i = 0; i < masterLinks.Count; i++)
            {
                if (masterLinks[i].Kind == LeashLinkKind.RatkinMotherBaby)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsYoungRatkinPet(Pawn pawn)
        {
            LeashLink link = GetLinkForPet(pawn);
            return link != null && link.Kind == LeashLinkKind.YoungRatkinPet;
        }

        public bool IsColonistYoungRatkinPet(Pawn pawn)
        {
            return LeadYourPetUtility.IsColonistYoungRatkin(pawn) && IsYoungRatkinPet(pawn);
        }

        public bool IsRatkinMotherBaby(Pawn pawn)
        {
            LeashLink link = GetLinkForPet(pawn);
            return link != null && link.Kind == LeashLinkKind.RatkinMotherBaby;
        }

        public bool HasAnchoredPetsFor(Pawn pawn)
        {
            List<LeashLink> masterLinks = GetLinksForMaster(pawn);
            for (int i = 0; i < masterLinks.Count; i++)
            {
                if (masterLinks[i].AnchorMode != LeashAnchorMode.None)
                {
                    return true;
                }
            }

            return false;
        }



        public void SetYoungRatkinSpecialSource(Pawn pawn, YoungRatkinSpecialSource source)
        {
            if (pawn == null || source == YoungRatkinSpecialSource.None)
            {
                return;
            }

            YoungRatkinState state = EnsureYoungRatkinState(pawn);
            state.SpecialSource = source;
        }

        public YoungRatkinSpecialSource GetYoungRatkinSpecialSource(Pawn pawn)
        {
            return GetYoungRatkinState(pawn)?.SpecialSource ?? YoungRatkinSpecialSource.None;
        }

        public bool HasSpecialMotherSourceMood(Pawn mother, YoungRatkinSpecialSource source)
        {
            if (mother?.relations?.Children == null)
            {
                return false;
            }

            foreach (Pawn child in mother.relations.Children)
            {
                if (child == null)
                {
                    continue;
                }

                if (LeadYourPetRules.ShouldHaveSpecialMotherMood(
                    LeadYourPetUtility.HasParentRelation(child, mother),
                    IsChildLeashedByMaster(mother, child),
                    GetYoungRatkinSpecialSource(child),
                    source))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasSpecialBabySourceMood(Pawn baby, YoungRatkinSpecialSource source)
        {
            if (baby == null)
            {
                return false;
            }

            Pawn mother = baby.GetMother();
            return mother != null && LeadYourPetRules.ShouldHaveSpecialBabyMood(
                LeadYourPetUtility.HasParentRelation(baby, mother),
                IsChildLeashedByMaster(mother, baby),
                GetYoungRatkinSpecialSource(baby),
                source);
        }




        private bool IsChildLeashedByMaster(Pawn master, Pawn child)
        {
            if (master == null || child == null)
            {
                return false;
            }

            LeashLink link = GetLinkForPet(child);
            return link != null && link.Master == master;
        }

        private void RebuildCaches()
        {
            linkByPet.Clear();
            linksByMaster.Clear();
            youngRatkinStateByPawn.Clear();

            for (int i = 0; i < youngRatkinStates.Count; i++)
            {
                RegisterYoungRatkinState(youngRatkinStates[i]);
            }

            // Replay old saves in creation order so the latest direction breaks a cycle.
            List<LeashLink> restoredLinks = links.OrderBy(x => x.CreatedTick).ToList();
            for (int i = 0; i < restoredLinks.Count; i++)
            {
                LeashLink link = restoredLinks[i];
                ReleaseIncomingLeashCycle(link.Master, link.Pet);
                RegisterLink(link);
            }
        }



    }
}
