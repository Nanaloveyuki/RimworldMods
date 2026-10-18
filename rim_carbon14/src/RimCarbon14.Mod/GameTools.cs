using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using RimCarbon14.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimCarbon14.Mod
{
    public static class GameTools
    {
        public static void Register(BridgeHost h)
        {
            h.AddRead("game_state", "Current real game, maps, selected objects and loaded mod versions.", _ => State(h));
            h.AddRead("objects_roots", "Obtain session-scoped handles to game, map, selected things and loaded Mod instances.", _ => new JObject
            {
                ["game"] = h.Handles.Describe(Current.Game), ["map"] = h.Handles.Describe(Find.CurrentMap),
                ["selection"] = h.Ready ? new JArray(Find.Selector.SelectedObjects.Select(h.Handles.Describe)) : new JArray(),
                ["mods"] = new JArray(LoadedModManager.ModHandles.Select(h.Handles.Describe))
            });
            h.Add("pawn_list", "List map pawns; detail is obtained separately by pawn_get.", "read", true, (a, _) => ReflectionTools.Page(Find.Maps.SelectMany(m => m.mapPawns.AllPawnsSpawned).Where(p => a["mapId"] == null || p.Map.uniqueID == (int)a["mapId"]!).Select(p => Summary(h, p)), a), "mapId:integer", "offset:integer", "limit:integer");
            h.Add("pawn_get", "Read health, needs, skills, equipment and live Job/path state.", "read", true, (a, _) => PawnDetail(h, Pawn(h, a)), "handle!");
            h.Add("pawn_select", "Select and optionally focus a spawned pawn through the game selector.", "write", true, (a, _) =>
            {
                var p = Pawn(h, a); Current.Game.CurrentMap = p.Map; Find.Selector.ClearSelection(); Find.Selector.Select(p);
                if ((bool?)a["focus"] ?? true) CameraJumper.TryJump(p);
                return Summary(h, p);
            }, "handle!", "focus:boolean");
            h.Add("pawn_draft", "Set the pawn's game draft controller; requires a player-controlled pawn.", "write", true, (a, _) =>
            {
                var p = Pawn(h, a);
                if (p.drafter == null || !p.IsColonistPlayerControlled) throw new BridgeException("cannot_draft", "Pawn has no controllable draft controller.");
                p.drafter.Drafted = (bool?)a["drafted"] ?? true;
                return Summary(h, p);
            }, "handle!", "drafted!:boolean");
            h.Add("pawn_move", "Order a drafted pawn to a reachable cell using the vanilla drafted move action. Acceptance is not arrival.", "write", true, (a, _) =>
            {
                var p = Pawn(h, a); var cell = Cell(a);
                if (!p.Drafted || !p.IsColonistPlayerControlled || !cell.InBounds(p.Map) || !cell.Standable(p.Map)) throw new BridgeException("cannot_move", "A controllable drafted pawn and standable map cell are required.");
                var report = FloatMenuOptionProvider_DraftedMove.PawnCanGoto(p, cell);
                if (!report.Accepted) throw new BridgeException("cannot_move", report.Reason);
                FloatMenuOptionProvider_DraftedMove.PawnGotoAction(cell, p, cell);
                return new JObject { ["accepted"] = true, ["pawn"] = Summary(h, p) };
            }, "handle!", "x!:integer", "z!:integer");
            h.Add("pawn_job", "Explicitly submit an ordered JobDef or cancel the current job; no rollback.", "write", true, (a, _) =>
            {
                var p = Pawn(h, a);
                if ((bool?)a["cancel"] ?? false) { p.jobs.EndCurrentJob(JobCondition.InterruptForced); return new JObject { ["cancelled"] = true }; }
                var def = DefDatabase<JobDef>.GetNamed(Wire.Required(a, "def"));
                var job = a["targetHandle"] != null ? JobMaker.MakeJob(def, (Thing)h.Handles.Get((string)a["targetHandle"]!)) : JobMaker.MakeJob(def, Cell(a));
                return new JObject { ["accepted"] = p.jobs.TryTakeOrderedJob(job) };
            }, "handle!", "cancel:boolean", "def", "targetHandle", "x:integer", "z:integer");
            h.Add("game_pause", "Pause or resume simulation without stopping bridge/UI frames. Waits for the load fade to release player controls.", "write", true, (a, _) =>
            {
                if (!Current.Game.PlayerHasControl) return null;
                SetSpeed(((bool?)a["paused"] ?? true) ? TimeSpeed.Paused : TimeSpeed.Normal);
                return State(h);
            }, "paused:boolean");
            h.Add("game_step", "Run exactly N DoSingleTick calls, one per frame, ending paused. Reports actual Tick delta.", "write", true, (a, op) => Step(h, a, op), "ticks!:integer");
            h.Add("wait_until", "Wait without blocking UI or mutations until a read-only tool snapshot matches a JSON path value; call timeout bounds the wait.", "read", false, (a, op) => Wait(h, a, op, false), "tool!", "arguments:object", "path!", "equals!:any");
            h.Add("game_run_until", "Advance one paused simulation tick per frame until a read-only snapshot matches, or maxTicks is reached. Inspect matched in the result.", "write", true, (a, op) => Wait(h, a, op, true), "tool!", "arguments:object", "path!", "equals!:any", "maxTicks!:integer");
            h.Add("game_load", "Load a named save from this profile and wait for an idle playable map. Old object handles expire.", "write", false, (a, op) =>
            {
                if (op.Context == null)
                {
                    if (LongEventHandler.AnyEventNowOrWaiting) return null;
                    var name = SaveName(a); if (!File.Exists(GenFilePaths.FilePathForSavedGame(name))) throw new BridgeException("save_not_found", name);
                    op.Context = Current.Game ?? (object)"entry";
                    GameDataSaveLoader.LoadGame(name); return null;
                }
                if (!h.Ready || !Current.Game.PlayerHasControl || ReferenceEquals(op.Context, Current.Game)) return null;
                SetSpeed(TimeSpeed.Paused);
                return State(h);
            }, "name!");
            h.Add("game_new_test", "Create a vanilla quick-test colony from the main menu and pause when ready. Intended to create isolated test fixtures.", "write", false, (_, op) =>
            {
                if (op.Context == null)
                {
                    if (LongEventHandler.AnyEventNowOrWaiting) return null;
                    if (Current.Game != null || Current.ProgramState != ProgramState.Entry) throw new BridgeException("not_main_menu", "Return to the main menu before creating a test colony.");
                    op.Context = true;
                    LongEventHandler.QueueLongEvent(() => { Root_Play.SetupForQuickTestPlay(); PageUtility.InitGameStart(); }, "GeneratingMap", true, GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);
                    return null;
                }
                if (!h.Ready || !Current.Game.PlayerHasControl) return null;
                SetSpeed(TimeSpeed.Paused);
                return State(h);
            });
            h.Add("game_save", "Save under a new name in this profile; existing saves are not overwritten.", "write", true, (a, _) =>
            {
                var name = SaveName(a); var path = GenFilePaths.FilePathForSavedGame(name);
                if (File.Exists(path)) throw new BridgeException("save_exists", "Choose a new save name.");
                GameDataSaveLoader.SaveGame(name);
                if (!File.Exists(path) || new FileInfo(path).Length == 0) throw new BridgeException("save_failed", "Game did not produce a save. Inspect game logs.");
                return new JObject { ["name"] = name, ["path"] = path, ["bytes"] = new FileInfo(path).Length };
            }, "name!");
            h.Add("game_exit", "Request normal game process shutdown without saving.", "write", false, (_, __) => { h.StartCoroutine(ExitNextFrame()); return new JObject { ["requested"] = true }; });
        }
        private static System.Collections.IEnumerator ExitNextFrame() { yield return null; yield return null; Root.Shutdown(); }
        private sealed class TickRun { public int Remaining; public int Start; public int LastFrame = -1; public string Session = ""; }
        private sealed class ConditionRun { public int Calls; public int StartTick; public int LastFrame = -1; }
        private static JToken? Wait(BridgeHost h, JObject args, Operation op, bool advance)
        {
            if (advance && !Current.Game.PlayerHasControl) return null;
            var maximum = (int?)args["maxTicks"] ?? 0;
            if (advance && (maximum < 1 || maximum > 60000)) throw new BridgeException("invalid_ticks", "maxTicks must be 1..60000.");
            if (op.Context == null) op.Context = new ConditionRun { StartTick = Current.Game?.tickManager?.TicksGame ?? 0 };
            var run = (ConditionRun)op.Context;
            if (run.LastFrame == Time.frameCount) return null;
            run.LastFrame = Time.frameCount;
            if (advance) SetSpeed(TimeSpeed.Paused);
            var snapshot = h.Probe(Wire.Required(args, "tool"), args["arguments"] as JObject ?? new JObject());
            var selected = snapshot.SelectToken(Wire.Required(args, "path"));
            var matched = selected != null && JToken.DeepEquals(selected, args["equals"]);
            if (matched || (advance && run.Calls >= maximum)) return new JObject { ["matched"] = matched, ["calls"] = run.Calls, ["startTick"] = run.StartTick, ["endTick"] = Current.Game?.tickManager?.TicksGame, ["snapshot"] = snapshot };
            if (advance) { Find.TickManager.DoSingleTick(); run.Calls++; }
            return null;
        }
        private static JToken? Step(BridgeHost h, JObject a, Operation op)
        {
            if (!Current.Game.PlayerHasControl && op.Context == null) return null;
            if (op.Context == null)
            {
                var count = (int?)a["ticks"] ?? 1;
                if (count < 1 || count > 60000) throw new BridgeException("invalid_ticks", "ticks must be 1..60000.");
                SetSpeed(TimeSpeed.Paused);
                op.Context = new TickRun { Remaining = count, Start = Find.TickManager.TicksGame, Session = h.Session };
            }
            var run = (TickRun)op.Context;
            if (run.Session != h.Session) throw new BridgeException("stale_session", "Session changed during tick stepping.");
            if (run.LastFrame == Time.frameCount) return null;
            run.LastFrame = Time.frameCount;
            SetSpeed(TimeSpeed.Paused);
            Find.TickManager.DoSingleTick();
            run.Remaining--;
            return run.Remaining == 0 ? new JObject { ["calls"] = (int?)a["ticks"] ?? 1, ["startTick"] = run.Start, ["endTick"] = Find.TickManager.TicksGame, ["delta"] = Find.TickManager.TicksGame - run.Start } : null;
        }
        private static void SetSpeed(TimeSpeed speed)
        {
            Find.TickManager.CurTimeSpeed = speed;
            if (Find.TickManager.CurTimeSpeed != speed) throw new BridgeException("time_control_unavailable", "The game rejected the time-speed change; no tick step was executed.");
        }
        private static string SaveName(JObject a)
        {
            var name = Wire.Required(a, "name");
            if (name != Path.GetFileName(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name == "." || name == "..") throw new BridgeException("invalid_save_name", "Use a filename without directories or extension.");
            return name;
        }
        public static IntVec3 Cell(JObject a) => new IntVec3((int?)a["x"] ?? throw new BridgeException("invalid_cell", "x required"), 0, (int?)a["z"] ?? throw new BridgeException("invalid_cell", "z required"));
        public static Pawn Pawn(BridgeHost h, JObject a)
        {
            if (!(h.Handles.Get(Wire.Required(a, "handle")) is Pawn pawn) || !pawn.Spawned || pawn.Destroyed) throw new BridgeException("pawn_unavailable", "A live spawned pawn handle is required.");
            return pawn;
        }
        public static JObject Summary(BridgeHost h, Pawn p) => new JObject { ["handle"] = h.Handles.Add(p), ["id"] = p.ThingID, ["label"] = p.LabelShort, ["mapId"] = p.Map?.uniqueID, ["position"] = Wire.Token(new { p.Position.x, p.Position.z }), ["drafted"] = p.Drafted, ["dead"] = p.Dead, ["downed"] = p.Downed, ["faction"] = p.Faction?.Name, ["job"] = p.CurJobDef?.defName };
        private static JObject PawnDetail(BridgeHost h, Pawn p)
        {
            var data = Summary(h, p);
            data["health"] = Wire.Token(p.health.hediffSet.hediffs.Select(x => new { def = x.def.defName, x.Severity, part = x.Part?.def.defName }));
            data["needs"] = Wire.Token(p.needs?.AllNeeds.Select(n => new { def = n.def.defName, level = n.CurLevel, max = n.MaxLevel }));
            data["skills"] = Wire.Token(p.skills?.skills.Select(s => new { def = s.def.defName, level = s.Level, passion = s.passion.ToString() }));
            data["equipment"] = Wire.Token(p.equipment?.AllEquipmentListForReading.Select(t => new { id = t.ThingID, def = t.def.defName }));
            data["apparel"] = Wire.Token(p.apparel?.WornApparel.Select(t => new { id = t.ThingID, def = t.def.defName }));
            data["jobObject"] = h.Handles.Describe(p.CurJob);
            data["path"] = p.pather == null ? JValue.CreateNull() : Wire.Token(new { p.pather.Moving, destination = new { p.pather.Destination.Cell.x, p.pather.Destination.Cell.z } });
            return data;
        }
        public static JObject State(BridgeHost h) => new JObject
        {
            ["status"] = h.LiveStatus(),
            ["maps"] = h.Ready ? Wire.Token(Find.Maps.Select(m => new { id = m.uniqueID, size = new { m.Size.x, m.Size.z }, pawns = m.mapPawns.AllPawnsSpawned.Count })) : new JArray(),
            ["selected"] = h.Ready ? new JArray(Find.Selector.SelectedObjects.Select(h.Handles.Describe)) : new JArray(),
            ["mods"] = Wire.Token(LoadedModManager.RunningMods.Select(m => new { id = m.PackageId, name = m.Name, root = m.RootDir, assemblies = m.assemblies.loadedAssemblies.Select(a => a.FullName) }))
        };
    }
}
