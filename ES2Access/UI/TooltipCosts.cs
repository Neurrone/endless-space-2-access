using System;
using System.Collections.Generic;
using Amplitude;
using Amplitude.Extensions;
using Amplitude.Unity.Framework;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// WHAT A CONTROL COSTS, computed the way the game's own tooltip computes it.
    ///
    /// Every price in this game is drawn by a tooltip. A tile, a hull, a module, a technology, a
    /// fleet action and a hacking program all say what they will take in one line of their hover
    /// dossier - and a dossier is only reviewable, so a player walking a panel of tiles heard six
    /// names and no numbers, and had to open the buffer on each to find out which one they could
    /// afford. The price is the reason the control is being looked at, so it belongs in the readout.
    ///
    /// It is read off the game's own data rather than recomposed. WHICH tooltip classes draw a cost
    /// line is a table (<c>GuiTooltipDescription</c>, one entry per class, each naming the little
    /// panel prefabs its window loads), and the line itself comes from interfaces the target
    /// implements - so this asks the table which cost panels a class has, and then makes exactly the
    /// calls that panel's own <c>Bind</c> makes, on the target and context the tooltip carries
    /// (<c>GuiTooltipWindow.DoBind</c> hands the panels those two). What the player hears is
    /// therefore what the panel draws, minus its "Cost:" caption, and it stays right when a bonus
    /// changes a price - nothing here is cached but the class's own answer.
    ///
    /// Five panel kinds draw a cost, and the differences between them are real: a recipe's says only
    /// how many turns, a ship design adds its manpower, a hacking program says whether something is
    /// making it cheaper, and a probe-based fleet action draws a movement-point price BESIDE its
    /// resource one. So the kinds are read in the order the class lists them and each contributes its
    /// own words.
    ///
    /// Main-thread only, like everything that touches the game's databases.
    /// </summary>
    public static class TooltipCosts
    {
        /// <summary>The cost panels a tooltip class draws, in the order its window would lay them
        /// out.</summary>
        private enum Panel
        {
            /// <summary><c>PanelFeatureCosts</c>: the resource price, with the remaining turns in
            /// brackets wherever the game computes them.</summary>
            Costs,

            /// <summary><c>PanelFeatureShipCosts</c>: the same, plus the design's manpower.</summary>
            ShipCosts,

            /// <summary><c>PanelFeatureHackingProgramCosts</c>: the program's price, and the game's
            /// own word for a price something is raising or lowering.</summary>
            HackingProgramCosts,

            /// <summary><c>PanelFeatureRemainingTurnsCostProvider</c>: a turn count alone, which is
            /// the whole of what a recipe's tooltip says it will take.</summary>
            RemainingTurns,

            /// <summary><c>PanelFeatureMovementPointCost</c>: what the order spends off the fleet's
            /// movement, drawn in ADDITION to a resource cost.</summary>
            MovementPoints,
        }

        private static readonly Panel[] None = new Panel[0];

        // Keyed by tooltip class, because that is what the game keys its own tooltip descriptions by
        // and the answer is authored data: the panels a class draws do not change while the game
        // runs. Only the ANSWER is cached - every number is computed when the control is read.
        private static readonly Dictionary<string, Panel[]> Cache =
            new Dictionary<string, Panel[]>();

        /// <summary>
        /// What <paramref name="tooltip"/>'s cost panel would say, resolved when the control is read,
        /// or null when its class draws no cost line at all - which is most of them (36 of the game's
        /// 151 tooltip classes have one).
        ///
        /// The class is settled here, when the section is declared, rather than per read: a graph is
        /// rebuilt from live widgets on every operation, so a pooled widget rebound to another class
        /// is asked again the moment anything moves.
        /// </summary>
        public static Func<string> Of(AgeTooltip tooltip)
        {
            try
            {
                if (tooltip == null)
                {
                    return null;
                }

                Panel[] panels = PanelsFor(tooltip.Class);
                if (panels.Length == 0)
                {
                    return null;
                }

                AgeTooltip it = tooltip;
                Panel[] drawn = panels;
                return () => Text(it, drawn);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Text(AgeTooltip tooltip, Panel[] panels)
        {
            MessageBuilder message = new MessageBuilder();
            try
            {
                object target = tooltip.Target;
                object context = tooltip.Context;
                for (int i = 0; i < panels.Length; i++)
                {
                    Draw(message, panels[i], target, context);
                }
            }
            catch (Exception e)
            {
                Log.Warn("tooltip: reading a cost panel threw: " + e);
            }

            return message.Build();
        }

        private static void Draw(
            MessageBuilder message,
            Panel panel,
            object target,
            object context
        )
        {
            switch (panel)
            {
                case Panel.Costs:
                    message.ListItem(Say(Costs(target, context)));
                    break;
                case Panel.ShipCosts:
                    message.ListItem(Say(Costs(target, context)));
                    message.ListItem(Say(Manpower(target)));
                    break;
                case Panel.HackingProgramCosts:
                    message.ListItem(Say(HackingCosts(target, context)));
                    message.ListItem(Say(HackingAlteration(target, context)));
                    break;
                case Panel.RemainingTurns:
                    message.ListItem(Say(RemainingTurns(target, context)));
                    break;
                case Panel.MovementPoints:
                    message.ListItem(Say(MovementPoints(target)));
                    break;
            }
        }

        // The game writes its cost lines in markup - colour tags around a number, an icon standing in
        // for the resource's name - and the reading of that is the same one every other piece of the
        // game's text gets.
        private static string Say(string drawn)
        {
            return AgeText.Clean(drawn);
        }

        /// <summary><c>PanelFeatureCosts.Bind</c>: the target's own cost string, and the turns the
        /// treasury computes for it where the context is somewhere that builds things.</summary>
        private static string Costs(object target, object context)
        {
            IFinalCostsProvider provider = target as IFinalCostsProvider;
            ICostResolutionContext resolution = context as ICostResolutionContext;
            if (provider == null || resolution == null || resolution.Empire == null)
            {
                return null;
            }

            string costs = provider.GetFinalCostsString(resolution);
            if (string.IsNullOrEmpty(costs))
            {
                return null;
            }

            IFinalCostsProviderExtended extended = provider as IFinalCostsProviderExtended;
            if (extended != null && !extended.CanAddTurnsToFinalCostsString(resolution))
            {
                return costs;
            }

            int turns = PanelFeatureRemainingTurnsCostProvider.ComputeTurns(
                context,
                target as ICostProvider,
                resolution,
                target as IPrerequisiteProvider,
                target as IMetaPrerequisiteProvider
            );
            return turns > 1000 || turns <= 0
                ? costs
                : string.Format(PanelFeatureCosts.CostWithTurnsFormat, costs, turns.ToString());
        }

        /// <summary><c>PanelFeatureShipCosts.Bind</c>'s second label.</summary>
        private static string Manpower(object target)
        {
            IShipInfoProvider ship = target as IShipInfoProvider;
            return ship == null
                ? null
                : string.Format("{0} [manPower]", ship.GetStatValue("ShipStatManpowerOptionalCost"));
        }

        /// <summary><c>PanelFeatureHackingProgramCosts.Bind</c>: a program's price comes off its own
        /// provider, never the general one.</summary>
        private static string HackingCosts(object target, object context)
        {
            IHackingProgramCostsProvider provider = target as IHackingProgramCostsProvider;
            ICostResolutionContext resolution = context as ICostResolutionContext;
            return provider == null || resolution == null || resolution.Empire == null
                ? null
                : provider.GetFinalCostsString(resolution);
        }

        /// <summary>The game's own word for a program whose price something is raising or lowering -
        /// the second label of the same panel, drawn only while there is an alteration.</summary>
        private static string HackingAlteration(object target, object context)
        {
            IHackingProgramCostsProvider provider = target as IHackingProgramCostsProvider;
            ICostResolutionContext resolution = context as ICostResolutionContext;
            if (provider == null || resolution == null || resolution.Empire == null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(provider.GetFinalCostsString(resolution)))
            {
                return null;
            }

            float alteration = provider.GetCostAlteration(resolution);
            if (alteration == 0f)
            {
                return null;
            }

            return Gui.Localize(
                alteration > 0f
                    ? "%PanelFeatureHackingProgramCostsMalusTitle"
                    : "%PanelFeatureHackingProgramCostsBonusTitle"
            );
        }

        /// <summary><c>PanelFeatureRemainingTurns.Bind</c> over
        /// <c>PanelFeatureRemainingTurnsCostProvider</c>'s turns: the panel hides itself on a negative
        /// count, and formats through the same call the construction queue's own turn label uses. Its
        /// format string is private to the panel, so the format is written out here.</summary>
        private static string RemainingTurns(object target, object context)
        {
            int turns = PanelFeatureRemainingTurnsCostProvider.ComputeTurns(
                context,
                target as ICostProvider,
                context as ICostResolutionContext,
                target as IPrerequisiteProvider,
                target as IMetaPrerequisiteProvider
            );
            return turns < 0
                ? null
                : string.Format("{0} [turnColored]", ConstructionLine.FormatNumberOfTurns(turns));
        }

        /// <summary><c>PanelFeatureMovementPointCost.Bind</c>.</summary>
        private static string MovementPoints(object target)
        {
            IMovementPointCostProvider provider = target as IMovementPointCostProvider;
            return provider == null
                ? null
                : string.Format(
                    Gui.Localize("%MovementPointCostFormat"),
                    provider.MovementPointCost.ToString(1)
                );
        }

        /// <summary>Which cost panels the game's tooltip table gives this class, in its own order. An
        /// unknown class, a class with no entry, and a class whose entry names none of the five all
        /// answer the same empty list - and it is cached, so a class is looked up once however many
        /// controls point at it.</summary>
        private static Panel[] PanelsFor(string tooltipClass)
        {
            // The empty class is the plain text box, whose words are on the widget: the same name the
            // rest of the mod gives it, so one cache entry covers both spellings.
            string name = string.IsNullOrEmpty(tooltipClass) ? "Simple" : tooltipClass;
            Panel[] panels;
            if (Cache.TryGetValue(name, out panels))
            {
                return panels;
            }

            panels = Read(name);
            Cache[name] = panels;
            return panels;
        }

        private static Panel[] Read(string name)
        {
            try
            {
                IDatabase<Amplitude.Unity.Gui.GuiTooltipDescription> database =
                    Databases.GetDatabase<Amplitude.Unity.Gui.GuiTooltipDescription>(false);
                Amplitude.Unity.Gui.GuiTooltipDescription description =
                    database == null ? null : database.GetValue(new StaticString(name));
                Amplitude.Unity.Gui.GuiPanelFeatureDescription[] features =
                    description == null ? null : description.PanelFeaturesDescriptions;
                if (features == null)
                {
                    return None;
                }

                List<Panel> found = null;
                for (int i = 0; i < features.Length; i++)
                {
                    if (features[i] == null)
                    {
                        continue;
                    }

                    Panel panel;
                    if (!Known(features[i].Prefab, out panel))
                    {
                        continue;
                    }

                    if (found == null)
                    {
                        found = new List<Panel>(2);
                    }

                    found.Add(panel);
                }

                return found == null ? None : found.ToArray();
            }
            catch (Exception e)
            {
                Log.Warn("tooltip: reading the cost panels of '" + name + "' threw: " + e);
                return None;
            }
        }

        /// <summary>A prefab path names its panel class in its last segment, and only the five that
        /// draw a price are of interest.</summary>
        private static bool Known(string prefab, out Panel panel)
        {
            panel = Panel.Costs;
            if (string.IsNullOrEmpty(prefab))
            {
                return false;
            }

            int slash = prefab.LastIndexOf('/');
            string cls = slash < 0 ? prefab : prefab.Substring(slash + 1);
            switch (cls)
            {
                case "PanelFeatureCosts":
                    panel = Panel.Costs;
                    return true;
                case "PanelFeatureShipCosts":
                    panel = Panel.ShipCosts;
                    return true;
                case "PanelFeatureHackingProgramCosts":
                    panel = Panel.HackingProgramCosts;
                    return true;
                case "PanelFeatureRemainingTurnsCostProvider":
                    panel = Panel.RemainingTurns;
                    return true;
                case "PanelFeatureMovementPointCost":
                    panel = Panel.MovementPoints;
                    return true;
                default:
                    return false;
            }
        }
    }
}
