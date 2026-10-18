using System;
using System.Collections.Generic;
using Oxygendsl.Core;
using Oxygendsl.Mod;

namespace Odf.PrisonerNames
{
    public static class EventFacts
    {
        public static IOxygenHostValue Create(OxygenPawnEvent message)
        {
            var facts = new Dictionary<string, object>(StringComparer.Ordinal);
            if (message.Facts != null) CopyKnown(message.Facts, facts);
            facts["id"] = message.Id ?? string.Empty;
            facts["provider"] = message.Provider ?? string.Empty;
            facts["sequence"] = message.Sequence;
            return new DictionaryOxygenHostValue(facts);
        }

        static void CopyKnown(IOxygenHostValue source, Dictionary<string, object> target)
        {
            string[] names = { "before", "after", "amount", "actor_id", "tick" };
            for (int i = 0; i < names.Length; i++)
            {
                object value;
                if (source.TryGetMember(names[i], out value)) target[names[i]] = value;
            }
        }
    }
}
