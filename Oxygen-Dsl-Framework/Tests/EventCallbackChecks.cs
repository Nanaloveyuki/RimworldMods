using System;
using System.Collections.Generic;
using Oxygendsl.Core;
using Oxygendsl.Language;

namespace Oxygendsl.Checks
{
    internal static class EventCallbackChecks
    {
        internal static void Run(Action<bool, string> check)
        {
            var parsed = new ArtiParser().Parse("fn on_event(pawn, event, state): float { return pawn.food + event.amount + state.score + game.tick }");
            var executor = new ArtiExecutor();
            ArtiExecutionResult prepared = executor.Execute(parsed.Program);
            if (prepared.HasErrors) throw new InvalidOperationException("Event callback fixture failed to prepare.");
            var function = (ArtiFunctionDeclarationStatement)parsed.Program.Statements[0];
            var facts = new DictionaryOxygenHostValue(new Dictionary<string, object> { ["amount"] = 2d });
            var state = new DictionaryOxygenHostValue(new Dictionary<string, object> { ["score"] = 3d });
            object first = executor.InvokeIsolated(function, new PawnContext { Food = 0.5 }, new GameContext { Tick = 10 }, facts, state);
            object second = executor.InvokeIsolated(function, new PawnContext { Food = 0.25 }, new GameContext { Tick = 20 }, facts, state);
            check(Convert.ToDouble(first) == 15.5 && Convert.ToDouble(second) == 25.25, "event callback observes current Pawn and game contexts with extra read-only facts");
            try
            {
                executor.InvokeIsolated(function, new PawnContext { Food = 0.5 }, new GameContext(), facts);
                throw new InvalidOperationException("Missing event state was accepted.");
            }
            catch (ScriptEvaluationException) { check(true, "event callback rejects a missing required state argument"); }
        }
    }
}
