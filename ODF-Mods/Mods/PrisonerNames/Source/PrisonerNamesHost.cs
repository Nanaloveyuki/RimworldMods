using System;
using Oxygendsl.Mod;

namespace Odf.PrisonerNames
{
    static class PrisonerNamesHost
    {
        internal const string PackageId = "odf.nanaloveyuki.prisonernames";
        internal static void Register()
        {
            OxygenEditorHost.RegisterDocuments(PackageId, PrisonerEditorDocuments.Current);
        }

        internal static void Subscribe()
        {
            if (subscription != null) return;
            subscription = OxygenEvents.Subscribe(PackageId, message =>
            {
                GameComponent_PrisonerNames runtime = GameComponent_PrisonerNames.Current;
                if (runtime != null) runtime.Handle(message);
            });
        }

        static IDisposable subscription;
    }
}
