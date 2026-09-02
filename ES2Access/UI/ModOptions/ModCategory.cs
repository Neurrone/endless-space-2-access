using System;
using Amplitude.Unity.Framework;

namespace ES2Access.UI.ModOptions
{
    /// <summary>
    /// One tab of the mod's options window: what the game calls the category, which service its
    /// panel resolves, what the tab is named in the player's language, and who fills its rows.
    ///
    /// The window's whole category bar is a LIST of these (<see cref="ModOptions.Categories"/>), so
    /// adding a tab - or putting one ahead of another - is one line there and nothing here.
    ///
    /// <paramref name="Name"/> is the game's own key for the category. It is what the window's
    /// private <c>categoryNames</c> array holds, what its tab dictionaries are keyed by, and what
    /// the radio group's selection is turned back into - so it is an identifier, never a spoken
    /// word. The words are <see cref="Title"/> and <see cref="Description"/>, written over the
    /// game's own labels after the tab is built, because a localization key the game has no row for
    /// is DRAWN AND SPOKEN raw.
    /// </summary>
    public sealed class ModCategory
    {
        public ModCategory(
            string name,
            Type serviceType,
            IService service,
            System.Func<string> title,
            System.Func<string> description,
            Action<OptionsTabPanel> fill
        )
        {
            Name = name;
            ServiceType = serviceType;
            Service = service;
            Title = title;
            Description = description;
            Fill = fill;
        }

        public readonly string Name;
        public readonly Type ServiceType;
        public readonly IService Service;
        /// <summary>The words, resolved when the tab is built rather than held as a key: two of the
        /// tabs are numbered, so their names are composed rather than looked up.</summary>
        public readonly System.Func<string> Title;

        /// <summary>The sentence the tab explains itself with, or NULL for a tab that has none - and
        /// then the tab carries no tooltip at all (ModOptionsWindow.Relabel).</summary>
        public readonly System.Func<string> Description;

        /// <summary>Puts the category's rows in, once the game has built the empty panel. Null for a
        /// category whose service declares its own option properties, which the game's own panel
        /// loading already turns into rows.</summary>
        public readonly Action<OptionsTabPanel> Fill;
    }
}
