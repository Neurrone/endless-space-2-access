using System;
using System.Collections.Generic;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// The political parties named INSIDE a population's dossier, each as a dossier of its own.
    ///
    /// A population's tooltip ends with a "Political Opinion" block naming the parties that people
    /// leans towards (<c>PanelFeaturePoliticalOpinion</c>), and each of those names carries its own
    /// tooltip - the party's dossier. For a mouse that is one more hover; for us it is unreachable,
    /// and measured to be unreachable rather than merely unimplemented: the game keeps ONE tooltip
    /// window, so pointing at the inner name replaces the population tooltip that was drawing it,
    /// which makes <c>PanelFeaturePoliticalOpinion.Unbind</c> release the very data the inner tooltip
    /// was going to be drawn from. Probed live 2026-08-22: the pointer moves, the window empties, and
    /// <c>DevProbe.Tooltip()</c> answers shown false.
    ///
    /// So the party's page is read the way the game's own panel features read it - off the wrappers,
    /// with no drawing involved. The three features the <c>Politics</c> tooltip class is made of
    /// (header, description, affecting events) are three properties of <c>GuiPolitics</c>, and the
    /// parties themselves come off the same <c>IPoliticalOpinionProvider</c> the drawn block reads.
    /// Every word is the game's.
    ///
    /// The nodes still point at the POPULATION's tooltip, so the picture on screen stays the page the
    /// player is reading their way down - the political opinion block included.
    /// </summary>
    public static class PoliticsDossier
    {
        /// <summary>One dossier per party the population's own tooltip names, in the order its block
        /// draws them. Empty for a tooltip whose target has no political opinion at all, which is
        /// every tooltip that is not a population's.</summary>
        public static List<TooltipChildren.Dossier> Parties(AgeTooltip tooltip)
        {
            List<TooltipChildren.Dossier> found = new List<TooltipChildren.Dossier>(2);
            try
            {
                IPoliticalOpinionProvider opinions =
                    tooltip == null ? null : tooltip.Target as IPoliticalOpinionProvider;
                PopulationDefinition.PopulationPoliticsDefinition[] parties =
                    opinions == null ? null : opinions.PopulationPoliticsDefinitions;
                for (int i = 0; parties != null && i < parties.Length; i++)
                {
                    GuiPolitics politics = Wrapper(parties[i]);
                    if (politics == null)
                    {
                        continue;
                    }

                    GuiPolitics it = politics;
                    AgeTooltip parent = tooltip;
                    found.Add(
                        new TooltipChildren.Dossier
                        {
                            Name = () => AgeText.Clean(it.Title),
                            Lines = () => Lines(it),
                            Aim = parent,
                            Anchor = parent.AgeTransform,
                        }
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("politics: reading a population's political opinion threw: " + e);
            }

            return found;
        }

        private static GuiPolitics Wrapper(PopulationDefinition.PopulationPoliticsDefinition party)
        {
            try
            {
                return party == null || party.PoliticsReference == null
                    ? null
                    : Gui.GuiWrapperProviderService.GetGuiPolitics(party.PoliticsReference.Name);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The party's page, in the order the game's own <c>Politics</c> tooltip draws it:
        /// what it is called, what kind of thing it is, what it is, and what moves it.</summary>
        private static IList<string> Lines(GuiPolitics politics)
        {
            List<string> lines = new List<string>(4);
            try
            {
                Add(lines, politics.Title);
                Add(lines, politics.CategoryTitle);
                Add(lines, politics.Description);
                Add(lines, Gui.Localize(politics.PoliticsAffectingEvents));
            }
            catch (Exception e)
            {
                Log.Warn("politics: reading a party's dossier threw: " + e);
            }

            return lines;
        }

        private static void Add(List<string> lines, string text)
        {
            // Every one of these is raw game text - a colour run round the party's name, an icon token
            // in the middle of a sentence - so it goes through the same cleaner every other reading
            // does before it is split into lines.
            IList<string> said = AgeText.Lines(AgeText.Clean(text));
            for (int i = 0; said != null && i < said.Count; i++)
            {
                if (!string.IsNullOrEmpty(said[i]) && !lines.Contains(said[i]))
                {
                    lines.Add(said[i]);
                }
            }
        }
    }
}
