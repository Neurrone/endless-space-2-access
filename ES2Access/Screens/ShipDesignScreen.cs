using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// The ship designer: the window the Military screen's Create and Edit buttons open, and the one
    /// place a design is put together before it is built.
    ///
    /// The window itself is only a frame. Its heading is two labels the game writes side by side - the
    /// word "Ship Design:" and either the design's name or "creating" - and its bottom edge is a row of
    /// buttons whose set changes with what the window was opened for: Close and Auto Design always,
    /// Create Design while a new design is being made, Reset and Apply while an existing one is being
    /// edited, and - for a design the game will not let the player touch at all - a sentence saying why
    /// and a button that opens the newest design of the same family instead. Every one of those is
    /// declared from what the window is DRAWING, which is the game's own answer to what is on offer
    /// (<c>RefreshButtons</c> :187-195 shows and hides them and writes each refusal onto its tooltip).
    ///
    /// Everything between those two is the <c>ShipDesignEditionPanel</c> prefab, and it is read by
    /// <see cref="ShipDesignRows"/> rather than here, because a hero's inspection window hosts the same
    /// panel on its ship page. What a module row's keys do, why the slots and the module list linearise
    /// and how the carry commits are all documented there.
    ///
    /// There is no screen name: the window's heading is a drawn element with its own explanation on its
    /// tooltip, so it is declared where it is drawn and focus lands on it - which says what has just
    /// opened, once, instead of saying it as a screen name and then again as a control.
    ///
    /// Escape is the game's. The window is an input handler of its own and answers Exit by closing -
    /// behind its own "you will lose your changes" box whenever the design has been touched
    /// (<c>HandleInput</c> :59-74), which is the message-box screen and speaks for free.
    /// </summary>
    public sealed class ShipDesignScreen : Screen
    {
        /// <summary>The prefix the shared reader keys this window's ids and stops under.</summary>
        private const string Keys = "shipdesign";

        private static readonly object HeadingStop = Keys + "/heading";
        private static readonly object ActionsStop = Keys + "/actions";

        private readonly TextFieldEditor _editor = new TextFieldEditor();

        // Reused across builds rather than allocated per frame: Build runs every tick.
        private readonly List<Cell> _cells = new List<Cell>();

        public override string Key
        {
            get { return "screen.ship-design"; }
        }

        /// <summary>Over the Military screen that opens it, and under everything its own controls can
        /// raise: the hull drop list at 70 and the lose-changes box at 100.</summary>
        public override int Layer
        {
            get { return 35; }
        }

        /// <summary>The heading, because it is drawn first and Tab does not wrap.</summary>
        public override object InitialFocusStop
        {
            get { return HeadingStop; }
        }

        /// <summary>Escape is the game's: the window closes itself, behind its own confirmation when
        /// the design has been changed.</summary>
        public override bool ConsumesBack
        {
            get { return false; }
        }

        /// <summary>False while the name box has been asked for and the keyboard has not changed hands
        /// yet: what the player types next belongs in the box, not in a search.</summary>
        public override bool CapturesRawInput
        {
            get { return _editor.Pending; }
        }

        public override bool IsActive()
        {
            try
            {
                ShipDesignModalWindow window = Window();
                if (window == null || !window.Shown || !window.IsReady)
                {
                    return false;
                }

                // The window keeps its design only while it is bound: the panel is unbound at
                // begin-hide, and a panel with no design draws nothing worth declaring.
                ShipDesignEditionPanel panel = Panel(window);
                return panel != null && panel.GuiShipDesign != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>False for the frames the game has greyed the window out while raising something over
        /// it - the lose-your-changes box Close puts up, or the hull drop list - during which every
        /// control here reads unavailable at once (measured: <c>Operable</c> on the window's own transform
        /// goes false while the box is up, and the button the player pressed said "unavailable" as the box
        /// arrived).</summary>
        public override bool IsWorkable
        {
            get
            {
                ShipDesignModalWindow window = Window();
                return window != null && AgeWidgets.Operable(window.AgeTransform);
            }
        }

        public override void OnPop()
        {
            _editor.Cancel();
        }

        public override void OnUpdate()
        {
            _editor.Update();
        }

        public override void Build(GraphBuilder builder)
        {
            ShipDesignModalWindow window = Window();
            ShipDesignEditionPanel panel = Panel(window);
            if (window == null || panel == null)
            {
                return;
            }

            try
            {
                BuildHeading(builder, window);
                ShipDesignRows.Build(builder, panel, Keys, _editor, _cells);
                BuildActions(builder, window);
            }
            catch (Exception e)
            {
                Log.Warn("ship design: reading the window threw: " + e);
            }
        }

        /// <summary>The heading, as one node rather than two: the game splits it across a fixed label
        /// and the design's name, drawn side by side at the top of the window, and it is one line to
        /// read.</summary>
        private void BuildHeading(GraphBuilder builder, ShipDesignModalWindow window)
        {
            AgeTransform name =
                window.TitleShipDesignName == null ? null : window.TitleShipDesignName.AgeTransform;
            if (name == null || !AgeWidgets.Visible(name))
            {
                return;
            }

            AgeTransform title = Heading(window);
            AgeTooltip tooltip = AgeWidgets.Raw(name) ?? AgeWidgets.Raw(title);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => HeadingText(title, name)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, name);
            builder.BeginStop(HeadingStop);
            builder.AddItem(ControlId.Referenced(name, Keys + "/title"), vtable);
        }

        private static string HeadingText(AgeTransform title, AgeTransform name)
        {
            Core.Speech.MessageBuilder message = new Core.Speech.MessageBuilder();
            message.Fragment(title == null ? null : AgeWidgets.TextOf(title));
            message.Fragment(AgeWidgets.TextOf(name));
            return message.Build();
        }

        /// <summary>The fixed half of the heading, which the window does not expose - the label beside
        /// the design's name.</summary>
        private static AgeTransform Heading(ShipDesignModalWindow window)
        {
            return AgeWidgets.ChildNamed(window.AgeTransform, "WindowTitle", 3);
        }

        /// <summary>
        /// The row of buttons along the bottom, in the order they are drawn - which is also the order
        /// the game lays them out in its own prefab, so the band is read left to right whichever of
        /// them are up.
        ///
        /// The band is walked rather than named field by field because which buttons exist is the
        /// question, and the game answers it by hiding them. The one thing in the band that is not a
        /// button is the sentence the window writes for a design it will not edit, which is declared
        /// as the line of text it is.
        ///
        /// One button per row, not the columns the window lays them out in: they are peers of one kind
        /// and which of them the game happened to draw side by side is a fact about the window's width
        /// (ui-navigation's roster-grid rule, the same reading the module strip and the costs get). The
        /// order across the band is still the drawn one.
        /// </summary>
        private void BuildActions(GraphBuilder builder, ShipDesignModalWindow window)
        {
            AgeTransform band = Band(window);
            IList<AgeTransform> children = band == null ? null : band.Children;
            if (children == null)
            {
                return;
            }

            _cells.Clear();
            for (int i = 0; i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child == null || !AgeWidgets.Visible(child))
                {
                    continue;
                }

                if (AgeWidgets.Button(child) != null)
                {
                    Cells.AddControl(_cells, child, Keys + "/button/" + child.name);
                    continue;
                }

                Cells.AddReadout(_cells, child, Keys + "/note/" + child.name);
            }

            if (_cells.Count == 0)
            {
                return;
            }

            builder.BeginStop(ActionsStop);
            ShipDesignRows.EmitLinear(builder, _cells);
        }

        private static AgeTransform Band(ShipDesignModalWindow window)
        {
            try
            {
                AgeTransform button = AgeWidgets.Transform(window.ApplyDesignButton);
                return button == null ? null : button.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The body the window instantiates from a prefab at load time. Asked of the window's
        /// own subtree rather than of the scene: the same panel is hosted by the hero inspection
        /// window, and there are two of them alive at once.</summary>
        private static ShipDesignEditionPanel Panel(ShipDesignModalWindow window)
        {
            try
            {
                return window == null
                    ? null
                    : window.GetComponentInChildren<ShipDesignEditionPanel>(true);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static ShipDesignModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<ShipDesignModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Where this screen is drawn, for the tooltip audit (see
        /// <see cref="ES2Access.Screens.Screen.RootTransform"/>).</summary>
        public override AgeTransform RootTransform
        {
            get { return RootOf(Window()); }
        }

        /// <summary>What every node this screen declares is keyed under, so the audit can
        /// tell its content from the shared heads-up display stops.</summary>
        public override string NodePrefix
        {
            get { return "shipdesign"; }
        }
    }
}
