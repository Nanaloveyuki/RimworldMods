#if RIMWORLD_1_7
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RimIris;
using UnityEngine;
using Verse;
using Waylith.Rendering;
using Material = UnityEngine.Material;
using NativeShader = Waylith.Rendering.Shader;
using Shader = UnityEngine.Shader;

namespace RimIrisShaders
{
    // RimIris still scans the root bundle manifest for its options and lifecycle.
    // Replace only this pack's activation: 1.7 cannot execute its old D3D bundle.
    internal static class MoonlightNative
    {
        private const string ShaderName = "Hidden/RimIrisShaders/Moonlight";
        private static readonly FieldInfo ActiveRuntimes = typeof(PackEngine).GetField(
            "active", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo TimingKey = typeof(PackPassDef).GetField(
            "TimingKey", BindingFlags.Instance | BindingFlags.NonPublic);
        // RimIris was compiled against UnityEngine.CoreModule. Waylith redirects
        // that identity at runtime; avoid binding those two legacy field types
        // in this net10 build, which correctly references UnityStubs instead.
        private static readonly FieldInfo PassMaterial = typeof(PackPassDef).GetField("Mat");
        private static readonly FieldInfo RuntimeMaterials = typeof(BundleRuntime).GetField("Materials");

        internal static void Install(PackEntry entry)
        {
            if (ActiveRuntimes == null || TimingKey == null || PassMaterial == null || RuntimeMaterials == null)
                throw new MissingFieldException("Unsupported RimIris PackEngine runtime contract.");
            ModContentPack mod = LoadedModManager.RunningMods.First(m =>
                string.Equals(m.PackageId, "RimIrisShaders.Moonlight", StringComparison.OrdinalIgnoreCase));
            string path = Path.Combine(mod.RootDir, "1.7", "Rendering", "Moonlight.frag.glsl");
            entry.OnActivated = () => Activate(entry.Id, path);
        }

        private static void Activate(string packId, string path)
        {
            var active = (Dictionary<string, BundleRuntime>)ActiveRuntimes.GetValue(null);
            if (active.ContainsKey(packId)) return;
            string source = File.ReadAllText(path);
            var definition = new ShaderProgramDefinition
            {
                fragmentShaderFileName = path,
                flipTextureST = false,
                transparent = false,
                blendMode = ShaderBlendMode.Opaque,
                depthTest = false,
                depthWrite = false,
                cullMode = FaceCull.None
            };
            var native = new NativeShader(ShaderName, path, definition,
                materialLayout: ShaderMetadataParser.ExtractMaterialLayout(source, path));
            if (native.HasErrors)
                throw new InvalidOperationException("Invalid Moonlight native shader layout.");
            var material = new Material(Shader.FromEngineShader(ShaderName, native));
            try
            {
                var pass = new PackPassDef
                {
                    Id = "moonlight",
                    ShaderName = ShaderName,
                    PreComposite = false,
                    EnableWhen = EnableWhen.Parse("strength > 0")
                };
                TimingKey.SetValue(pass, "pack:" + packId + ":moonlight:post");
                PassMaterial.SetValue(pass, material);
                var runtime = new BundleRuntime { Active = true };
                ((IList)RuntimeMaterials.GetValue(runtime)).Add(material);
                runtime.Passes.Add(pass);
                // PackEngine owns the material from here: its existing deactivate,
                // watchdog, uniform updates and post-composite pass stay in control.
                active.Add(packId, runtime);
            }
            catch
            {
                UnityEngine.Object.Destroy(material);
                throw;
            }
            Log.Message("[Moonlight] Activated native 1.7 post-composite pass.");
        }
    }
}
#endif
