using Amplitude.Unity.Framework;
using Amplitude.Unity.Options;

namespace ES2Access.UI.ModOptions
{
    /// <summary>
    /// THE ONE OPTION THE SCANNER TAB HAS, and the player never meets it.
    ///
    /// The tab's whole content is a tree of mod nodes (<see cref="ScannerEditor"/>) - the game has no
    /// row kind that can express a list somebody edits. But the window's Apply/Cancel machinery is
    /// entirely built on OPTIONS: it decides whether Apply lights, whether Escape asks "you have not
    /// applied your changes", and what Cancel puts back, by walking the rows of every panel and
    /// asking each row's option whether it has changed
    /// (<c>OptionsTabPanel.CheckWhetherSomeApplicationSettingHasChanged</c>,
    /// <c>OptionsModalWindow.OnOptionChanged</c>). A panel with no rows can never be dirty.
    ///
    /// So the tab declares exactly one toggle, whose row is built by the game's own panel loading and
    /// then hidden (<see cref="ScannerRows"/>). Its value is not a setting: it answers "does what the
    /// player has edited differ from what is saved", which is precisely the question the window is
    /// asking. Setting it back to its backup - which is what Cancel and the game's own
    /// exit-without-applying box do - throws the edits away.
    ///
    /// This is why the mod does not re-implement any of it: no override of <c>HandleInput</c>, no
    /// rewiring of the drawn Apply and Cancel buttons, and no second confirmation box of its own.
    /// Non-latent, like every other mod row, so no fifteen-second validate countdown is possible
    /// (<c>OptionsModalWindow.OnApplyCb</c> raises that for any latent commit, on any tab).
    /// </summary>
    public interface IModScannerService : IService
    {
        [OptionTypeToggle("ScannerCustomCategories")]
        bool Edited { get; set; }
    }

    /// <summary>The Scanner tab's service. It holds nothing: the edits live in
    /// <see cref="ScannerEditor"/>, which outlives the window being rebuilt.</summary>
    public sealed class ModScannerService : IModScannerService
    {
        public bool Edited
        {
            get { return ScannerEditor.Edited; }
            set
            {
                // The only write the game ever makes is Restore's, putting the backup - false - back.
                // A write of true would be the window telling us something we told it, so it is
                // ignored rather than acted on.
                if (!value)
                {
                    ScannerEditor.Discard();
                }
            }
        }
    }
}
