using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The dossiers a system carries beyond the ones its children already do: what is in the
    /// ground under it, and the stat block behind the star itself.</summary>
    public sealed partial class GalaxyHudScreen
    {
        /// <summary>
        /// The dossiers the map hangs on a system beyond the ones its children already carry: the
        /// system's own stat block, and one per kind of deposit found in the ground.
        ///
        /// The star, the name and the population count all carry the SAME dossier - one wrapper, three
        /// widgets (measured on Osulo: identical <c>GuiStarSystem</c> target on all three) - so it is
        /// one node, named the way the game's own header names it ("Osulo - Niris"). Which of the two
        /// star tooltips is asked for is <see cref="StarDossier"/>'s rule: the map keeps one on the
        /// label and another over the star once the camera is in, and only the one being drawn has any
        /// words at all.
        ///
        /// Everything else on the label that carries a dossier is already a node here - the planets,
        /// the fleet lozenges, the diplomacy button - so none of them is declared twice.
        /// </summary>
        private static List<TooltipChildren.Dossier> SystemDossiers(
            StarSystemNode node,
            Empire empire,
            StarSystemLabel label
        )
        {
            List<TooltipChildren.Dossier> found = new List<TooltipChildren.Dossier>(4);
            try
            {
                StarSystemNode it = node;
                Empire looking = empire;
                StarSystemLabel drawn = label;
                AgeTooltip star = StarAim(node, empire, label);
                TooltipChildren.Add(
                    found,
                    star,
                    star == null ? null : star.AgeTransform,
                    () => StarDossierLines(it, looking, drawn),
                    // The words were always asked for afresh; the AIM and the header line are asked
                    // the same way now, or the node reads a system the camera has moved on from.
                    () => StarAim(it, looking, LabelFor(it, SystemLabels()))
                );
                // The stat block behind the star is what the PLACE is, so it leads the "Details"
                // region - the first thing the player reaches asking what this system is.
                SystemLabelReadout.In(found, 0, SystemLabelReadout.Region.Details);
                // Then every picture the label is drawing, in its own order, with the deposits back in
                // the place the label draws them (<see cref="SystemLabelReadout.IconsAboveDeposits"/>).
                // Each stamps the region of the row it belongs in as it goes, and the emit reads them
                // back region by region while keying every node by its place in THIS list.
                SystemLabelReadout.IconsAboveDeposits(found, label);
                AddDeposits(found, node, empire, label);
                SystemLabelReadout.IconsBelowDeposits(found, label);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading a system's dossiers threw: " + e);
            }

            return found;
        }

        /// <summary>
        /// One dossier per KIND of deposit in this system's ground, read off the planets rather than
        /// off the icons the label happens to be drawing.
        ///
        /// The label draws its deposit strip only from a close enough camera, so taking the list from
        /// the strip made a system's deposits reachable at one zoom and gone at another - for content
        /// the map is not withholding at all (the fog gates are the planets': everything here is under
        /// <c>MapVisibility.Perceived</c> and the branch's own expansion). The list is built exactly
        /// as <c>StarSystemLabel.RefreshDepositsLine</c> builds it - every planet's deposits in orbit
        /// order, deduped by definition name - so the order the player walks is the order the icons
        /// are drawn in.
        ///
        /// The AIM still prefers the game's own icon wherever the game is drawing one (owner ruling
        /// 2026-08-23), so a sighted player sees the tooltip appear over the deposit it belongs to;
        /// a carrier of the mod's own stands in only where there is no icon on the screen, and the
        /// words are the same either way because the tooltip window assembles them from the wrapper.
        /// A drawn item is matched to the definition it is BOUND to rather than taken by position,
        /// which is also what stops a stale binding on a culled-out label being read.
        /// </summary>
        private static void AddDeposits(
            List<TooltipChildren.Dossier> found,
            StarSystemNode node,
            Empire empire,
            StarSystemLabel label
        )
        {
            ColonizedStarSystem colony = LabelColony(node, empire);
            Empire owner = colony == null ? null : colony.Empire;
            List<ResourceDepositDefinition> kinds = DepositKinds(node);
            StarSystemNode it = node;
            Empire looking = empire;
            for (int i = 0; i < kinds.Count; i++)
            {
                ResourceDepositDefinition definition = kinds[i];
                ResourceDepositDefinition kind = definition;
                AgeTooltip tooltip = DepositAim(node, definition, label, owner);
                int at = found.Count;
                TooltipChildren.Add(
                    found,
                    tooltip,
                    tooltip == null ? null : tooltip.AgeTransform,
                    null,
                    // The label's deposit strip is drawn only from close enough and its items are
                    // pooled among the deposits the label is showing, so which widget carries a kind
                    // is a question about the camera - asked again every time the pointer is aimed
                    // rather than once when the node was declared.
                    () =>
                        DepositAim(
                            it,
                            kind,
                            LabelFor(it, SystemLabels()),
                            DepositOwner(it, looking)
                        )
                );
                if (found.Count > at)
                {
                    ExploitedName(found, at, it, kind);
                    SystemLabelReadout.In(found, at, SystemLabelReadout.Region.Resources);
                }
            }
        }

        /// <summary>Put the state the label paints a deposit's picture in onto that deposit's own node -
        /// exploited or idle, read off the drawn icon at every read, because whether the map is drawing
        /// one at all is a question about where the camera is
        /// (<see cref="SystemLabelReadout.DepositName"/>). The naming ladder underneath is kept: it is
        /// what a sibling entry reads to find out whether the two answer to the same word.</summary>
        private static void ExploitedName(
            List<TooltipChildren.Dossier> found,
            int at,
            StarSystemNode node,
            ResourceDepositDefinition definition
        )
        {
            TooltipChildren.Dossier entry = found[at];
            Func<string> named = entry.Name;
            StarSystemNode it = node;
            ResourceDepositDefinition kind = definition;
            entry.Name = () =>
                SystemLabelReadout.DepositName(
                    named(),
                    DrawnDeposit(LabelFor(it, SystemLabels()), kind)
                );
            found[at] = entry;
        }

        /// <summary>The widget a kind of deposit's dossier is drawn through right now: the label's own
        /// icon wherever the map is drawing one for it, else a carrier of the mod's.</summary>
        private static AgeTooltip DepositAim(
            StarSystemNode node,
            ResourceDepositDefinition definition,
            StarSystemLabel label,
            Empire owner
        )
        {
            bool drawing = label != null && AgeWidgets.Painted(label.AgeTransform);
            AgeTooltip icon = drawing ? DrawnDeposit(label, definition) : null;
            return icon ?? DepositCarrier(node, definition, owner);
        }

        /// <summary>Whose colony the deposits are being read under, which is what a carrier is stamped
        /// with.</summary>
        private static Empire DepositOwner(StarSystemNode node, Empire empire)
        {
            ColonizedStarSystem colony = LabelColony(node, empire);
            return colony == null ? null : colony.Empire;
        }

        /// <summary>Every kind of deposit in a system's ground, in the order the label's strip draws
        /// them: planet by planet, deposit by deposit, one entry per definition NAME
        /// (<c>StarSystemLabel.RefreshDepositsLine</c>).</summary>
        private static List<ResourceDepositDefinition> DepositKinds(StarSystemNode node)
        {
            List<ResourceDepositDefinition> kinds = new List<ResourceDepositDefinition>(4);
            try
            {
                for (int i = 0; i < node.Planets.Count; i++)
                {
                    Planet planet = node.Planets[i];
                    for (int j = 0; j < planet.ResourceDeposits.Count; j++)
                    {
                        ResourceDepositDefinition definition = planet.ResourceDeposits[j].Definition;
                        if (definition == null || Holds(kinds, definition))
                        {
                            continue;
                        }

                        kinds.Add(definition);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: listing a system's deposits threw: " + e);
            }

            return kinds;
        }

        private static bool Holds(
            List<ResourceDepositDefinition> kinds,
            ResourceDepositDefinition definition
        )
        {
            for (int i = 0; i < kinds.Count; i++)
            {
                if (kinds[i].Name == definition.Name)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The label's own icon for one kind of deposit, where it is drawing one. Found by
        /// what the icon is BOUND to, never by position: an icon the label bound for another system
        /// and has not refreshed since answers no.</summary>
        private static AgeTooltip DrawnDeposit(
            StarSystemLabel label,
            ResourceDepositDefinition definition
        )
        {
            if (label == null)
            {
                return null;
            }

            AgeTooltip found = DrawnDeposit(label.DepositsMainTable, definition);
            return found ?? DrawnDeposit(label.DepositsSecondaryTable, definition);
        }

        private static AgeTooltip DrawnDeposit(
            AgeTransform table,
            ResourceDepositDefinition definition
        )
        {
            // Flow control: a table the label is not drawing must not be WALKED for a deposit to read.
            if (!AgeWidgets.Visible(table))
            {
                return null;
            }

            IList<AgeTransform> items = table.Children;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                AgeTransform item = items[i];
                // Content: which icon carries this deposit's sentence. The table pools its items, and a
                // retired one is faded rather than hidden while it still holds the last binding.
                if (!AgeWidgets.Painted(item))
                {
                    continue;
                }

                AgeTooltip tooltip = AgeWidgets.Raw(item);
                GuiResourceDepositGroup group =
                    tooltip == null ? null : tooltip.Target as GuiResourceDepositGroup;
                if (group != null && group.Definition != null
                    && group.Definition.Name == definition.Name)
                {
                    return tooltip;
                }
            }

            return null;
        }

        /// <summary>A carrier of the mod's own bound exactly as <c>StarSystemLabelDepositItem.Bind</c>
        /// binds the game's icon - the same class, the same wrapper, the same refusal text - so the
        /// tooltip window assembles the same panel for it.</summary>
        private static AgeTooltip DepositCarrier(
            StarSystemNode node,
            ResourceDepositDefinition definition,
            Empire owner
        )
        {
            try
            {
                AgeTooltip carrier;
                bool rebind = ScratchTooltips.Rebind(
                    "deposit/" + node.GUID + "/" + definition.Name,
                    DossierStamp(owner),
                    out carrier
                );
                if (rebind && carrier != null)
                {
                    GuiResourceDepositGroup group = new GuiResourceDepositGroup(
                        node,
                        definition,
                        owner
                    );
                    List<FailureInfo> refusals = new List<FailureInfo>();
                    group.IsExploited(PlayerEmpire(), refusals);
                    carrier.Class = group.TooltipClass;
                    carrier.Content = Gui.FormatFailureInfos(refusals);
                    carrier.Context = null;
                    carrier.Target = group;
                }

                return carrier;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: binding a deposit dossier threw: " + e);
                return null;
            }
        }

        /// <summary>What a dossier built from the simulation depends on: the turn it was read in and
        /// whose empire it was read for. Everything a deposit group or a star system counts - what is
        /// exploited, what the empire may exploit at all, who lives there - settles at the turn's end,
        /// and rebinding a carrier more often than that would restart the tooltip's own countdown
        /// every frame and it would never finish appearing.</summary>
        private static long DossierStamp(Empire owner)
        {
            try
            {
                Game game = Gui.Game;
                long stamp = game == null ? 0L : game.Turn * 1000003L;
                return (stamp * 31L) + (owner == null ? 0L : owner.Index + 1L);
            }
            catch (Exception)
            {
                return 0L;
            }
        }

        /// <summary>
        /// The dossier behind the star - what the system IS, in the game's own stat block.
        ///
        /// The map keeps TWO of these for one system and swaps them as the camera moves: the one on the
        /// system's label while the label is what the map draws, and the one the orbital window parks
        /// over the star once the camera is all the way in - at that distance the label is pushed off
        /// the top of the screen. Both are assembled by the tooltip window as it draws them, so only
        /// the one being drawn has any words in it at all.
        ///
        /// Which is why the section asks for whichever is up rather than remembering the label's:
        /// remembering it left the buffer of a system the player had zoomed into holding everything the
        /// LABEL says - what it is building, what is in the ground - and nothing about the system
        /// itself, while the picture on screen showed the dossier the whole time.
        /// </summary>
        private static NodeSection StarDossier(
            StarSystemNode node,
            Empire empire,
            StarSystemLabel label
        )
        {
            AgeTooltip either = StarAim(node, empire, label);
            if (either == null)
            {
                return null;
            }

            StarSystemNode it = node;
            Empire looking = empire;
            StarSystemLabel drawn = label;
            return GraphNodes.TooltipSection(
                either,
                () => StarDossierLines(it, looking, drawn)
            );
        }

        /// <summary>
        /// Which of a system's star dossiers the pointer is put on: the one the orbital window parks
        /// over the star once the camera is in and it says the whole card
        /// (<see cref="OrbitalStarDossier"/>), else the one on the label while the map is drawing the
        /// label, else a carrier of the mod's own bound the way the label binds its
        /// (<c>StarSystemLabel.BindLabelTooltip</c>).
        ///
        /// The third case is what makes a system OFF the screen still readable - the label is culled
        /// and its binding is stale, and reading a stale binding is how a system came to describe the
        /// last place its pooled label was pointed at.
        /// </summary>
        private static AgeTooltip StarAim(
            StarSystemNode node,
            Empire empire,
            StarSystemLabel label
        )
        {
            AgeTooltip orbital = OrbitalStarDossier(node, empire);
            if (orbital != null)
            {
                return orbital;
            }

            AgeTooltip onTheLabel = label == null ? null : label.StarTooltip;
            // Content: which tooltip the star's dossier is - the label's own only while the map is drawing that label.
            if (
                onTheLabel != null
                && AgeWidgets.Painted(label.AgeTransform)
                && AgeWidgets.Draws(onTheLabel)
            )
            {
                return onTheLabel;
            }

            return StarCarrier(node, empire);
        }

        /// <summary>Whichever of a system's star tooltips the game is drawing. One at most can be up,
        /// so the first of them with anything to say is the one on the screen - and the mod's own
        /// carrier is asked last, because it is the one nothing else would have drawn. The orbital
        /// window's is skipped where it would say LESS than the label's
        /// (<see cref="OrbitalStarDossier"/>), which is the same rule <see cref="StarAim"/> aims by,
        /// so what is pointed at and what is read can never be two different cards.</summary>
        private static IList<string> StarDossierLines(
            StarSystemNode node,
            Empire empire,
            StarSystemLabel label
        )
        {
            IList<string> words = TooltipWords(OrbitalStarDossier(node, empire));
            if (words != null && words.Count > 0)
            {
                return words;
            }

            words = TooltipWords(label == null ? null : label.StarTooltip);
            return words != null && words.Count > 0
                ? words
                : TooltipWords(StarCarrier(node, empire));
        }

        /// <summary>The system's own stat block on a carrier of the mod's, bound exactly as
        /// <c>StarSystemLabel.BindLabelTooltip</c> binds the label's: the same class, the same wrapper
        /// as both target AND context, the same content string.</summary>
        private static AgeTooltip StarCarrier(StarSystemNode node, Empire empire)
        {
            try
            {
                ColonizedStarSystem colony = LabelColony(node, empire);
                AgeTooltip carrier;
                bool rebind = ScratchTooltips.Rebind(
                    "star/" + node.GUID,
                    DossierStamp(colony == null ? null : colony.Empire),
                    out carrier
                );
                if (rebind && carrier != null)
                {
                    GuiStarSystem gui = GuiStarSystem.Instantiate(node, colony);
                    carrier.Class = gui.TooltipClass;
                    carrier.Content = gui.TooltipContent;
                    carrier.Context = gui;
                    carrier.Target = gui;
                }

                return carrier;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: binding a system's own dossier threw: " + e);
                return null;
            }
        }

        private static IList<string> TooltipWords(AgeTooltip tooltip)
        {
            Func<IList<string>> lines = AgeWidgets.TooltipLines(tooltip);
            return lines == null ? null : lines();
        }

        /// <summary>
        /// The orbital window's star tooltip where it says the whole card, and nothing where it says
        /// a reduced one - so that a system's dossier is chosen by what it CONTAINS rather than by
        /// which widget the game happens to be drawing.
        ///
        /// The window binds that tooltip with the PLAYER'S OWN colony
        /// (<c>PlanetLabelsWindow_SystemOrbital.OnBeginShow</c> asks the colony repository for
        /// <c>Gui.PlayerEmpire</c> alone), so on a system somebody ELSE has colonised it carries no
        /// colony at all - and the card the tooltip window then assembles from it drops the owner out
        /// of its header ("Osulo" rather than "Osulo - Niris") and leaves the system's defence off
        /// altogether. The map's LABEL binds the same card ownership-blind (<see cref="LabelColony"/>),
        /// so the fuller card exists the whole time; zooming in on a foreign system was simply
        /// swapping it for the thinner one.
        ///
        /// Nothing here keys on the camera. The window is left shown and bound for several steps of
        /// zooming back out (measured 2026-08-25), so a rule that trusted the zoom would still be
        /// reading the thin card at label distance.
        /// </summary>
        private static AgeTooltip OrbitalStarDossier(StarSystemNode node, Empire empire)
        {
            AgeTooltip orbital = OrbitalStarTooltip(node);
            if (orbital == null)
            {
                return null;
            }

            GuiStarSystem gui = orbital.Target as GuiStarSystem;
            bool colonyless = gui == null || gui.ColonizedStarSystem == null;
            return colonyless && LabelColony(node, empire) != null ? null : orbital;
        }

        /// <summary>
        /// The tooltip the orbital window draws on a system's star, which it keeps parked over the
        /// star wherever the star is on screen. Null unless the window is describing THIS system.
        ///
        /// Which system that is, is asked of the tooltip's own binding and never of where the camera
        /// is. The window binds this tooltip once, in <c>PlanetLabelsWindow_SystemOrbital.OnBeginShow</c>,
        /// to the system that was focused THEN - and the game leaves the window shown and bound to the
        /// system the player came from while <c>FocusedStarSystemNode</c> has already moved on
        /// (measured 2026-08-24: window bound to Rigel, focused system Dusay, and it stays that way).
        /// Trusting the camera's answer therefore aimed a system's dossier at a widget carrying its
        /// neighbour's, and the game drew the neighbour's card under the player's cursor for good.
        ///
        /// Declining here costs nothing: the caller falls through to the system's own map label and
        /// then to a carrier of the mod's own, both of which describe the system that was asked about.
        /// </summary>
        private static AgeTooltip OrbitalStarTooltip(StarSystemNode node)
        {
            try
            {
                PlanetLabelsWindow_SystemOrbital window = OrbitalWindow();
                AgeTooltip star = window == null ? null : window.StarTooltip;
                if (star == null || star.AgeTransform == null || !Describes(star, node))
                {
                    return null;
                }

                return star;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Whether a star tooltip is bound to this system - the wrapper the game put on it
        /// names the system its words will be assembled about.</summary>
        private static bool Describes(AgeTooltip star, StarSystemNode node)
        {
            GuiStarSystem gui = star.Target as GuiStarSystem;
            return gui != null && ReferenceEquals(gui.StarSystemNode, node);
        }
    }
}
