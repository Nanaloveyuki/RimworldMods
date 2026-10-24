using System;
using System.Collections.Generic;
using Verse;

namespace RatkinRecycle;

public partial class RatkinRecycleSettings : ModSettings
{
	public bool enabled = true;

	public bool showLetter = true;

	public bool showStopLetter = true;

	public bool babyMoodNeed = true;

	public bool debugLog;

	public RatkinRecycleStatus eaterStatuses = RatkinRecycleStatus.Default;

	public RatkinRecycleStatus victimStatuses = RatkinRecycleStatus.Default;

	public List<string> allowedBodyParts = RatkinRecyclePolicy.DefaultBodyParts();

	private HashSet<string> bodyPartMembership;

	public string raceKeywords = "Ratkin";

	public float momHungerPct = 0.5f;

	public float otherHungerPct = 0.3f;

	public bool useMalnutritionTrigger;

	public float momMalnutritionSeverity = 0.2f;

	public float otherMalnutritionSeverity = 0.4f;

	public bool preferAvailableFood = true;

	public bool manualEnabled = true;

	public float manualMinAge = 4f;

	public bool manualRequireHunger;

	public float adultAgeYears = 1f;

	public float postpartumHours = 120f;

	public bool momOwnEggFallback = true;

	public int fineMealMode;

	public float fineMealHours = 48f;

	public float maxEggAge = 3f;

	public float splitEggAge = 1f;

	public float minEaterAge = 1f;

	public float maxEaterAge = 100f;

	public bool eaterAgeUnlimited = true;

	public float minVictimAge;

	public float maxVictimAge = 3f;

	public bool victimAgeUnlimited;

	public bool limitYoungPredators = true;

	public float searchRadius = 40f;

	public bool skipDrafted = true;

	public bool skipMentalState = true;

	public int checkInterval = 120;

	public int retryCooldown = 900;

	public int biteInterval = 90;

	public float biteDamage = 4f;

	public float nutritionPerBite = 0.05f;

	public float stopAtFullness = 0.7f;

	public float nutritionOnFinish = 0.4f;

	public bool limbsFirst = true;

	public bool destroyCorpse = true;

	public bool leaveMeat;

	public int meatCount = 2;

	public float tierToddlerMax = 1f;

	public float tierChildMax = 3f;

	public float momNonOwnToddler = 20f;

	public float momNonOwnChild = 20f;

	public float momNonOwnAdult = 20f;

	public float momOwnToddler = -20f;

	public float momOwnChild = -20f;

	public float momOwnAdult = 15f;

	public float otherToddler = 10f;

	public float otherChild = 10f;

	public float otherAdult = 10f;

	public float eatenToddler = -50f;

	public float eatenChild = -50f;

	public float eatenAdult = -50f;

	public bool bleedStationEnabled = true;

	public float bleedStationRadius = 12f;

	public int bleedStationInterval = 120;

	public float bleedStationHeal = 0.03f;

	public bool bleedStationHealFriendly = true;

	public bool bleedStationHealHostile;

	public bool bleedStationHealWild = true;

	public bool bleedStationHealAnimals = true;

	public override void ExposeData()
	{
		Scribe_Values.Look(ref enabled, "enabled", defaultValue: true);
		Scribe_Values.Look(ref showLetter, "showLetter", defaultValue: true);
		Scribe_Values.Look(ref showStopLetter, "showStopLetter", defaultValue: true);
		Scribe_Values.Look(ref babyMoodNeed, "babyMoodNeed", defaultValue: true);
		Scribe_Values.Look(ref debugLog, "debugLog", defaultValue: false);
		Scribe_Values.Look(ref eaterStatuses, "eaterStatuses", RatkinRecycleStatus.Default);
		Scribe_Values.Look(ref victimStatuses, "victimStatuses", RatkinRecycleStatus.Default);
		Scribe_Collections.Look(ref allowedBodyParts, "allowedBodyParts", LookMode.Value);
		Scribe_Values.Look(ref raceKeywords, "raceKeywords", "Ratkin");
		Scribe_Values.Look(ref momHungerPct, "momHungerPct", 0.5f);
		Scribe_Values.Look(ref otherHungerPct, "otherHungerPct", 0.3f);
		Scribe_Values.Look(ref useMalnutritionTrigger, "useMalnutritionTrigger", defaultValue: false);
		Scribe_Values.Look(ref momMalnutritionSeverity, "momMalnutritionSeverity", 0.2f);
		Scribe_Values.Look(ref otherMalnutritionSeverity, "otherMalnutritionSeverity", 0.4f);
		Scribe_Values.Look(ref preferAvailableFood, "preferAvailableFood", defaultValue: true);
		Scribe_Values.Look(ref manualEnabled, "manualEnabled", defaultValue: true);
		Scribe_Values.Look(ref manualMinAge, "manualMinAge", 4f);
		Scribe_Values.Look(ref manualRequireHunger, "manualRequireHunger", defaultValue: false);
		Scribe_Values.Look(ref adultAgeYears, "adultAgeYears", 1f);
		Scribe_Values.Look(ref postpartumHours, "postpartumHours", 120f);
		Scribe_Values.Look(ref momOwnEggFallback, "momOwnEggFallback", defaultValue: true);
		Scribe_Values.Look(ref fineMealMode, "fineMealMode", 0);
		Scribe_Values.Look(ref fineMealHours, "fineMealHours", 48f);
		Scribe_Values.Look(ref maxEggAge, "maxEggAge", 3f);
		Scribe_Values.Look(ref splitEggAge, "splitEggAge", 1f);
		Scribe_Values.Look(ref minEaterAge, "minEaterAge", 1f);
		Scribe_Values.Look(ref maxEaterAge, "maxEaterAge", 100f);
		Scribe_Values.Look(ref eaterAgeUnlimited, "eaterAgeUnlimited", true);
		Scribe_Values.Look(ref minVictimAge, "minVictimAge", 0f);
		Scribe_Values.Look(ref maxVictimAge, "maxVictimAge", 3f);
		Scribe_Values.Look(ref victimAgeUnlimited, "victimAgeUnlimited", false);
		Scribe_Values.Look(ref limitYoungPredators, "limitYoungPredators", true);
		Scribe_Values.Look(ref searchRadius, "searchRadius", 40f);
		Scribe_Values.Look(ref skipDrafted, "skipDrafted", defaultValue: true);
		Scribe_Values.Look(ref skipMentalState, "skipMentalState", defaultValue: true);
		Scribe_Values.Look(ref checkInterval, "checkInterval", 120);
		Scribe_Values.Look(ref retryCooldown, "retryCooldown", 900);
		Scribe_Values.Look(ref biteInterval, "biteInterval", 90);
		Scribe_Values.Look(ref biteDamage, "biteDamage", 4f);
		Scribe_Values.Look(ref nutritionPerBite, "nutritionPerBite", 0.05f);
		Scribe_Values.Look(ref stopAtFullness, "stopAtFullness", 0.7f);
		Scribe_Values.Look(ref nutritionOnFinish, "nutritionOnFinish", 0.4f);
		Scribe_Values.Look(ref limbsFirst, "limbsFirst", defaultValue: true);
		Scribe_Values.Look(ref destroyCorpse, "destroyCorpse", defaultValue: true);
		Scribe_Values.Look(ref leaveMeat, "leaveMeat", defaultValue: false);
		Scribe_Values.Look(ref meatCount, "meatCount", 2);
		Scribe_Values.Look(ref tierToddlerMax, "tierToddlerMax", 1f);
		Scribe_Values.Look(ref tierChildMax, "tierChildMax", 3f);
		Scribe_Values.Look(ref momNonOwnToddler, "momNonOwnToddler", 20f);
		Scribe_Values.Look(ref momNonOwnChild, "momNonOwnChild", 20f);
		Scribe_Values.Look(ref momNonOwnAdult, "momNonOwnAdult", 20f);
		Scribe_Values.Look(ref momOwnToddler, "momOwnToddler", -20f);
		Scribe_Values.Look(ref momOwnChild, "momOwnChild", -20f);
		Scribe_Values.Look(ref momOwnAdult, "momOwnAdult", 15f);
		Scribe_Values.Look(ref otherToddler, "otherToddler", 10f);
		Scribe_Values.Look(ref otherChild, "otherChild", 10f);
		Scribe_Values.Look(ref otherAdult, "otherAdult", 10f);
		Scribe_Values.Look(ref eatenToddler, "eatenToddler", -50f);
		Scribe_Values.Look(ref eatenChild, "eatenChild", -50f);
		Scribe_Values.Look(ref eatenAdult, "eatenAdult", -50f);
		Scribe_Values.Look(ref bleedStationEnabled, "bleedStationEnabled", defaultValue: true);
		Scribe_Values.Look(ref bleedStationRadius, "bleedStationRadius", 12f);
		Scribe_Values.Look(ref bleedStationInterval, "bleedStationInterval", 120);
		Scribe_Values.Look(ref bleedStationHeal, "bleedStationHeal", 0.03f);
		Scribe_Values.Look(ref bleedStationHealFriendly, "bleedStationHealFriendly", defaultValue: true);
		Scribe_Values.Look(ref bleedStationHealHostile, "bleedStationHealHostile", defaultValue: false);
		Scribe_Values.Look(ref bleedStationHealWild, "bleedStationHealWild", defaultValue: true);
		Scribe_Values.Look(ref bleedStationHealAnimals, "bleedStationHealAnimals", defaultValue: true);
		if (Scribe.mode == LoadSaveMode.PostLoadInit)
		{
			Normalize();
			bodyPartMembership = null;
			ClearEditBuffers();
			RatkinRecyclePolicy.InvalidateBodyParts();
		}
	}

	public void ResetDefaults()
	{
		enabled = true;
		showLetter = true;
		showStopLetter = true;
		babyMoodNeed = true;
		debugLog = false;
		eaterStatuses = victimStatuses = RatkinRecycleStatus.Default;
		allowedBodyParts = RatkinRecyclePolicy.DefaultBodyParts();
		bodyPartMembership = null;
		raceKeywords = "Ratkin";
		momHungerPct = 0.5f;
		otherHungerPct = 0.3f;
		useMalnutritionTrigger = false;
		momMalnutritionSeverity = 0.2f;
		otherMalnutritionSeverity = 0.4f;
		preferAvailableFood = true;
		manualEnabled = true;
		manualMinAge = 4f;
		manualRequireHunger = false;
		adultAgeYears = 1f;
		postpartumHours = 120f;
		momOwnEggFallback = true;
		fineMealMode = 0;
		fineMealHours = 48f;
		maxEggAge = 3f;
		splitEggAge = 1f;
		minEaterAge = 1f;
		maxEaterAge = 100f;
		eaterAgeUnlimited = true;
		minVictimAge = 0f;
		maxVictimAge = 3f;
		victimAgeUnlimited = false;
		limitYoungPredators = true;
		searchRadius = 40f;
		skipDrafted = true;
		skipMentalState = true;
		checkInterval = 120;
		retryCooldown = 900;
		biteInterval = 90;
		biteDamage = 4f;
		nutritionPerBite = 0.05f;
		stopAtFullness = 0.7f;
		nutritionOnFinish = 0.4f;
		limbsFirst = true;
		destroyCorpse = true;
		leaveMeat = false;
		meatCount = 2;
		tierToddlerMax = 1f;
		tierChildMax = 3f;
		momNonOwnToddler = (momNonOwnChild = (momNonOwnAdult = 20f));
		momOwnToddler = -20f;
		momOwnChild = -20f;
		momOwnAdult = 15f;
		otherToddler = (otherChild = (otherAdult = 10f));
		eatenToddler = (eatenChild = (eatenAdult = -50f));
		bleedStationEnabled = true;
		bleedStationRadius = 12f;
		bleedStationInterval = 120;
		bleedStationHeal = 0.03f;
		bleedStationHealFriendly = true;
		bleedStationHealHostile = false;
		bleedStationHealWild = true;
		bleedStationHealAnimals = true;
		ClearEditBuffers();
		RatkinRecyclePolicy.InvalidateBodyParts();
	}

	public bool BodyPartAllowed(string defName)
	{
		bodyPartMembership ??= new HashSet<string>(allowedBodyParts ??= RatkinRecyclePolicy.DefaultBodyParts(), StringComparer.Ordinal);
		return defName != null && bodyPartMembership.Contains(defName);
	}

	public void SetBodyPartAllowed(string defName, bool allowed)
	{
		if (string.IsNullOrEmpty(defName)) return;
		bool current = BodyPartAllowed(defName);
		if (current == allowed) return;
		if (allowed)
		{
			allowedBodyParts.Add(defName);
			bodyPartMembership.Add(defName);
		}
		else
		{
			allowedBodyParts.RemoveAll(name => name == defName);
			bodyPartMembership.Remove(defName);
		}
	}

	public void Normalize()
	{
		raceKeywords ??= "Ratkin";
		eaterStatuses &= RatkinRecycleStatus.All;
		victimStatuses &= RatkinRecycleStatus.All;
		allowedBodyParts ??= RatkinRecyclePolicy.DefaultBodyParts();
		var seen = new HashSet<string>(StringComparer.Ordinal);
		allowedBodyParts.RemoveAll(name => string.IsNullOrEmpty(name) || !seen.Add(name));
		bodyPartMembership = seen;
		minEaterAge = Nonnegative(minEaterAge, 1f);
		maxEaterAge = Nonnegative(maxEaterAge, 100f);
		minVictimAge = Nonnegative(minVictimAge, 0f);
		maxVictimAge = Nonnegative(maxVictimAge, 3f);
		manualMinAge = Nonnegative(manualMinAge, 4f);
		if (!eaterAgeUnlimited && maxEaterAge < minEaterAge) maxEaterAge = minEaterAge;
		if (!victimAgeUnlimited && maxVictimAge < minVictimAge) maxVictimAge = minVictimAge;
		adultAgeYears = Nonnegative(adultAgeYears, 1f);
		maxEggAge = Nonnegative(maxEggAge, 3f);
		splitEggAge = Nonnegative(splitEggAge, 1f);
		tierToddlerMax = Nonnegative(tierToddlerMax, 1f);
		tierChildMax = Math.Max(tierToddlerMax, Nonnegative(tierChildMax, 3f));
		momHungerPct = Math.Clamp(Finite(momHungerPct, 0.5f), 0f, 1f);
		otherHungerPct = Math.Clamp(Finite(otherHungerPct, 0.3f), 0f, 1f);
		momMalnutritionSeverity = Math.Clamp(Finite(momMalnutritionSeverity, 0.2f), 0f, 1f);
		otherMalnutritionSeverity = Math.Clamp(Finite(otherMalnutritionSeverity, 0.4f), 0f, 1f);
		stopAtFullness = Math.Clamp(Finite(stopAtFullness, 0.7f), 0f, 1f);
		postpartumHours = Nonnegative(postpartumHours, 120f);
		fineMealHours = Nonnegative(fineMealHours, 48f);
		fineMealMode = Math.Clamp(fineMealMode, 0, 1);
		searchRadius = Nonnegative(searchRadius, 40f);
		checkInterval = Math.Max(10, checkInterval);
		retryCooldown = Math.Max(1, retryCooldown);
		biteInterval = Math.Max(10, biteInterval);
		biteDamage = Nonnegative(biteDamage, 4f);
		nutritionPerBite = Nonnegative(nutritionPerBite, 0.05f);
		nutritionOnFinish = Nonnegative(nutritionOnFinish, 0.4f);
		meatCount = Math.Max(0, meatCount);
		momNonOwnToddler = Finite(momNonOwnToddler, 20f);
		momNonOwnChild = Finite(momNonOwnChild, 20f);
		momNonOwnAdult = Finite(momNonOwnAdult, 20f);
		momOwnToddler = Finite(momOwnToddler, -20f);
		momOwnChild = Finite(momOwnChild, -20f);
		momOwnAdult = Finite(momOwnAdult, 15f);
		otherToddler = Finite(otherToddler, 10f);
		otherChild = Finite(otherChild, 10f);
		otherAdult = Finite(otherAdult, 10f);
		eatenToddler = Finite(eatenToddler, -50f);
		eatenChild = Finite(eatenChild, -50f);
		eatenAdult = Finite(eatenAdult, -50f);
		bleedStationRadius = CompAutoStopBleeding.EffectiveRadius(Finite(bleedStationRadius, 12f));
		bleedStationInterval = Math.Max(1, bleedStationInterval);
		bleedStationHeal = Nonnegative(bleedStationHeal, 0.03f);
	}

	private static float Finite(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;

	private static float Nonnegative(float value, float fallback) => Math.Max(0f, Finite(value, fallback));
}
