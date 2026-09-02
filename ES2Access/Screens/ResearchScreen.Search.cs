using System;
using System.Collections;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>Type-ahead over the whole wheel: the scope it searches and the per-turn corpus of
    /// words each technology answers to.</summary>
    public sealed partial class ResearchScreen
    {
        // ---- searching the whole wheel ----

        /// <summary>
        /// Typing looks through every technology the game is willing to draw, wherever it is - which
        /// is the only scope worth having here, because what the player cannot do on this page is
        /// find something.
        ///
        /// Landing on one opens the quadrant and the stage it is in, so the branch the player is put
        /// into is the branch they can then walk. The opening is recorded rather than done: the graph
        /// is rebuilt between this call and the focus landing, and the expansion set belongs to that
        /// rebuild.
        ///
        /// What each dot answers by is its title and then, after a comma, the same words the GAME's
        /// own search box looks through (<see cref="SearchText"/>) - so typing an unlock finds the
        /// technology that grants it, and a title match still outranks it (the search's own
        /// before-the-comma rule).
        /// </summary>
        public override SearchScope TypeAheadScope(GraphNode focused, GraphRender render)
        {
            if (focused == null || !Equals(focused.StopKey, TreeStop))
            {
                return null;
            }

            List<TechnologyItem2> items = new List<TechnologyItem2>();
            List<ControlId> quadrants = new List<ControlId>();
            List<ControlId> stages = new List<ControlId>();
            WheelIndex(items, quadrants, stages);

            ExpireSearchText();
            List<TechnologyItem2> found = items;
            List<ControlId> quadrantIds = quadrants;
            List<ControlId> stageIds = stages;
            return new SearchScope(
                found.Count,
                index => SearchText(found[index].GuiTechnology),
                index => Reveal(found[index], quadrantIds[index], stageIds[index]),
                // The dot itself, with nothing opened - so that the shared scope can add what a
                // collapsed dot would declare (each unlock's own dossier) without offering the dot
                // twice (<see cref="SearchScope.Extend"/>).
                index => TechnologyId(found[index])
            );
        }

        /// <summary>What one technology answers a typed search by, built once and kept.</summary>
        private readonly Dictionary<GuiTechnology2, string> _searchText =
            new Dictionary<GuiTechnology2, string>();

        /// <summary>The turn the kept search words were built for. Which unlocks pass their
        /// availability prerequisites is what the words depend on, and that only moves when a
        /// technology finishes - which is a turn boundary.</summary>
        private int _searchTurn = int.MinValue;

        /// <summary>How many times per keystroke the words were composed rather than reused. Read by
        /// the dev probe: the corpus must be built once per technology, never once per letter.
        /// </summary>
        public static int SearchTextBuilds;

        /// <summary>
        /// The words a technology answers a search by: its title, then - after a comma, so a title
        /// match still wins - the technology's own keywords and, for every unlock the player already
        /// passes the availability prerequisites for, that unlock's title, keywords and the localized
        /// titles of its category and sub-category.
        ///
        /// That list is the game's own search corpus, term for term
        /// (<c>TechnologyLookupPanel.BindTechnology</c> :41-73), so typing here finds what typing into
        /// the game's own search box would highlight. Kept per technology because
        /// <see cref="TypeAheadScope"/> is asked again on every keystroke and this walks every unlock
        /// of every dot.
        /// </summary>
        private string SearchText(GuiTechnology2 technology)
        {
            if (technology == null)
            {
                return null;
            }

            string kept;
            if (_searchText.TryGetValue(technology, out kept))
            {
                return kept;
            }

            SearchTextBuilds++;
            string built = BuildSearchText(technology);
            _searchText[technology] = built;
            return built;
        }

        /// <summary>Throw the kept words away when what they were built from can have changed - a new
        /// turn, or a wheel that has been rebound since (its technologies are new objects, which simply
        /// miss the table).</summary>
        private void ExpireSearchText()
        {
            int turn = Turn();
            if (turn != _searchTurn)
            {
                _searchTurn = turn;
                _searchText.Clear();
            }
        }

        private static int Turn()
        {
            try
            {
                Game game = Gui.Game;
                return game == null ? int.MinValue : game.Turn;
            }
            catch (Exception)
            {
                return int.MinValue;
            }
        }

        private static string BuildSearchText(GuiTechnology2 technology)
        {
            MessageBuilder message = new MessageBuilder();
            List<string> terms = new List<string>();
            try
            {
                message.Fragment(AgeText.Clean(technology.Title));
                AddTerms(terms, technology.TechnologyDefinition.GetLocalizedKeywords());
                AddUnlockTerms(terms, technology);
            }
            catch (Exception e)
            {
                Log.Warn("research: building a technology's search words threw: " + e);
            }

            for (int i = 0; i < terms.Count; i++)
            {
                message.ListItemForcedComma(terms[i]);
            }

            return message.Build();
        }

        /// <summary>The words every unlock this technology grants contributes - but only the unlocks
        /// the empire already meets the availability prerequisites for, which is the filter the game's
        /// own search applies, so a search never finds a technology by something it could not yet
        /// give.</summary>
        private static void AddUnlockTerms(List<string> terms, GuiTechnology2 technology)
        {
            IList unlocks = technology.TechnologyUnlocks as IList;
            DepartmentOfScience science = Science();
            Amplitude.Unity.Simulation.SimulationObject empire =
                science == null || science.Empire == null ? null : science.Empire.SimulationObject;
            for (int i = 0; unlocks != null && i < unlocks.Count; i++)
            {
                GuiUnlock unlock = unlocks[i] as GuiUnlock;
                if (unlock == null || unlock.Unlock == null)
                {
                    continue;
                }

                Amplitude.Unity.Framework.IPrerequisiteProvider provider =
                    unlock.Unlock as Amplitude.Unity.Framework.IPrerequisiteProvider;
                if (
                    provider != null
                    && empire != null
                    && !Amplitude.Unity.Framework.PrerequisiteHelper.CheckPrerequisites(
                        empire,
                        provider,
                        ConstructionFlags.UnlockAvailability
                    )
                )
                {
                    continue;
                }

                AddTerm(terms, AgeText.Clean(Localized(unlock.Title)));
                AddTerms(terms, unlock.Unlock.GetLocalizedKeywords());
                AddTerm(terms, TitleOfCategory(unlock.Category));
                AddTerm(terms, TitleOfCategory(unlock.SubCategory));
            }
        }

        private static string Localized(string text)
        {
            try
            {
                return Gui.IsLocalizationKey(text) ? Gui.Localize(text) : text;
            }
            catch (Exception)
            {
                return text;
            }
        }

        private static string TitleOfCategory(string category)
        {
            try
            {
                return string.IsNullOrEmpty(category)
                    ? null
                    : AgeText.Clean(Gui.GetLocalizedTitle(category));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void AddTerms(List<string> terms, IList<string> words)
        {
            for (int i = 0; words != null && i < words.Count; i++)
            {
                AddTerm(terms, words[i]);
            }
        }

        private static void AddTerm(List<string> terms, string word)
        {
            if (string.IsNullOrEmpty(word) || Gui.IsLocalizationKey(word) || terms.Contains(word))
            {
                return;
            }

            terms.Add(word);
        }
    }
}
