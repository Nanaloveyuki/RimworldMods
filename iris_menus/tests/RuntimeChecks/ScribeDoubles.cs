using System;
using System.Collections.Generic;

namespace Verse
{
    public class ModSettings { public virtual void ExposeData() { } }
    public static class Translation { public static string Translate(this string text) => text; }
    public interface IExposable { void ExposeData(); }
    public enum LoadSaveMode { Inactive, Saving, LoadingVars, ResolvingCrossRefs, PostLoadInit }
    public static class Scribe { public static LoadSaveMode mode; }
    public enum LookMode { Value, Deep }
    public static class Scribe_Values
    {
        public static Dictionary<string, object> Data = new Dictionary<string, object>();
        public static readonly List<(LoadSaveMode Mode, string Key)> LookCalls = new List<(LoadSaveMode, string)>();
        public static void Look<T>(ref T value, string key, T defaultValue = default, bool forceSave = false)
        {
            LookCalls.Add((Scribe.mode, key));
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                value = Data.TryGetValue(key, out object stored) ? (T)stored : defaultValue;
            else if (Scribe.mode == LoadSaveMode.Saving && (forceSave || !EqualityComparer<T>.Default.Equals(value, defaultValue)))
                Data[key] = value;
        }

        internal static void Expose(IExposable value, Dictionary<string, object> data)
        {
            Dictionary<string, object> previous = Data;
            Data = data;
            try { value.ExposeData(); }
            finally { Data = previous; }
        }
    }
    public static class Scribe_Collections
    {
        public static void Look<K, V>(ref Dictionary<K, V> values, string key, LookMode keyMode, LookMode valueMode)
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                values = Scribe_Values.Data.TryGetValue(key, out object stored) && stored != null ? new Dictionary<K, V>((Dictionary<K, V>)stored) : null;
            else if (Scribe.mode == LoadSaveMode.Saving)
                Scribe_Values.Data[key] = values == null ? null : new Dictionary<K, V>(values);
        }
        public static void Look<T>(ref List<T> values, string key, LookMode mode)
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (!Scribe_Values.Data.TryGetValue(key, out object stored) || stored == null) values = null;
                else if (mode == LookMode.Deep)
                {
                    values = new List<T>();
                    foreach (Dictionary<string, object> data in (List<Dictionary<string, object>>)stored)
                    {
                        if (data == null) { values.Add(default); continue; }
                        T value = (T)Activator.CreateInstance(typeof(T));
                        Scribe_Values.Expose((IExposable)value, data);
                        values.Add(value);
                    }
                }
                else values = new List<T>((List<T>)stored);
            }
            else if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (values == null) Scribe_Values.Data[key] = null;
                else if (mode == LookMode.Deep)
                {
                    var stored = new List<Dictionary<string, object>>();
                    foreach (T value in values)
                    {
                        if (value == null) { stored.Add(null); continue; }
                        var data = new Dictionary<string, object>();
                        Scribe_Values.Expose((IExposable)value, data);
                        stored.Add(data);
                    }
                    Scribe_Values.Data[key] = stored;
                }
                else Scribe_Values.Data[key] = new List<T>(values);
            }
            else if (mode == LookMode.Deep && values != null)
                foreach (T value in values)
                    if (value is IExposable exposable) exposable.ExposeData();
        }
    }
}

namespace RimWorld
{
    public class Dialog_ModSettings : Verse.Window
    {
        private Verse.Mod mod;
        public Dialog_ModSettings(Verse.Mod mod) { this.mod = mod; }
        public Verse.Mod Owner => mod;
    }
    public abstract class MainButtonWorker
    {
        public bool EngineVisible = true;
        public virtual bool Visible => EngineVisible;
        public abstract void Activate();
    }
}
