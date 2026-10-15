using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace SaveYourStorageAndTickEveryMinute.Collections
{
    internal static class ListVersionAccessor
    {
        private static readonly FieldInfo VersionField =
            typeof(List<Thing>).GetField(
                "_version",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Func<List<Thing>, int> GetVersion =
            CreateAccessor();

        public static bool IsAvailable => GetVersion != null || VersionField != null;

        public static bool TryGet(List<Thing> source, out int version)
        {
            Func<List<Thing>, int> accessor = GetVersion;
            if (source == null)
            {
                version = 0;
                return false;
            }

            if (accessor != null)
            {
                version = accessor(source);
                return true;
            }

            object value = VersionField == null ? null : VersionField.GetValue(source);
            if (value is int)
            {
                version = (int)value;
                return true;
            }

            version = 0;
            return false;
        }

        private static Func<List<Thing>, int> CreateAccessor()
        {
            if (VersionField == null)
                return null;

            try
            {
                var method = new DynamicMethod(
                    "SaveYourStorage_GetThingListVersion",
                    typeof(int),
                    new[] { typeof(List<Thing>) },
                    typeof(List<Thing>).Module,
                    true);
                ILGenerator il = method.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, VersionField);
                il.Emit(OpCodes.Ret);
                return (Func<List<Thing>, int>)method.CreateDelegate(
                    typeof(Func<List<Thing>, int>));
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
