using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The short list a planet card puts up when you ask for work on the planet: which improvement to
    /// build on it, which type to terraform it into, or which of its anomalies to reduce.
    ///
    /// It is the second half of the card's build buttons. Pressing one does not order anything - it
    /// slides this panel out from under the card, and the ORDER is placed by picking a line in it
    /// (<c>PlanetConstructiblePanel.OnClickConstructibleItem</c>, PlanetConstructiblePanel.cs:314-357).
    /// Without a screen here, a player who chose one of those entries from the card's menu would hear
    /// nothing at all and be sitting in front of a list they could not reach.
    ///
    /// It is a PANEL, not a window, and there are THREE of it: the game gives one to each host that
    /// draws planet cards with build buttons on them - <c>PlanetLabelsWindow_SystemOrbital</c> (:11,
    /// shown at :124-161), <c>PlanetLabelsWindow_SystemManagement</c> (:12, shown at :155-171), and the
    /// EMPIRE page's own cards panel (<c>EmpireScreen.StarSystemsManagementPanel</c> :28 →
    /// <c>StarSystemPlanetCardsPanel.ConstructiblePanel</c> :15, opened by
    /// <c>OnClickBuildInfrastructure</c> :278-288) - and moves it every frame to stay under the card's
    /// button row. They are different objects, at most one of them is up (each host hides its own as it
    /// shows and as it hides, the two label windows are the two views of one star system screen, and
    /// the empire page's cards panel is hidden the moment another table row or cell is clicked), so the
    /// screen answers with whichever is drawing a planet.
    ///
    /// They differ in what the panel is bound to, which is what a line can SAY:
    /// - the management card and the empire page's card both bind the colonized planet
    ///   (<c>BindPlanet(planet, mode)</c>, <c>StarSystemPlanetCardsPanel.ShowConstructiblePanel</c>
    ///   :255-271) in all three modes, so a line carries the planet's own title and the game may REFUSE
    ///   it, filling the item's failure list with the reason
    ///   (<c>StarSystemConstructibleItem.RefreshContent</c> :169-194);
    /// - the orbital card binds a Behemoth's fleet action (<c>BindPlanet(provider, planet, mode)</c>),
    ///   which forces every line enabled and makes the game draw the constructible's ALT title
    ///   (<c>RefreshConstructibleItem</c> :307-310 passes both flags together).
    ///
    /// Escape is claimed. Nothing in the game closes this panel with a key - it is dismissed by
    /// clicking its button again, or it closes itself once an order has been placed - so Escape would
    /// otherwise sail past it into the pause menu while the panel stayed on screen. Closing goes
    /// through the game's own route, the message every one of its close paths sends to the host that
    /// owns it - the panel's own <c>Client</c>, which each host sets to itself as it loads
    /// (<c>StarSystemPlanetCardsPanel.Load</c> :49, and it handles <c>OnCloseConstructiblePanel</c>
    /// itself at :314-316), so one route serves all three.
    ///
    /// The management and empire routes are base-game reachable and are where this screen is tested.
    /// The ORBITAL route still needs a Behemoth in the system with the matching fleet action available
    /// (<c>PlanetLabel_SystemOrbital.RefreshTerraformationStatus</c> :785-856,
    /// <c>RefreshAnomalyReductionStatus</c> :940-1010, both of which return before making the button
    /// visible when no fleet offers the action), which no fixture has - so the alt-title and
    /// forced-enabled branches above are read from the game's code, not measured.
    /// </summary>
    public sealed class PlanetConstructiblesScreen : Screen
    {
        /// <summary>Reused across builds rather than allocated per frame: Build runs every tick.
        /// </summary>
        private readonly List<Cell> _cells = new List<Cell>();

        public override string Key
        {
            get { return "screen.planet-constructibles"; }
        }

        /// <summary>Above whichever page the card belongs to - the galaxy at the orbital step and the
        /// system's management page, both at layer 10, and the empire page at 15: the panel covers part
        /// of it and takes the keyboard - and below the tutorial popup and everything else that can
        /// appear on top of them.</summary>
        public override int Layer
        {
            get { return 20; }
        }

        /// <summary>The panel's own heading, which the game writes to say which of the two questions it
        /// is asking - "Terraform to", "Reduce Anomaly".</summary>
        public override string ScreenName
        {
            get
            {
                PlanetConstructiblePanel panel = Panel();
                return panel == null ? null : AgeText.Label(panel.TitleLabel);
            }
        }

        /// <summary>Ours while the game is drawing the panel and it still has the planet it was opened
        /// for. The planet is what the panel drops last when it is dismissed
        /// (<c>PlanetConstructiblePanel.OnEndHide</c> → <c>UnbindPlanet</c>), so it outlives the
        /// shown flag through the fade and keeps the screen from blinking out mid-transition.
        ///
        /// And only once the game has SWITCHED THE PANEL ON. It is shown and drawn for about seven
        /// frames before that (measured 2026-08-24), and every line on it reads as refused for as long
        /// as its panel does - so a page taken over during those frames announced its first line
        /// "unavailable" when nothing was refusing it, and the correction is silent because the
        /// corrected state is an EMPTY part. Waiting is half a second of the game's own animation.
        /// </summary>
        public override bool IsActive()
        {
            try
            {
                PlanetConstructiblePanel panel = Panel();
                return panel != null
                    && panel.Planet != null
                    && panel.Shown
                    // Input routing, not node existence: whether the popup is the thing keys go to.
                    && AgeWidgets.Visible(panel.AgeTransform)
                    && AgeWidgets.Operable(panel.AgeTransform);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Escape dismisses the list, leaving the card it belongs to alone.</summary>
        public override bool Back()
        {
            Close();
            return true;
        }

        public override bool ConsumesBack
        {
            get { return true; }
        }

        public override void Build(GraphBuilder builder)
        {
            PlanetConstructiblePanel panel = Panel();
            if (panel == null || panel.ConstructibleTable == null)
            {
                return;
            }

            _cells.Clear();
            try
            {
                StarSystemConstructibleItem[] items =
                    panel.ConstructibleTable.GetComponentsInChildren<StarSystemConstructibleItem>(
                        true
                    );
                for (int i = 0; i < items.Length; i++)
                {
                    Add(_cells, items[i], panel);
                }
            }
            catch (Exception e)
            {
                Core.Util.Log.Warn("planet constructibles: reading the list threw: " + e);
                return;
            }

            // In the order they are drawn, which for a table the game arranges is top to bottom - one
            // graph row per drawn band, and the game draws this list one line to a band.
            Cells.Emit(builder, _cells);
        }

        /// <summary>One line of the list: what would be built, what it would cost and how long it
        /// would take - the tooltip the game hangs on the line - and the game's own reasons where it
        /// is refusing. Choosing it replays the line's own click, which is what places the order.
        ///
        /// The refusal reasons are the same reading the star system's constructible grid gives the same
        /// prefab (<see cref="SystemPanels.ConstructibleFailures"/>): the game collects them onto the
        /// item as it works out whether to offer it, and read from there they are in the buffer the
        /// moment focus lands rather than only once the tooltip window has drawn its failure panel. On
        /// the orbital route every line is force-enabled and the list is always empty; on the
        /// management route it is the whole of what a blocked line has to say.
        /// </summary>
        private static void Add(
            List<Cell> cells,
            StarSystemConstructibleItem item,
            PlanetConstructiblePanel panel
        )
        {
            if (item == null)
            {
                return;
            }

            IGuiConstructible constructible = item.GuiConstructible;
            if (constructible == null)
            {
                return;
            }

            StarSystemConstructibleItem it = item;
            bool alt = panel != null && panel.OngoingFleetActionProvider != null;
            AgeTooltip tooltip = AgeWidgets.Raw(item.AgeTransform);
            Func<IList<string>> drawn = GraphNodes.TooltipDetails(tooltip);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => Name(it, alt)),
                    GraphNodes.DisabledPart(() => AgeWidgets.Operable(it.AgeTransform)),
                },
                Sections = GraphNodes.Sections(
                    GraphNodes.TooltipSection(tooltip),
                    NodeSection.Buffer(() => SystemPanels.ConstructibleFailures(it, drawn))
                ),
                OnActivate = () => Choose(it),
            };
            // The line's tooltip is the renderer-assembled kind, so it is only indicated - and a line
            // the game is refusing would then say "unavailable" and nothing else. The reason is read
            // off the wrapper the tooltip carries, as its failure panel does.
            GraphNodes.AddRefusal(vtable, tooltip, () => AgeWidgets.Operable(it.AgeTransform));
            AgeWidgets.PointAt(vtable, item.AgeTransform);
            Cells.Add(
                cells,
                item.AgeTransform,
                ControlId.For(item, "planet-constructible/" + constructible.Name),
                vtable
            );
        }

        /// <summary>The line's full name. The panel clips its caption to the width of the card's button
        /// row and truncates what will not fit, so the name comes from what the line is FOR.
        ///
        /// <paramref name="alt"/> is the game's own choice of which of the two titles it DREW: a panel
        /// bound to a Behemoth's fleet action writes the constructible's alt title on every line
        /// (<c>PlanetConstructiblePanel.RefreshConstructibleItem</c> :307-310 into
        /// <c>StarSystemConstructibleItem.RefreshContent</c> :133), which is how the same terraforming
        /// definition is called when a ship is doing it rather than the colony. The ordinary title is
        /// the fallback, because the game leaves an alt title empty where it has nothing else to call
        /// the thing (<c>GuiWrapper.AltTitle</c> :90-104).</summary>
        private static string Name(StarSystemConstructibleItem item, bool alt)
        {
            try
            {
                IGuiConstructible constructible = item.GuiConstructible;
                string name = alt ? AgeText.Clean(Gui.Localize(constructible.AltTitle)) : null;
                return string.IsNullOrEmpty(name)
                    ? AgeText.Clean(Gui.Localize(constructible.Title))
                    : name;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Place the order the way a click on the line places it - the button's own handler,
        /// which is where the game builds the action context and posts the order.</summary>
        private static void Choose(StarSystemConstructibleItem item)
        {
            try
            {
                if (AgeWidgets.Operable(item.AgeTransform))
                {
                    AgeWidgets.Press(item.Button);
                }
            }
            catch (Exception e)
            {
                Core.Util.Log.Warn("planet constructibles: choosing a line threw: " + e);
            }
        }

        /// <summary>Dismiss the panel through the game's own route: the message its owner listens for,
        /// which every one of the panel's own close paths sends (PlanetConstructiblePanel.cs:336, 367,
        /// 373, 378, 383).</summary>
        private static void Close()
        {
            try
            {
                PlanetConstructiblePanel panel = Panel();
                if (panel != null && panel.Client != null)
                {
                    panel.Client.SendMessage(
                        "OnCloseConstructiblePanel",
                        UnityEngine.SendMessageOptions.DontRequireReceiver
                    );
                }
            }
            catch (Exception e)
            {
                Core.Util.Log.Warn("planet constructibles: closing the panel threw: " + e);
            }
        }

        /// <summary>
        /// Whichever of the three owners is holding a planet in its constructible panel.
        ///
        /// Asked of all three every time rather than remembered: the player moves between the system's
        /// orbital view, its management view and the empire page's planet cards without this screen
        /// being involved, and each of them drives its OWN copy of the panel. Only one of them can be
        /// up - each planet-label window hides its copy as it shows and as it hides
        /// (<c>PlanetLabelsWindow_SystemManagement.OnBeginShow</c> :105-110 and <c>OnBeginHide</c>
        /// :112-116, the same pair on the orbital window), and the empire page's copy lives on a panel
        /// the star system table hides whenever another row or cell is clicked
        /// (<c>StarSystemsManagementPanel.OnLineSelection</c> :286-289) - so the first one still bound
        /// to a planet is the answer, and the shown one is preferred over one that is only fading out
        /// with its planet still attached.
        /// </summary>
        private static PlanetConstructiblePanel Panel()
        {
            try
            {
                if (!Gui.GuiServiceAvailable)
                {
                    return null;
                }

                PlanetLabelsWindow_SystemOrbital orbital =
                    Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemOrbital>(false);
                PlanetLabelsWindow_SystemManagement management =
                    Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemManagement>(false);
                global::EmpireScreen empire =
                    Gui.GuiService.GetWindow<global::EmpireScreen>(false);
                StarSystemsManagementPanel systems =
                    empire == null ? null : empire.StarSystemsManagementPanel;
                StarSystemPlanetCardsPanel cards =
                    systems == null ? null : systems.StarSystemPlanetCardsPanel;
                PlanetConstructiblePanel first = orbital == null ? null : orbital.ConstructiblePanel;
                PlanetConstructiblePanel second =
                    management == null ? null : management.ConstructiblePanel;
                PlanetConstructiblePanel third = cards == null ? null : cards.ConstructiblePanel;
                return Showing(first)
                    ?? Showing(second)
                    ?? Showing(third)
                    ?? Bound(first)
                    ?? Bound(second)
                    ?? Bound(third)
                    ?? first
                    ?? second
                    ?? third;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The panel if the game is drawing it with a planet in it - what
        /// <see cref="IsActive"/> is about to ask of whichever panel this answers with.</summary>
        private static PlanetConstructiblePanel Showing(PlanetConstructiblePanel panel)
        {
            return Bound(panel) != null && panel.Shown ? panel : null;
        }

        /// <summary>The panel if it still has a planet in it - which outlives the shown flag through
        /// the dismissal fade, and is what keeps a panel on its way out from being mistaken for the
        /// other view's.</summary>
        private static PlanetConstructiblePanel Bound(PlanetConstructiblePanel panel)
        {
            return panel != null && panel.Planet != null ? panel : null;
        }
    }
}
