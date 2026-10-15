$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'source-tools.ps1')

function Require-Text([string]$text, [string]$needle, [string]$message) {
    if (!$text.Contains($needle)) {
        throw $message
    }
}

$policy = Get-Content (Join-Path $root '1.6/Source/MouseDisasterFactionRelationPolicy.cs') -Raw
$factions = Get-Content (Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.Factions.cs') -Raw
$identity = Get-Content (Join-Path $root '1.6/Source/IdentityLifecyclePatches.cs') -Raw
$relationPatch = Get-Content (Join-Path $root '1.6/Source/FactionRelationPatches.cs') -Raw
$lifecycle = Get-Content (Join-Path $root '1.6/Source/Utilities/MouseDisasterUtility.Lifecycle.cs') -Raw

Require-Text $relationPatch 'IsMouseDisasterManagedFaction' 'RelationWith patch still only covers the hidden faction.'
Require-Text $relationPatch 'TryGetOrRepairManagedFactionRelation' 'RelationWith patch does not reuse managed relation repair.'
Require-Text $relationPatch 'ShouldInterceptRelationLookup' 'RelationWith patch does not use the teardown-safe intercept policy.'
Require-Text $identity 'ShouldSkipIdentityNormalizationDuringFactionTeardown' 'GuestStatus identity hook is not skipped during faction teardown.'
Require-Text $lifecycle 'IsHostileTo(pawn.Faction, Faction.OfPlayer)' 'Identity normalization still calls vanilla HostileTo.'
Require-Text $factions 'IsMouseDisasterManagedFaction(first)' 'Missing-relation repair still ignores event visitor factions.'
Require-Text $factions 'ShouldPersistRepairedRelation' 'Managed relation repair can persist onto a faction already removed from the manager.'
Require-Text $factions 'IsMouseDisasterManagedFaction(faction) && !IsFactionListed(faction)' 'Hostility changes are not ignored for already-removed managed factions.'

$stub = @'
using System;
using RimWorld;
using MouseDisaster;

namespace RimWorld
{
    public enum FactionRelationKind { Neutral, Ally, Hostile }
}

public static class Harness
{
    static int checks;
    static void Check(bool ok, string message)
    {
        checks++;
        if (!ok) throw new Exception(message);
    }

    static void Resolve(
        MouseDisasterManagedFactionKind first, bool firstPlayer, bool firstEnemy,
        MouseDisasterManagedFactionKind second, bool secondPlayer, bool secondEnemy,
        FactionRelationKind expectedKind, int expectedGoodwill, string name)
    {
        MouseDisasterFactionRelationPolicy.ResolveDefaultRelation(
            first, firstPlayer, firstEnemy, second, secondPlayer, secondEnemy,
            out FactionRelationKind kind, out int goodwill);
        Check(kind == expectedKind && goodwill == expectedGoodwill, name + ": " + kind + "/" + goodwill);
    }

    public static int Run()
    {
        Check(!MouseDisasterFactionRelationPolicy.ShouldInterceptRelationLookup(false, true, false, false, false, false, true), "allowNull intercept");
        Check(!MouseDisasterFactionRelationPolicy.ShouldInterceptRelationLookup(false, false, true, false, false, false, true), "repair reentry intercept");
        Check(!MouseDisasterFactionRelationPolicy.ShouldInterceptRelationLookup(true, false, false, false, false, false, true), "worldgen intercept");
        Check(!MouseDisasterFactionRelationPolicy.ShouldInterceptRelationLookup(false, false, false, false, false, false, false), "unmanaged intercept");
        Check(MouseDisasterFactionRelationPolicy.ShouldInterceptRelationLookup(false, false, false, false, false, false, true), "managed intercept");
        Check(!MouseDisasterFactionRelationPolicy.ShouldPersistRepairedRelation(false, true), "unlisted owner persist");
        Check(!MouseDisasterFactionRelationPolicy.ShouldPersistRepairedRelation(true, false), "unlisted other persist");
        Check(MouseDisasterFactionRelationPolicy.ShouldPersistRepairedRelation(true, true), "listed persist");
        Check(MouseDisasterFactionRelationPolicy.ShouldSkipIdentityNormalizationDuringFactionTeardown(true, false), "teardown skip");
        Check(!MouseDisasterFactionRelationPolicy.ShouldSkipIdentityNormalizationDuringFactionTeardown(true, true), "listed identity skip");
        Check(!MouseDisasterFactionRelationPolicy.ShouldSkipIdentityNormalizationDuringFactionTeardown(false, false), "unmanaged unlisted skip");

        Resolve(MouseDisasterManagedFactionKind.NeutralVisitors, false, false, MouseDisasterManagedFactionKind.None, true, false, FactionRelationKind.Neutral, 0, "neutral vs player");
        Resolve(MouseDisasterManagedFactionKind.HostileVisitors, false, true, MouseDisasterManagedFactionKind.None, true, false, FactionRelationKind.Hostile, -100, "hostile vs player");
        Resolve(MouseDisasterManagedFactionKind.FriendlyVisitors, false, false, MouseDisasterManagedFactionKind.None, true, false, FactionRelationKind.Ally, 100, "friendly vs player");
        Resolve(MouseDisasterManagedFactionKind.Hidden, false, false, MouseDisasterManagedFactionKind.None, true, false, FactionRelationKind.Neutral, 20, "hidden vs player");
        Resolve(MouseDisasterManagedFactionKind.HostileVisitors, false, true, MouseDisasterManagedFactionKind.Hidden, false, false, FactionRelationKind.Neutral, 0, "hostile vs hidden");
        Resolve(MouseDisasterManagedFactionKind.NeutralVisitors, false, false, MouseDisasterManagedFactionKind.Hidden, false, false, FactionRelationKind.Neutral, 0, "neutral vs hidden");
        Resolve(MouseDisasterManagedFactionKind.NeutralVisitors, false, false, MouseDisasterManagedFactionKind.None, false, false, FactionRelationKind.Hostile, -100, "neutral vs world");
        return checks;
    }
}
'@

Add-Type -TypeDefinition ($stub + "`n" + [regex]::Replace($policy, '(?m)^using [^;]+;\r?\n', ''))
$checks = [Harness]::Run()
Write-Host "PASS: $checks faction-relation policy assertions; GuestStatus teardown skip and managed RelationWith repair are wired. Not an in-game reproduction."
