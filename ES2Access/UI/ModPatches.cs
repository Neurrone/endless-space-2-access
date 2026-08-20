using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ES2Access.UI
{
    /// <summary>
    /// Who is patching a game method right now, one id per patch.
    ///
    /// This is the reload check no other probe can make: a reload that failed to unpatch leaves TWO
    /// owners on the same method, and two copies of an idempotent hook do the same thing twice and
    /// look perfectly healthy from the outside - identical speech, identical dumps. The ids are
    /// unique per load (repo convention), so the count and the names together say which load each
    /// patch belongs to.
    /// </summary>
    internal static class ModPatches
    {
        internal static string[] Owners(MethodBase method, bool prefixes)
        {
            try
            {
                Patches info = method == null ? null : Harmony.GetPatchInfo(method);
                ICollection<Patch> patches = info == null
                    ? null
                    : (prefixes ? info.Prefixes : info.Postfixes);
                if (patches == null)
                {
                    return new string[0];
                }

                List<string> owners = new List<string>(1);
                foreach (Patch patch in patches)
                {
                    owners.Add(patch.owner);
                }

                return owners.ToArray();
            }
            catch (Exception e)
            {
                return new[] { "<err: " + e.Message + ">" };
            }
        }
    }
}
