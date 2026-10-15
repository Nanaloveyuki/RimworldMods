using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Oxygendsl.Mod;
using Oxygendsl.Mod.Editor;
using Verse;

namespace Odf.PrisonerNames
{
    static class PrisonerEditorDocuments
    {
        internal static IEnumerable<OxygenEditorDocument> Current()
        {
            GameComponent_PrisonerNames runtime = GameComponent_PrisonerNames.Current;
            Game game = Verse.Current.ProgramState == ProgramState.Playing ? Verse.Current.Game : null;
            if (runtime == null || game == null) yield break;
            int gameId = RuntimeHelpers.GetHashCode(game);
            yield return Document(runtime, game, gameId, "naming", "Naming rule", () => runtime.DraftSource, value => runtime.DraftSource = value,
                () => runtime.Apply(runtime.DraftSource));
            yield return Document(runtime, game, gameId, "events", "Event rules", () => runtime.EventDraft, value => runtime.EventDraft = value,
                () => runtime.Apply(runtime.ActiveSource, runtime.EventDraft, false));
        }

        static OxygenEditorDocument Document(GameComponent_PrisonerNames runtime, Game game, int gameId, string kind, string title, Func<string> read, Action<string> write, Func<bool> apply)
        {
            return new OxygenEditorDocument
            {
                Id = PrisonerNamesHost.PackageId + ":runtime:" + gameId + ":" + kind,
                Title = title,
                Path = "current/" + kind,
                Kind = "text",
                IsModule = false,
                Writable = true,
                Read = () => { Require(runtime, game); return read() ?? string.Empty; },
                Write = value => { Require(runtime, game); write(value); },
                Apply = () =>
                {
                    Require(runtime, game);
                    return apply()
                        ? new OxygenEditorActionResult { Success = true, Message = "Applied to this save." }
                        : new OxygenEditorActionResult { Success = false, Message = string.IsNullOrEmpty(runtime.LastError) ? "The draft was not applied." : runtime.LastError };
                },
                Preview = (source, facts) => NamingPreview.Evaluate(source, facts, CurrentPolicy(runtime, game), NamingLanguage.Chinese, OxygenLocalization.Instance)
            };
        }

        static NamePolicy CurrentPolicy(GameComponent_PrisonerNames runtime, Game game)
        {
            return Same(runtime, game) ? runtime.Policy : NamePolicy.CreateDefault();
        }

        static void Require(GameComponent_PrisonerNames runtime, Game game)
        {
            if (!Same(runtime, game)) throw new InvalidOperationException("This save is no longer active.");
        }

        static bool Same(GameComponent_PrisonerNames runtime, Game game)
        {
            return ReferenceEquals(GameComponent_PrisonerNames.Current, runtime) && ReferenceEquals(Verse.Current.Game, game);
        }
    }
}
