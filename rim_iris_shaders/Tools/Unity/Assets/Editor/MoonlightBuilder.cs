using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class MoonlightBuilder
{
    public static void Build()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string staging = Path.Combine(root, "artifacts", "bundles");
        Directory.CreateDirectory(staging);
        var manifest = BuildPipeline.BuildAssetBundles(staging, new[] {
            new AssetBundleBuild { assetBundleName = "moonlight_win", assetNames = new[] { "Assets/Moonlight.shader" } }
        }, BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.ForceRebuildAssetBundle, BuildTarget.StandaloneWindows64);
        if (manifest == null) throw new BuildFailedException("Bundle build failed.");

        string path = Path.Combine(staging, "moonlight_win");
        var bundle = AssetBundle.LoadFromFile(path);
        if (bundle == null) throw new BuildFailedException("Bundle reload failed.");
        try
        {
            var shaders = bundle.LoadAllAssets<Shader>();
            if (shaders.Length != 1 || shaders[0].name != "Hidden/RimIrisShaders/Moonlight" || !shaders[0].isSupported)
                throw new BuildFailedException("Expected supported Moonlight shader in bundle.");
            Validate(shaders[0]);
        }
        finally { bundle.Unload(true); }
        File.Copy(path, Path.Combine(root, "ShaderPacks/Moonlight/moonlight_win"), true);
        Debug.Log("[Moonlight] Bundle reload and GPU validation passed; bundle published.");
    }

    private static void Validate(Shader shader)
    {
        var material = new Material(shader);
        var source = new Texture2D(4, 4, TextureFormat.RGBAFloat, false, true);
        var result = new Texture2D(4, 4, TextureFormat.RGBAFloat, false, true);
        var target = RenderTexture.GetTemporary(4, 4, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var previous = RenderTexture.active;
        try
        {
            const string prefix = "_RimIris_Pack_rimirisshaders_moonlight_";
            Shader.SetGlobalFloat(prefix + "brightness", 1.6f);
            Shader.SetGlobalFloat(prefix + "coolness", 0.65f);
            Func<Color, float, float, Color> render = (input, sun, strength) => {
                var pixels = new Color[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = input;
                source.SetPixels(pixels);
                source.Apply();
                Shader.SetGlobalFloat("_RimIris_SunGlow", sun);
                Shader.SetGlobalFloat(prefix + "strength", strength);
                Graphics.Blit(source, target, material);
                RenderTexture.active = target;
                result.ReadPixels(new Rect(0, 0, 4, 4), 0, 0);
                result.Apply();
                return result.GetPixel(2, 2);
            };
            var sample = new Color(0.15f, 0.15f, 0.15f, 0.6f);
            Near(render(sample, 1, 1), sample, "day identity");
            Near(render(sample, 0, 0), sample, "disabled identity");
            Near(render(Color.black, 0, 1), Color.black, "black preservation");
            Near(render(Color.white, 0, 1), Color.white, "white preservation");
            Color night = render(sample, 0, 1);
            if (night.r <= sample.r || night.b <= night.g || night.g <= night.r)
                throw new BuildFailedException("Night must brighten neutral shadows with a cool tint.");
            if (Mathf.Abs(night.a - sample.a) > 0.002f)
                throw new BuildFailedException("Alpha changed.");
            Color dusk = render(sample, 0.3f, 1);
            if (!(dusk.b > sample.b && dusk.b < night.b))
                throw new BuildFailedException("Twilight transition is not intermediate.");
            for (int i = 0; i <= 100; i++)
            {
                Color c = render(new Color(i / 100f, i / 100f, i / 100f, 1), 0, 1);
                if (float.IsNaN(c.r + c.g + c.b) || Mathf.Min(c.r, Mathf.Min(c.g, c.b)) < -0.002f || c.maxColorComponent > 1.002f)
                    throw new BuildFailedException("Non-finite or out-of-range output.");
            }
            Debug.Log("[Moonlight] GPU: day/off identity, black/white, night brightness/tint, alpha, twilight, 101 grayscale samples passed. Night=" + night);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(result);
        }
    }

    private static void Near(Color actual, Color expected, string label)
    {
        for (int i = 0; i < 4; i++)
            if (Mathf.Abs(actual[i] - expected[i]) > 0.002f)
                throw new BuildFailedException(label + ": " + actual + " != " + expected);
    }
}
