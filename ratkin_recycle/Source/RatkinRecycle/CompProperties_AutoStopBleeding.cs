using UnityEngine;
using Verse;

namespace RatkinRecycle;

public class CompProperties_AutoStopBleeding : CompProperties
{
	public CompProperties_AutoStopBleeding()
	{
		compClass = typeof(CompAutoStopBleeding);
	}

	public override void DrawGhost(IntVec3 center, Rot4 rot, ThingDef thingDef, Color ghostCol, AltitudeLayer drawAltitude, Thing thing = null)
	{
		CompAutoStopBleeding.DrawRange(center);
	}
}
