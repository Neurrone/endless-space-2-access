using System.Collections.Generic;
using ES2Access.Core.Speech;

namespace ES2Access.Tests.Speech
{
    /// <summary>Installing a handful of translations for one test, written as the pairs they are.</summary>
    internal static class ModStringsFixture
    {
        public static void Install(params string[] pairs)
        {
            Dictionary<string, string> strings = new Dictionary<string, string>();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                strings[pairs[i]] = pairs[i + 1];
            }

            ModStrings.Install(strings);
        }
    }
}
