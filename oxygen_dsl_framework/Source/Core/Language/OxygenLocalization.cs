using System.Collections.Generic;

namespace Oxygendsl.Core
{
    public interface IOxygenLocalization
    {
        string Translate(string key, IDictionary<string, object> arguments);
    }
}
