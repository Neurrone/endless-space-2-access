using System;
using System.Collections.Generic;
using System.Reflection;

namespace ES2Access.UI
{
    /// <summary>
    /// The mod manager's Steam Workshop controls, which exist only in the STEAM build of the game -
    /// and the only place in the mod allowed to name them.
    ///
    /// This is the second shape the two stores' builds differ in, and it is not the galaxy's
    /// (<see cref="GameGalaxy"/>, a type RENAMED). Here the members are simply absent: the GOG build
    /// ships no Steam Workshop at all, so <c>ModdingScreen</c> declares neither
    /// <c>WorkshopLegalAgreementButton</c> nor <c>SteamWorkshopButton</c>, and
    /// <c>ModdingAvailableModsPanel</c> declares no <c>WorkshopFilterToggle</c>. The two classes
    /// themselves, and every other field on them, are identical in both builds.
    ///
    /// Naming an absent field will not compile against the GOG assemblies, and a binary compiled
    /// naming it would throw <c>MissingFieldException</c> on the GOG build at runtime, so one
    /// reflected read per control is what lets one built DLL serve both. Null is the honest answer
    /// on GOG and needs no special case downstream: the mod's own <c>Cells.AddControl</c> and
    /// checkbox builders contribute nothing for a null widget, so the page declares exactly the
    /// controls the player in front of it actually has.
    ///
    /// The <c>FieldInfo</c>s are cached with no invalidation, which is sound because a process loads
    /// exactly one build of the game; nothing here holds a game object, so a hot reload needs no
    /// teardown.
    /// </summary>
    public static class SteamWorkshop
    {
        private static readonly Dictionary<string, FieldInfo> Fields =
            new Dictionary<string, FieldInfo>();

        /// <summary>The Workshop's legal agreement button in the manager's top band; null on GOG.
        /// </summary>
        public static AgeTransform LegalAgreementButton(ModdingScreen screen)
        {
            return Read(screen, "WorkshopLegalAgreementButton") as AgeTransform;
        }

        /// <summary>The button that leaves the game for the Workshop, in the action band; null on
        /// GOG.</summary>
        public static AgeTransform OpenButton(ModdingScreen screen)
        {
            return Read(screen, "SteamWorkshopButton") as AgeTransform;
        }

        /// <summary>The library filter that decides whether Workshop mods are listed; null on GOG.
        /// </summary>
        public static AgeControlToggle FilterToggle(ModdingAvailableModsPanel panel)
        {
            return Read(panel, "WorkshopFilterToggle") as AgeControlToggle;
        }

        private static object Read(object owner, string name)
        {
            if (owner == null)
            {
                return null;
            }

            Type type = owner.GetType();
            string key = type.FullName + "." + name;
            FieldInfo field;
            if (!Fields.TryGetValue(key, out field))
            {
                field = type.GetField(name);
                Fields[key] = field;
            }

            return field == null ? null : field.GetValue(owner);
        }
    }
}
