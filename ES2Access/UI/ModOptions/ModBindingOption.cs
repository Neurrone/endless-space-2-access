using Amplitude.Unity.Framework;
using Amplitude.Unity.Input;
using Amplitude.Unity.Options;
using ES2Access.UI.Input;
using InputBinding = Amplitude.Unity.Input.InputBinding;

namespace ES2Access.UI.ModOptions
{
    /// <summary>
    /// The Keybinds category's service. It declares NO options of its own: its rows are minted one
    /// per mod action (<see cref="ModBindingOption"/>) rather than read off one provider's
    /// properties, because the actions are a list that grows and a C# property is not.
    ///
    /// It exists at all because the game's panel refuses to load without a registered service
    /// (<c>OptionsTabPanel.Load</c> logs an error and gives up), and because removing it is how the
    /// mod's tab stops answering after a hot reload.
    /// </summary>
    public interface IModKeybindsService : IService { }

    /// <summary>
    /// The interface ONE mod action's binding is read and written through.
    ///
    /// The attribute lives here rather than on the class because that is what the game's option
    /// machinery walks: <c>Option.GetOptions(instance, typeof(IModBindingProvider), …)</c> reads
    /// the properties of the TYPE it is handed, and the plain-object overload does not follow a
    /// class up to its interfaces at all (measured 2026-08-23).
    ///
    /// <c>AcceptsMultipleKeys</c> is the mod's ruling that mod keys are not conflict-checked
    /// against each other: it makes the game's row skip its own already-bound lookup on commit and
    /// makes the game's Controls tab unable to steal a chord from one of these rows. The one
    /// warning the design does want - a mod key landing on a chord the game uses, and the reverse -
    /// is a later stage.
    /// </summary>
    public interface IModBindingProvider
    {
        [OptionTypeKeyMapping("Binding", true)]
        InputBinding Binding { get; set; }
    }

    /// <summary>
    /// One mod action, seen as a settings row.
    ///
    /// The getter answers with the STABLE instance the binding store holds, never a fresh object:
    /// <c>InputBinding</c> has no <c>Equals</c>, and the options window decides whether anything has
    /// changed by comparing the value it stored against the value it reads back. A new object every
    /// call would leave Apply permanently lit and make Escape always ask about unapplied changes.
    ///
    /// The setter is non-latent on purpose (the mod's rows behave like the game's Gameplay tab): it
    /// takes effect at once, Cancel puts the backup back through this same setter, and Apply is what
    /// makes the value outlive the session.
    /// </summary>
    public sealed class ModBindingOption : IModBindingProvider
    {
        public ModBindingOption(string actionKey)
        {
            ActionKey = actionKey;
        }

        /// <summary>Which of the mod's actions this row is for - the action's own name.</summary>
        public readonly string ActionKey;

        public InputBinding Binding
        {
            get { return ModBindings.Of(ActionKey); }
            set { ModBindings.Set(ActionKey, value); }
        }
    }

    /// <summary>The Keybinds service itself - a marker with nothing on it, see
    /// <see cref="IModKeybindsService"/>.</summary>
    public sealed class ModKeybindsService : IModKeybindsService { }
}
