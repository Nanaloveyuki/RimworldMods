using System;
using Verse;

namespace MouseDisaster
{
    public readonly struct MouseDisasterGeneRemovalScope : IDisposable
    {
        private readonly Pawn pawn;

        public MouseDisasterGeneRemovalScope(Pawn pawn)
        {
            this.pawn = pawn;
            MouseDisasterUtility.BeginInternalMouseDisasterGeneRemoval(pawn);
        }

        public void Dispose()
        {
            MouseDisasterUtility.EndInternalMouseDisasterGeneRemoval(pawn);
        }
    }
}
