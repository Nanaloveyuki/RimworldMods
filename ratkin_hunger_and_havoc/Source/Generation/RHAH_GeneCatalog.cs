using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Generation
{
    internal sealed class RHAH_XenotypeRegistration
    {
        internal RHAH_XenotypeRegistration(string defName, float suggestedWeight, bool builtin)
        {
            DefName = defName;
            SuggestedWeight = RHAH_XenotypeWeightTable.Clamp(suggestedWeight);
            Builtin = builtin;
        }

        internal string DefName { get; }
        internal float SuggestedWeight { get; }
        internal bool Builtin { get; }
    }

    internal static class RHAH_GeneCatalog
    {
        internal const string DefaultXenotypeDefName = "RK_XenoType_Ratkin";
        internal const string FallbackXenotypeDefName = "RHAH_Xenotype_Ratkin";
        internal const string GenePrefix = "RHAH_";

        static readonly List<RHAH_XenotypeRegistration> registrations = new List<RHAH_XenotypeRegistration>();

        static RHAH_GeneCatalog()
        {
            RegisterXenotype(DefaultXenotypeDefName, RHAH_XenotypeWeightTable.DefaultBuiltinWeight, true);
            RegisterXenotype(FallbackXenotypeDefName, RHAH_XenotypeWeightTable.DefaultExternalWeight, false);
        }

        internal static void RegisterXenotype(string defName, float suggestedWeight, bool builtin)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return;
            }

            for (int i = 0; i < registrations.Count; i++)
            {
                if (string.Equals(registrations[i].DefName, defName, StringComparison.OrdinalIgnoreCase))
                {
                    if (!builtin && string.Equals(registrations[i].DefName, DefaultXenotypeDefName, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    registrations[i] = new RHAH_XenotypeRegistration(registrations[i].DefName, suggestedWeight, builtin);
                    return;
                }
            }

            registrations.Add(new RHAH_XenotypeRegistration(defName, suggestedWeight, builtin));
        }

        internal static void ResetForTests()
        {
            registrations.Clear();
            RegisterXenotype(DefaultXenotypeDefName, RHAH_XenotypeWeightTable.DefaultBuiltinWeight, true);
            RegisterXenotype(FallbackXenotypeDefName, RHAH_XenotypeWeightTable.DefaultExternalWeight, false);
        }

        internal static IReadOnlyList<RHAH_XenotypeRegistration> Registrations => registrations;

        internal static bool IsOwnedGene(GeneDef gene)
        {
            return gene != null &&
                !string.IsNullOrEmpty(gene.defName) &&
                gene.defName.StartsWith(GenePrefix, StringComparison.Ordinal);
        }

        internal static bool IsRegistered(string defName)
        {
            return Find(defName) != null;
        }

        internal static float SuggestedWeight(string defName)
        {
            RHAH_XenotypeRegistration registration = Find(defName);
            return registration == null
                ? RHAH_XenotypeWeightTable.DefaultExternalWeight
                : registration.SuggestedWeight;
        }

        internal static bool IsBuiltin(string defName)
        {
            RHAH_XenotypeRegistration registration = Find(defName);
            return registration != null && registration.Builtin;
        }

        static RHAH_XenotypeRegistration Find(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return null;
            }

            for (int i = 0; i < registrations.Count; i++)
            {
                if (string.Equals(registrations[i].DefName, defName, StringComparison.OrdinalIgnoreCase))
                {
                    return registrations[i];
                }
            }

            return null;
        }
    }
}
