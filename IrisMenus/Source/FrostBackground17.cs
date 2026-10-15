#if RIMWORLD_1_7
using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;
using Waylith.Rendering;
using RimWorldWaylith.Rendering;
using Brutal.VulkanApi;
using NativeTexture = Waylith.Rendering.Texture;
using NativeShader = Waylith.Rendering.Shader;
using Material = UnityEngine.Material;
using Graphics = UnityEngine.Graphics;

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

        private static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("RimWorldWaylith.Imgui.ImGuiRenderFeature");
            var mapping = type.GetInterfaceMap(typeof(IRenderPass));
            for (int i = 0; i < mapping.InterfaceMethods.Length; i++)
                if (mapping.InterfaceMethods[i].Name == nameof(IRenderPass.Record))
                    // The public forwarding target can already be inlined before mods load.
                    return mapping.TargetMethods[i];
            throw new MissingMethodException(type.FullName, nameof(IRenderPass.Record));
        }

        private static void Prefix(in FrameContext ctx)
        {
            Texture.Native.GpuResource = null;
            if (Current.ProgramState != ProgramState.Playing) return;
            var targets = (SceneColorTargets)SceneTargets.GetValue(null);
            if (targets == null) return;
            var source = targets.Current(ctx.SubmissionFrameIndex);
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
                bool frost = Settings.FrostEnabled && Settings.Transparency > 0f && Current.ProgramState == ProgramState.Playing;
                material.SetTexture("_SceneTex", frost ? FrostSceneBinding.Texture : BaseContent.WhiteTex);
                material.SetVector("_Shape", new Vector4(rect.width, rect.height, CornerRadius, 1.5f));
                material.SetVector("_Frost", new Vector4(frost ? 1f : 0f, Mathf.Clamp01(Settings.Blur) * 6f,
                    Mathf.Clamp01(Settings.Transparency), Current.ColorCorrectionCurves?.saturation ?? 1f));
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
