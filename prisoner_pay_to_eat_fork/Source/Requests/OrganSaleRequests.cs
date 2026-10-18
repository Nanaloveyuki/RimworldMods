using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PrisonersPayToEat2
{
    /// <summary>Letters are the saved pending state; only explicit player approval creates a bill.</summary>
    public static class OrganSaleRequests
    {
        public const int CheckInterval = 250;
        public const int CooldownTicks = 120000;
        public const string LetterDefName = "PPTE2_OrganSaleRequest";

        public static bool BelowTrigger(Pawn pawn)
        {
            var manager = PrisonersPayToEat2Manager.Current;
            var settings = PrisonersPayToEat2Mod.Settings;
            return pawn != null && manager != null && settings != null
                && manager.Balance(pawn) < settings.organSaleTriggerBalance;
        }

        public static bool Eligible(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && !pawn.Destroyed && pawn.Spawned
                && pawn.Map != null && pawn.IsPrisonerOfColony
                && PrisonersPayToEat2Manager.Current?.CanHarvestOrgans(pawn) == true
                && BelowTrigger(pawn);
        }

        public static bool HasPendingBill(Pawn pawn)
        {
            var bills = pawn.BillStack.Bills;
            for (int i = 0; i < bills.Count; i++)
            {
                if (bills[i].recipe.Worker is RecipeWorker_OrganSale) return true;
                if (bills[i] is Bill_Medical medical && medical.Part?.depth == BodyPartDepth.Inside
                    && bills[i].recipe.Worker is Recipe_RemoveBodyPart) return true;
            }
            return false;
        }

        private static bool HasPendingLetter(Pawn pawn)
        {
            var letters = Find.LetterStack.LettersListForReading;
            for (int i = 0; i < letters.Count; i++)
                if (letters[i] is ChoiceLetter_OrganSale letter && !letter.resolved && letter.prisoner == pawn) return true;
            return false;
        }

        public static void Cooldown(Pawn pawn)
        {
            var manager = PrisonersPayToEat2Manager.Current;
            if (manager != null && pawn != null)
                manager.DataFor(pawn).organRequestCooldownUntilTick = Find.TickManager.TicksGame + CooldownTicks;
        }

        public static void Tick()
        {
            var game = Current.Game;
            if (game == null || game.tickManager.TicksGame % CheckInterval != 0) return;
            var manager = PrisonersPayToEat2Manager.For(game);
            if (manager == null) return;
            // Clean up pending letters even if all organ-sale settings were subsequently disabled.
            var letters = Find.LetterStack.LettersListForReading;
            for (int i = letters.Count - 1; i >= 0; i--)
            {
                if (letters[i] is ChoiceLetter_OrganSale letter && !letter.IsValid())
                    Find.LetterStack.RemoveLetter(letter);
            }
            int now = game.tickManager.TicksGame;
            foreach (var map in game.Maps)
            {
                var prisoners = map.mapPawns.PrisonersOfColonySpawned;
                for (int i = 0; i < prisoners.Count; i++)
                {
                    var pawn = prisoners[i];
                    if (!Eligible(pawn)) continue;
                    var data = manager.DataFor(pawn);
                    if (now < data.organRequestCooldownUntilTick || HasPendingBill(pawn) || HasPendingLetter(pawn)) continue;
                    var part = OrganHarvestHelper.SelectOrgan(pawn);
                    if (part == null) continue;
                    var def = DefDatabase<LetterDef>.GetNamed(LetterDefName);
                    var letter = (ChoiceLetter_OrganSale)LetterMaker.MakeLetter(
                        "PPTE2_OrganRequestLabel".Translate(pawn.LabelShortCap),
                        "PPTE2_OrganRequestText".Translate(pawn.LabelShortCap, manager.Balance(pawn),
                            PrisonersPayToEat2Mod.Settings.organSaleTriggerBalance, part.Label,
                            OrganHarvestHelper.OrganPayout(OrganHarvestHelper.OrganKey(part)), PPTEName.Ticket),
                        def, new LookTargets(pawn));
                    letter.prisoner = pawn;
                    letter.part = part;
                    Find.LetterStack.ReceiveLetter(letter);
                }
            }
        }
    }

    public class ChoiceLetter_OrganSale : ChoiceLetter
    {
        public Pawn prisoner;
        public BodyPartRecord part;
        public bool resolved;

        private bool Active => !resolved && Find.LetterStack.LettersListForReading.Contains(this);

        public bool IsValid()
        {
            if (resolved || !OrganSaleRequests.Eligible(prisoner) || OrganSaleRequests.HasPendingBill(prisoner)) return false;
            var recipe = OrganHarvestHelper.RecipeFor(part);
            return recipe != null && recipe.Worker.AvailableOnNow(prisoner, part)
                && OrganHarvestHelper.CanRemove(prisoner, part);
        }

        public bool TryApprove()
        {
            // Dialogs and the archive can retain callbacks after removal. They cannot resurrect a
            // request or schedule another operation, including after a save/load round trip.
            if (!Active) return false;
            if (!IsValid())
            {
                Find.LetterStack.RemoveLetter(this);
                Messages.Message("PPTE2_OrganRequestUnavailable".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }
            var bill = new Bill_OrganSale(OrganHarvestHelper.RecipeFor(part));
            prisoner.BillStack.AddBill(bill);
            bill.Part = part;
            resolved = true;
            OrganSaleRequests.Cooldown(prisoner);
            Find.LetterStack.RemoveLetter(this);
            Messages.Message("PPTE2_OrganSurgeryAdded".Translate(prisoner.LabelShortCap, part.Label),
                prisoner, MessageTypeDefOf.NeutralEvent);
            return true;
        }

        public void Decline()
        {
            if (Active) Find.LetterStack.RemoveLetter(this);
        }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (!Active)
                {
                    yield return Option_Close;
                    yield break;
                }
                var approve = new DiaOption("PPTE2_OrganRequestApprove".Translate())
                {
                    action = () => TryApprove(),
                    resolveTree = true
                };
                if (!IsValid()) approve.Disable("PPTE2_OrganRequestUnavailable".Translate());
                yield return approve;
                yield return new DiaOption("PPTE2_OrganRequestDecline".Translate())
                {
                    action = Decline,
                    resolveTree = true
                };
                yield return Option_Postpone;
                yield return Option_JumpToLocationAndPostpone;
            }
        }

        public override void Removed()
        {
            base.Removed();
            if (resolved) return;
            resolved = true;
            OrganSaleRequests.Cooldown(prisoner);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref prisoner, "prisoner");
            Scribe_BodyParts.Look(ref part, "part");
            Scribe_Values.Look(ref resolved, "resolved", false);
        }
    }

    /// <summary>The saved concrete bill type distinguishes approved requests from manual sales.</summary>
    public class Bill_OrganSale : Bill_Medical
    {
        public Bill_OrganSale() { }
        public Bill_OrganSale(RecipeDef recipe) : base(recipe, null) { }

        public override bool CompletableEver => OrganSaleRequests.Eligible(GiverPawn) && base.CompletableEver;
        public override bool ShouldDoNow() => base.ShouldDoNow() && CompletableEver;
    }
}
