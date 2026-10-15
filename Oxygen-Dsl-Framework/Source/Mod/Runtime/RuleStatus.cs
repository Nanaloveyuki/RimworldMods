using System.Collections.Generic;
using Oxygendsl.Core;

namespace Oxygendsl.Mod
{
    public sealed class RuleStatus
    {
        public string Id;
        public string Label;
        public string LastError;
        public RuleKind Kind;
        public int Matched;
        public int Skipped;
        public int Active;
        public int LastTick;
        public List<string> Details = new List<string>();
    }
}
