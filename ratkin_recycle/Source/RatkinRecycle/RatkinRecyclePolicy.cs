using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RatkinRecycle;

[Flags]
public enum RatkinRecycleStatus
{
	None = 0,
	Prisoner = 1,
	Slave = 2,
	Colonist = 4,
	FriendlyOutsider = 8,
	HostileOutsider = 16,
	Factionless = 32,
	Default = Prisoner | Slave | FriendlyOutsider | HostileOutsider | Factionless,
	All = Default | Colonist
}

public static class RatkinRecyclePolicy
{
	private static readonly List<BodyPartDef> bodyParts = new List<BodyPartDef>();
	private static bool bodyPartsReady;

	public static RatkinRecycleStatus Status(Pawn pawn)
	{
		if (pawn.IsPrisonerOfColony) return RatkinRecycleStatus.Prisoner;
		if (pawn.IsSlaveOfColony) return RatkinRecycleStatus.Slave;
		if (pawn.IsFreeNonSlaveColonist) return RatkinRecycleStatus.Colonist;
		if (pawn.Faction == null) return RatkinRecycleStatus.Factionless;
		return pawn.Faction.HostileTo(Faction.OfPlayer) ? RatkinRecycleStatus.HostileOutsider : RatkinRecycleStatus.FriendlyOutsider;
	}

	public static bool AgeAllowed(Pawn pawn, float minimum, float maximum, bool unlimited)
	{
		if (pawn?.ageTracker == null) return false;
		float years = pawn.ageTracker.AgeBiologicalYearsFloat;
		return years >= minimum && (unlimited || years < maximum);
	}

	public static List<string> DefaultBodyParts()
	{
		return new List<string>
		{
			"Torso", "Neck", "Head", "Skull", "Brain", "Eye", "Ear", "Nose", "Jaw", "Tongue",
			"Shoulder", "Clavicle", "Arm", "Humerus", "Radius", "Hand", "Finger", "Thumb",
			"Sternum", "Ribcage", "Spine", "Pelvis", "Waist", "Heart", "Lung", "Kidney", "Liver", "Stomach",
			"Leg", "Femur", "Tibia", "Foot", "Toe", "RK_BodyPart_Tail"
		};
	}

	public static IReadOnlyList<BodyPartDef> BodyParts
	{
		get
		{
			if (!bodyPartsReady)
			{
				bodyParts.Clear();
				var seen = new HashSet<BodyPartDef>();
				foreach (ThingDef race in DefDatabase<ThingDef>.AllDefsListForReading)
				{
					if (race.race?.Humanlike != true || race.race.body == null || !RaceMatches(race.defName)) continue;
					foreach (BodyPartRecord part in race.race.body.AllParts)
						if (seen.Add(part.def)) bodyParts.Add(part.def);
				}
				bodyParts.Sort((a, b) => string.Compare(a.defName, b.defName, StringComparison.Ordinal));
				bodyPartsReady = true;
			}
			return bodyParts;
		}
	}

	private static bool RaceMatches(string name)
	{
		string keywords = RatkinRecycleMod.Settings?.raceKeywords ?? "Ratkin";
		foreach (string keyword in keywords.Split(new[] { ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries))
			if (name.IndexOf(keyword.Trim(), StringComparison.OrdinalIgnoreCase) >= 0 && keyword.Trim().Length > 0) return true;
		return false;
	}

	public static void InvalidateBodyParts() => bodyPartsReady = false;
}
