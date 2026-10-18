using System;
using System.Reflection;

namespace Oxygendsl.Mod
{
    static class AllocationProbe
    {
        static readonly Func<long> ReadBytes = Bind();

        public static bool Available { get { return ReadBytes != null; } }

        public static long Snapshot()
        {
            if (ReadBytes == null) return -1L;
            try { return ReadBytes(); }
            catch (TargetInvocationException) { return -1L; }
            catch (InvalidOperationException) { return -1L; }
        }

        static Func<long> Bind()
        {
            MethodInfo method = typeof(GC).GetMethod(
                "GetAllocatedBytesForCurrentThread",
                BindingFlags.Public | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);
            if (method == null || method.ReturnType != typeof(long)) return null;
            Func<long> read = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
            long before = read();
            byte[] calibration = new byte[256];
            long after = read();
            GC.KeepAlive(calibration);
            return after > before ? read : null;
        }
    }
}
