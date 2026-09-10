using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using RimCarbon14.Core;
using UnityEngine;
using Verse;

namespace RimCarbon14.Mod
{
    public sealed class CaptureTools
    {
        private readonly BridgeHost host;
        public CaptureTools(BridgeHost host) { this.host = host; }
        private sealed class Capture { public bool Done; public JToken? Result; public Exception? Error; public bool Cancelled; }
        public void Register() => host.Add("capture_screen", "Capture the next rendered frame, optionally crop in top-left physical pixels or to a UI element. Returns downloadable PNG artifact.", "capture", false, Begin, "elementId", "layoutVersion:integer", "rect:object");
        private JToken? Begin(JObject args, Operation op)
        {
            if (op.Context == null)
            {
                var request = new Capture(); op.Context = request;
                var prior = op.Cleanup; op.Cleanup = () => { request.Cancelled = true; prior?.Invoke(); };
                host.StartCoroutine(CaptureAtFrameEnd(request, args)); return null;
            }
            var capture = (Capture)op.Context;
            if (!capture.Done) return null;
            if (capture.Error != null) throw capture.Error;
            return capture.Result;
        }
        private IEnumerator CaptureAtFrameEnd(Capture request, JObject args)
        {
            yield return new WaitForEndOfFrame();
            if (request.Cancelled) yield break;
            Texture2D? full = null; Texture2D? crop = null;
            try
            {
                host.Ui.FinalizeFrame();
                if (!host.Ui.HasCurrentRepaint) throw new BridgeException("no_render_frame", "Game UI is not repainting. Keep the game window rendered, not hidden or minimized.");
                var files = new DirectoryInfo(host.ArtifactDirectory).GetFiles("*.png");
                if (files.Sum(f => f.Length) > 512L * 1024 * 1024) throw new BridgeException("artifact_limit", "Artifact storage exceeds 512 MiB; archive and remove old captures.");
                full = ScreenCapture.CaptureScreenshotAsTexture();
                if (full == null || full.width < 1 || full.height < 1) throw new BridgeException("no_render_frame", "No screenshot texture was available.");
                var rect = new Rect(0, 0, full.width, full.height);
                if (args["elementId"] != null) rect = host.Ui.ResolveRect(Wire.Required(args, "elementId"), (int?)args["layoutVersion"] ?? -1);
                else if (args["rect"] is JObject r) rect = new Rect((float?)r["x"] ?? 0, (float?)r["y"] ?? 0, (float?)r["width"] ?? 0, (float?)r["height"] ?? 0);
                int x = Mathf.Clamp(Mathf.FloorToInt(rect.x), 0, full.width), top = Mathf.Clamp(Mathf.FloorToInt(rect.y), 0, full.height);
                int width = Mathf.Clamp(Mathf.CeilToInt(rect.xMax) - x, 0, full.width - x), height = Mathf.Clamp(Mathf.CeilToInt(rect.yMax) - top, 0, full.height - top);
                if (width == 0 || height == 0) throw new BridgeException("empty_crop", "Crop lies outside the screenshot.");
                var output = full;
                if (width != full.width || height != full.height)
                {
                    crop = new Texture2D(width, height, TextureFormat.RGB24, false);
                    crop.SetPixels(full.GetPixels(x, full.height - top - height, width, height)); crop.Apply(); output = crop;
                }
                var id = Guid.NewGuid().ToString("N") + ".png";
                var bytes = output.EncodeToPNG(); File.WriteAllBytes(Path.Combine(host.ArtifactDirectory, id), bytes);
                request.Result = new JObject { ["artifactId"] = id, ["mimeType"] = "image/png", ["width"] = width, ["height"] = height, ["bytes"] = bytes.Length, ["uiScale"] = Prefs.UIScale, ["crop"] = Wire.Token(new { x, y = top, width, height }), ["capturedAt"] = host.Stamp(), ["layout"] = host.Ui.Snapshot() };
            }
            catch (Exception ex) { request.Error = ex; }
            finally { if (crop != null) UnityEngine.Object.Destroy(crop); if (full != null) UnityEngine.Object.Destroy(full); request.Done = true; }
        }
    }
}
