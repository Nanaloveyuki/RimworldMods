#if RIMWORLD_1_7
using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Waylith.Rendering;
using RimWorldWaylith.Rendering;
using Brutal.VulkanApi;
using NativeTexture = Waylith.Rendering.Texture;
using NativeShader = Waylith.Rendering.Shader;
using Material = UnityEngine.Material;
using Graphics = UnityEngine.Graphics;
using Texture2D = UnityEngine.Texture2D;

namespace IrisMenus
{
    // UI snapshots capture this borrowed texture object; bind its GPU image only on the
    // render thread, once the submission slot (not the main-thread frame) is known.
    [HarmonyPatch]
    internal static class FrostSceneBinding
    {
        private static readonly FieldInfo SceneTargets = AccessTools.Field(
            AccessTools.TypeByName("RimWorldWaylith.Bootstrap"), "_sceneTargets");
        internal static readonly SceneTexture Texture = new SceneTexture();

        // Render-feature and host forwarding calls can be inlined before mods load.
        // Bind where the UI material consumes the image, not at those wrappers.
        private static MethodBase TargetMethod() => AccessTools.Method(
            AccessTools.TypeByName("RimWorldWaylith.Imgui.ImGuiMaterialRenderer"), "TryRecord");

        private static void Prefix(int frameSlot)
        {
            Texture.Native.GpuResource = null;
            if (Current.ProgramState != ProgramState.Playing) return;
            var targets = (SceneColorTargets)SceneTargets.GetValue(null);
            if (targets == null) return;
            var source = targets.Current(frameSlot);
            if (!source.IsCreated) return;
            Texture.Native.width = source.Width;
            Texture.Native.height = source.Height;
            Texture.Native.GpuResource = source.Color;
            // Scene render-pass final layouts already transitioned this image. Keep
            // the allocation tracker consistent for the GUI descriptor resolver.
            source.Color.SetMipLayout(0, VkImageLayout.ShaderReadOnlyOptimal);
        }

        private static void Finalizer() => Texture.Native.GpuResource = null;

        internal sealed class SceneTexture : UnityEngine.Texture
        {
            internal readonly BorrowedTexture Native = new BorrowedTexture();
            public override NativeTexture CoreTexture => Native;
        }

        internal sealed class BorrowedTexture : NativeTexture
        {
            public override int width { get; set; }
            public override int height { get; set; }
            internal BorrowedTexture() { Name = "IrisMenus Scene Color"; wrapMode = Waylith.Rendering.TextureWrapMode.Clamp; }
            public override void Dispose() { GpuResource = null; } // The game owns the image.
        }
    }

    internal sealed class FrostBackground : IWindowDrawing, IDisposable
    {
        private readonly DefaultWindowDrawing vanilla = new DefaultWindowDrawing();
        internal const float CornerRadius = 12f;
        private Material material;
        private bool failed;
        private Texture2D menuBackdrop;
        private MenuSettings Settings => IrisMenusMod.Instance.Settings;
        public GUIStyle EmptyStyle => vanilla.EmptyStyle;
        public void DoWindowBackground(Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;
            Color fill = Widgets.WindowBGFillColor;
            fill.a = 1f - Mathf.Clamp01(Settings.Transparency);
            UnityStubs.Gui.GuiBackend.Current?.FillRectRounded(rect, fill, CornerRadius);
            if (failed) return;
            try
            {
                if (material == null)
                {
                    string path = Path.Combine(IrisMenusMod.Instance.Content.RootDir, "1.7", "Rendering", "Frost17.frag.glsl");
                    string source = File.ReadAllText(path);
                    var definition = new ShaderProgramDefinition
                    {
                        fragmentShaderFileName = path,
                        transparent = true,
                        depthTest = false,
                        depthWrite = false,
                        cullMode = FaceCull.None
                    };
                    var shader = new NativeShader("IrisMenus/Frost17", path, definition,
                        materialLayout: ShaderMetadataParser.ExtractMaterialLayout(source, path),
                        vertexInputLayout: new ShaderVertexInputLayout());
                    if (shader.HasErrors) throw new InvalidOperationException("Invalid IrisMenus native frost shader layout.");
                    material = new Material(UnityEngine.Shader.FromEngineShader("IrisMenus/Frost17", shader));
                }
                bool scene = Current.ProgramState == ProgramState.Playing;
                UnityEngine.Texture backdrop = null;
                Rect sourceRect = new Rect(0f, 0f, Screen.width, Screen.height);
                if (Settings.FrostEnabled && Settings.Transparency > 0f)
                {
                    if (scene) backdrop = FrostSceneBinding.Texture;
                    else if (UIMenuBackgroundManager.background is UI_BackgroundMain menu)
                    {
                        // The entry background is an IMGUI image, not scene color. Sample
                        // its original texture with the same centered cover transform.
                        if (menuBackdrop == null) menuBackdrop = ContentFinder<Texture2D>.Get("UI/HeroArt/BGPlanet");
                        backdrop = menu.overrideBGImage ?? menuBackdrop;
                        Vector2 size = menu.overrideBGImage != null
                            ? new Vector2(backdrop.width, backdrop.height) : new Vector2(2048f, 1280f);
                        if (UI.screenWidth <= UI.screenHeight * size.x / size.y)
                        {
                            float width = UI.screenHeight * size.x / size.y;
                            sourceRect = new Rect((UI.screenWidth / 2 - width * 0.5f) * Prefs.UIScale, 0f,
                                width * Prefs.UIScale, UI.screenHeight * Prefs.UIScale);
                        }
                        else
                        {
                            float height = UI.screenWidth * size.y / size.x;
                            sourceRect = new Rect(0f, (UI.screenHeight / 2 - height * 0.5f) * Prefs.UIScale,
                                UI.screenWidth * Prefs.UIScale, height * Prefs.UIScale);
                        }
                    }
                }
                material.SetTexture("_SceneTex", backdrop ?? BaseContent.WhiteTex);
                material.SetVector("_SourceRect", new Vector4(sourceRect.x, sourceRect.y, sourceRect.width, sourceRect.height));
                material.SetVector("_Shape", new Vector4(rect.width, rect.height, CornerRadius, 1.5f));
                material.SetVector("_Frost", new Vector4(backdrop != null ? 1f : 0f, Mathf.Clamp01(Settings.Blur) * 6f,
                    Mathf.Clamp01(Settings.Transparency), scene ? Current.ColorCorrectionCurves?.saturation ?? 1f : 1f));
                material.SetColor("_Fill", Widgets.WindowBGFillColor);
                // Native material binding can fail without throwing. Its ordinary-image
                // fallback must leave the rounded translucent fill above intact.
                Graphics.DrawTexture(rect, BaseContent.ClearTex, new Rect(0f, 0f, 1f, 1f),
                    0, 0, 0, 0, Color.white, material, 0);
            }
            catch (Exception exception)
            {
                failed = true;
                Log.Error("[IrisMenus] Native frost background unavailable: " + exception);
            }
        }

        internal void Retry() { Dispose(); failed = false; }
        public void Dispose()
        {
            if (material != null) UnityEngine.Object.Destroy(material);
            material = null;
        }
        public bool DoCloseButton(Rect rect, string text) => vanilla.DoCloseButton(rect, text);
        public bool DoClostButtonSmall(Rect rect) => vanilla.DoClostButtonSmall(rect);
        public void BeginGroup(Rect rect) => vanilla.BeginGroup(rect);
        public void EndGroup() => vanilla.EndGroup();
        public void DoGrayOut(Rect rect) => vanilla.DoGrayOut(rect);
    }
}
#endif
