#if RIMWORLD_1_7
using System;
using System.Collections;
using System.Reflection;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using Brutal.VulkanApi;
using HarmonyLib;
using UnityEngine;
using UnityStubs.Gui;
using UnityStubs.Rendering;

namespace IrisMenus
{
    // Redirect only this window's output; never read the screen or repeat hosted callbacks.
    internal sealed class NativeGuiSnapshot17 : IDisposable
    {
        private readonly object backend;
        private readonly FieldInfo drawList, materialDraws;
        private readonly IDisposable standalone;
        private readonly Func<float2, float2, ImDrawListPtr> reset;
        private readonly Action upload;
        private readonly object renderer;
        private readonly MethodInfo copy, record;
        private readonly Type snapshotType;
        private readonly RimWorldWaylith.Rendering.CameraTextureBaker baker;
        private RenderTexture texture;

        internal NativeGuiSnapshot17()
        {
            backend = GuiBackend.Current ?? throw new InvalidOperationException("No active native GUI backend.");
            Type type = backend.GetType();
            drawList = Required(AccessTools.Field(type, "drawList"), "ImGuiGuiBackend.drawList");
            materialDraws = Required(AccessTools.Field(type, "materialDraws"), "ImGuiGuiBackend.materialDraws");
            object host = Required(AccessTools.Field(type, "host"), "ImGuiGuiBackend.host").GetValue(backend);
            renderer = Required(AccessTools.Property(host.GetType(), "Renderer"), "ImGuiHost.Renderer").GetValue(host);
            Assembly assembly = type.Assembly;
            Type standaloneType = assembly.GetType("RimWorldWaylith.Imgui.StandaloneDrawList", true);
            snapshotType = assembly.GetType("RimWorldWaylith.Imgui.ImGuiDrawSnapshot", true);
            copy = Required(AccessTools.Method(snapshotType, "CopyFrom", new[]
            {
                typeof(ImDrawListPtr), typeof(int), typeof(float2), typeof(float2),
                typeof(System.Collections.Generic.IReadOnlyList<>).MakeGenericType(
                    assembly.GetType("RimWorldWaylith.Imgui.ImGuiMaterialDraw", true))
            }), "ImGuiDrawSnapshot.CopyFrom");
            record = Required(AccessTools.Method(renderer.GetType(), "RecordOffscreen", new[]
                { snapshotType, typeof(CommandBuffer), typeof(VkRenderPass) }), "ImGuiVulkanRenderer.RecordOffscreen");
            upload = Required(AccessTools.Method(renderer.GetType(), "ProcessPendingTextureUpdates"),
                "ImGuiVulkanRenderer.ProcessPendingTextureUpdates").CreateDelegate<Action>(renderer);
            baker = CameraTextureRenderer.Current as RimWorldWaylith.Rendering.CameraTextureBaker
                ?? throw new InvalidOperationException("No native GUI texture baker.");
            MethodInfo resetMethod = Required(AccessTools.Method(standaloneType, "Reset", new[]
                { typeof(float2), typeof(float2) }), "StandaloneDrawList.Reset");
            standalone = (IDisposable)Activator.CreateInstance(standaloneType, true);
            reset = resetMethod.CreateDelegate<Func<float2, float2, ImDrawListPtr>>(standalone);
        }

        private static T Required<T>(T member, string name) where T : MemberInfo =>
            member ?? throw new MissingMemberException("IrisMenus frame capture requires " + name);

        internal void Capture(Action draw)
        {
            ReleaseTexture();
            var live = (ImDrawListPtr)drawList.GetValue(backend);
            ImDrawListPtr captured = reset(live.GetClipRectMin(), live.GetClipRectMax());
            IList materials = (IList)materialDraws.GetValue(backend);
            int firstMaterial = materials.Count;
            object snapshot = Activator.CreateInstance(snapshotType, true);
            try
            {
                drawList.SetValue(backend, captured);
                try { draw(); }
                finally { drawList.SetValue(backend, live); }
                upload();
                var display = new float2(Screen.width, Screen.height);
                copy.Invoke(snapshot, new object[] { captured, 0, display, new float2(1f, 1f), materials });
                texture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32)
                {
                    name = "IrisMenus frozen interface",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                texture.Create();
                // The baker waits for completion, so the same Repaint may sample the frozen image.
                baker.RecordIntoTarget(texture, (command, pass) =>
                    record.Invoke(renderer, new object[] { snapshot, command, pass }));
            }
            catch
            {
                ReleaseTexture();
                throw;
            }
            finally
            {
                while (materials.Count > firstMaterial) materials.RemoveAt(materials.Count - 1);
            }
        }

        internal void DrawTile(Rect destination, Rect source)
        {
            Rect screen = IrisMenus.UI.UiCoordinates.ToScreen(source);
            GUI.DrawTextureWithTexCoords(destination, texture, new Rect(screen.x / texture.width,
                1f - screen.yMax / texture.height, screen.width / texture.width, screen.height / texture.height), false);
        }

        internal void ReleaseTexture()
        {
            if (texture == null) return;
            texture.Release();
            UnityEngine.Object.Destroy(texture);
            texture = null;
        }

        public void Dispose()
        {
            ReleaseTexture();
            standalone.Dispose();
        }
    }
}
#endif
