using System.Collections.Generic;
using Verse;

namespace HungerAndHavoc.Incidents
{
    // XML 和存档通过类名创建信件
    public sealed class ChoiceLetter_RHAH_BanditAttack : ChoiceLetter
    {
        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                yield return Option_Close;
            }
        }
    }
}
