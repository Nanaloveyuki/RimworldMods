using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SaveYourStorageAndTickEveryMinute.Collections
{
    internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public static readonly ReferenceEqualityComparer<T> Instance =
            new ReferenceEqualityComparer<T>();

        private ReferenceEqualityComparer()
        {
        }

        public bool Equals(T left, T right)
        {
            return ReferenceEquals(left, right);
        }

        public int GetHashCode(T value)
        {
            return value == null ? 0 : RuntimeHelpers.GetHashCode(value);
        }
    }
}
