using System;

namespace ES2Access.UI
{
    /// <summary>
    /// The missing-technology hint this game hangs on a control it is refusing: whether the game is
    /// DRAWING one right now, which is a narrower question than the hint being THERE
    /// (<see cref="AgeWidgets.Hinted"/>).
    ///
    /// <c>Gui.FormatButtonHint</c> (<c>Gui.cs</c> :1150-1245) is the only thing that ever clears a
    /// widget's technology, so every refresh path that does not reach a <c>FormatButtonHint</c> call
    /// leaves the previous subject's technology on the component for good - and the refresh paths that
    /// do not reach it are the ones where the control is FINE. A pooled widget rebound to a subject the
    /// game allows, and a persistent widget whose subject becomes allowed, both keep a dead hint; the
    /// player is told nothing about it, because the call asks for no fade and the allowing branch
    /// rewrites the tooltip from scratch.
    ///
    /// So the test is the game's own: the hint call appends its
    /// <c>%MissingTechnologyClickDescription</c> sentence to the tooltip of the very widget it hinted,
    /// and a widget whose tooltip no longer carries that sentence is one the game has stopped offering
    /// the jump on. The same comparison the game makes before appending it - markup and all, since that
    /// is what sits in <c>Content</c>.
    /// </summary>
    public static class TechnologyHints
    {
        /// <summary>Whether the game is drawing <paramref name="widget"/>'s missing-technology hint.
        /// The widget is the one the game passed to <c>Gui.FormatButtonHint</c>, since that is both
        /// where the hint component lives and whose tooltip the sentence was appended to.</summary>
        public static bool Drawn(AgeTransform widget)
        {
            return Drawn(widget, null);
        }

        /// <summary>
        /// The same question where the game wrote the sentence somewhere OTHER than on the hinted
        /// widget - the <c>customTooltip</c> argument of <c>Gui.FormatButtonHint</c>, which three
        /// prefabs in this game pass (<c>FleetActionItem.SetEnable</c> :86/:90 hands it the ITEM's
        /// tooltip while hinting the button or the toggle inside it; <c>HackingProgramLine.Refresh</c>
        /// :50 hands it the line's own <c>Tooltip</c> field while hinting the little hint button).
        /// Those buttons carry no <c>AgeTooltip</c> of their own (measured), so the one-argument form
        /// answers false at all three even while the hint is live.
        ///
        /// <paramref name="drawnOn"/> null is every other caller and asks the widget for its own,
        /// which is what <see cref="Drawn(AgeTransform)"/> does.
        /// </summary>
        public static bool Drawn(AgeTransform widget, AgeTooltip drawnOn)
        {
            try
            {
                if (!AgeWidgets.Hinted(widget))
                {
                    return false;
                }

                AgeTooltip tooltip = drawnOn ?? AgeWidgets.Raw(widget);
                // The RAW content, deliberately: nothing here is read to the player - this is the
                // same string comparison the game makes before appending the sentence, and the
                // sentence it appends carries colour markup, so cleaning either side would stop the
                // two matching. The words of this tooltip reach the player through the door, on the
                // control's own tooltip section.
                string content = tooltip == null ? null : tooltip.Content;
                return !string.IsNullOrEmpty(content) && content.Contains(Sentence());
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string _sentence;
        private static string _sentenceLanguage;

        /// <summary>The game's "Hold Control+Click…" sentence, localized once per language rather than
        /// once per widget per frame - <see cref="Drawn"/> is asked on a build path. Keyed on the
        /// language, which is what every memo over a composed phrase is keyed on.</summary>
        private static string Sentence()
        {
            string language = ES2Access.Localization.ModLocale.Language;
            if (_sentence == null || _sentenceLanguage != language)
            {
                _sentence = Gui.Localize("%MissingTechnologyClickDescription");
                _sentenceLanguage = language;
            }

            return _sentence;
        }
    }
}
