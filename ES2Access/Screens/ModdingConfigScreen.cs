using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

// The game has its own ModdingScreen in the global namespace; this adapts it, so the two names have to
// coexist.
using GameModdingScreen = ModdingScreen;

namespace ES2Access.Screens
{
    /// <summary>
    /// The mod manager (<c>ModdingScreen</c>): the library of mods down the left, what the selected one is
    /// down the right, the log under that, and the buttons that act on the lot.
    ///
    /// Three panels and two bands, declared in the order they are drawn. Each panel is entered under the
    /// heading the game wrote on it ("Mods Library", "Selected Mod Info", "Log Messages"), read from the
    /// panel's own <c>PanelTitle</c> so the words are the game's.
    ///
    /// A LIBRARY ROW is the game's two independent switches on one line, and they are not the same
    /// question: the SELECTION toggle decides which mod the info panel is describing (one at a time - it is
    /// the game's select-then-look), and the ACTIVATION toggle decides whether the mod is in the
    /// configuration Confirm would load. So the row is a selection node named by the mod's title, with the
    /// activation tick beside it and, for a local mod while Steam is up, the publish button the game draws
    /// at the end. The order number the game paints on an activated row is its place in the load order and
    /// is said as part of the row, because that is the only thing on screen that expresses it.
    ///
    /// THE INFO PANEL IS ALPHA-SWITCHED, not hidden: the game fades <c>MainContent</c> and
    /// <c>NoSelectionContent</c> past each other (<c>ModdingSelectedModPanel.Refresh</c>) and leaves both
    /// widget trees visible, and the faded-out one still holds the PREFAB's authoring text ("Mon Mod", "by
    /// Choupinette", "TYPE: Extension"). So visibility cannot gate this and the model asks the screen which
    /// mod it has selected instead; with none, the only thing declared is the game's own invitation to pick
    /// one.
    ///
    /// NOTHING HERE IS PRESSED BY A TEST. Confirm reloads the whole game runtime with the new configuration
    /// (<c>OnConfirmValidate</c>), the publish button uploads a mod to the Steam Workshop, and the two web
    /// buttons and Steam Workshop leave the game for a browser or the Steam overlay. All of them are
    /// declared - a player must be able to do what the page is for - and each is left to the player.
    /// The two folder filters are declared as the checkboxes they are, and switching one is also PERSISTENT
    /// (each writes a registry preference), which is why a test leaves them alone too.
    ///
    /// Escape: <c>HandleInput</c> shows the main menu again, behind the game's own confirmation box where
    /// the player has changed the configuration ("%ModdingScreenExitConfirmation").
    /// </summary>
    public sealed class ModdingConfigScreen : MenuDestinationScreen
    {
        private static readonly object TopStop = "modding:top";
        private static readonly object LibraryStop = "modding:library";
        private static readonly object SelectedStop = "modding:selected";
        private static readonly object LogStop = "modding:log";
        private static readonly object ActionsStop = "modding:actions";

        private readonly List<Cell> _cells = new List<Cell>();

        public override string Key
        {
            get { return "screen.modding"; }
        }

        protected override string Prefix
        {
            get { return "modding"; }
        }

        protected override string ScreenNameKey
        {
            get { return "screen.modding"; }
        }

        /// <summary>The library, because picking a mod is what the page is for - not the web link that
        /// happens to be drawn highest.</summary>
        public override object InitialFocusStop
        {
            get { return LibraryStop; }
        }

        protected override GuiWindow Window()
        {
            return Get<GameModdingScreen>();
        }

        public override void Build(GraphBuilder builder)
        {
            GameModdingScreen window = Window() as GameModdingScreen;
            if (window == null)
            {
                return;
            }

            builder.BeginStop(TopStop);
            _cells.Clear();
            Cells.AddControl(_cells, window.ModdingManualButton, "modding:manual");
            Cells.AddControl(
                _cells,
                SteamWorkshop.LegalAgreementButton(window),
                "modding:legal"
            );
            Cells.EmitLinear(builder, _cells);

            builder.BeginStop(LibraryStop);
            Library(builder, window);

            builder.BeginStop(SelectedStop);
            Selected(builder, window);

            builder.BeginStop(LogStop);
            Messages(builder, window);

            builder.BeginStop(ActionsStop);
            _cells.Clear();
            Cells.AddControl(_cells, window.ValidateButton, "modding:confirm");
            Cells.AddControl(_cells, SteamWorkshop.OpenButton(window), "modding:workshop");
            Cells.AddControl(_cells, Back(window), "modding:back");
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>The mods the game found, under the two filters that decide which of them are listed.
        /// </summary>
        private void Library(GraphBuilder builder, GameModdingScreen window)
        {
            ModdingAvailableModsPanel panel = window.AvailableModsPanel;
            if (panel == null)
            {
                return;
            }

            builder.PushContext(PanelName(panel.AgeTransform));
            _cells.Clear();
            try
            {
                AddCheckbox(panel.LocalFilterToggle, "modding:filter/local");
                AddCheckbox(SteamWorkshop.FilterToggle(panel), "modding:filter/workshop");
                Cells.AddControl(_cells, panel.DisableCustomConfigButton, "modding:custom-config");
                if (AgeWidgets.Visible(panel.NoDataAvailableGroup))
                {
                    Cells.AddReadout(_cells, panel.NoDataAvailableGroup, "modding:no-mods");
                }

                AgeTransform table = panel.AvailableModItemsTable;
                IList<AgeTransform> rows = table == null ? null : table.Children;
                for (int i = 0; rows != null && i < rows.Count; i++)
                {
                    Row(rows[i], i);
                }
            }
            catch (Exception e)
            {
                Log.Warn("modding: reading the mod library threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            builder.PopContext();
        }

        /// <summary>One mod: what it is, whether it is in the configuration, and - for one of your own
        /// while Steam is up - the publish button.</summary>
        private void Row(AgeTransform widget, int index)
        {
            AvailableModItem item = widget == null ? null : widget.GetComponent<AvailableModItem>();
            // Painted, not merely visible: the library is a pooled table and it never shrinks, so
            // switching a folder filter off leaves the surplus rows behind at alpha zero still holding
            // the mods they used to show (measured on the content browser's own pooled list).
            if (item == null || !AgeWidgets.Painted(widget))
            {
                return;
            }

            AvailableModItem it = item;
            string key = "modding:mod/" + Named(item, index);
            AgeControlToggle selection = item.SelectionToggle;
            AgeTransform at = AgeWidgets.Transform(selection) ?? widget;
            AgeTooltip tooltip = item.Tooltip;
            Func<bool> offered = () => AgeWidgets.Offered(at) && Settled(it);
            NodeVtable vtable = GraphNodes.Radio(
                () => Title(it),
                () => selection != null && selection.State,
                () => AgeWidgets.Toggle(selection),
                offered,
                null,
                tooltip
            );
            vtable.Announcements.Add(GraphNodes.ValuePart(() => Details(it)));
            GraphNodes.AddRefusal(vtable, tooltip, offered);
            AgeWidgets.Point(vtable, selection, tooltip, at);
            Cells.Add(_cells, at, ControlId.Structural(key), vtable);

            AddCheckbox(
                item.ActivationToggle,
                key + "/activate",
                ModStrings.ModdingActivated,
                () => Settled(it)
            );
            Cells.AddControl(_cells, item.PublishingButton, key + "/publish");
        }

        /// <summary>What a row is called. The drawn title where the game drew one - and where it did not,
        /// the wrapper's own answer: a row that is still DOWNLOADING has no mod behind it yet, so the game
        /// hides the whole content block and its title label keeps the prefab's authoring text ("Mon Mod").
        /// <c>GuiModSlot.Title</c> is what the game itself falls back to there.</summary>
        private static string Title(AvailableModItem item)
        {
            string drawn = Drawn(item.ModTitleLabel);
            if (!string.IsNullOrEmpty(drawn))
            {
                return drawn;
            }

            try
            {
                GuiModSlot slot = item.GuiModSlot;
                return slot == null ? null : AgeText.Clean(slot.Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What a row says beyond its name: where it sits in the load order where the game has
        /// numbered it, what kind of mod it is, who wrote it, which folder it came from, and that it is
        /// being uploaded where it is. Each is said only while the game is drawing it - the row keeps a
        /// block per case and hides the ones that do not apply, and a hidden block holds the values of
        /// whichever mod the row showed last.</summary>
        private static string Details(AvailableModItem item)
        {
            try
            {
                return new MessageBuilder()
                    .ListItem(Drawn(item.OrderNumberLabel))
                    .ListItem(Drawn(item.ModTypeLabel))
                    .ListItem(Drawn(item.ModAuthorLabel))
                    .ListItem(Drawn(item.ModFolderLabel))
                    .ListItem(Drawn(item.UploadTitle))
                    .Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Whether the game can act on this row at all. A row whose mod is still downloading has
        /// no mod behind it, and the game leaves its two toggles switched ON anyway: clicking either
        /// answers with a logged error and nothing else ("Trying to select a GuiModSlot with no GuiMod.
        /// This item should be disabled." - <c>AvailableModItem.OnToggleSelectionCb</c>, and the enable flag
        /// is only ever written inside the branch that HAS a mod). So the refusal the game meant is the one
        /// declared here, with its own sentence for the reason.</summary>
        private static bool Settled(AvailableModItem item)
        {
            try
            {
                GuiModSlot slot = item.GuiModSlot;
                return slot != null && slot.GuiMod != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Everything the game wrote about the mod the player picked, or its own invitation to
        /// pick one. Which of the two is the SCREEN's answer, not the widgets': it fades the panels past
        /// each other and the faded one still holds the prefab's placeholder text.</summary>
        private void Selected(GraphBuilder builder, GameModdingScreen window)
        {
            ModdingSelectedModPanel panel = window.SelectedModPanel;
            if (panel == null)
            {
                return;
            }

            builder.PushContext(PanelName(panel.AgeTransform));
            _cells.Clear();
            try
            {
                if (Chosen(window))
                {
                    AddLine(panel.ModTitleLabel, "modding:mod-title");
                    AddLine(panel.ModAuthorLabel, "modding:mod-author");
                    AddParagraph(panel.ModDescriptionLabel, "modding:mod-description");
                    AddLine(panel.ModTypeLabel, "modding:mod-type");
                    AddLine(panel.ModContentTagsLabel, "modding:mod-tags");
                    AddLine(panel.ModFolderLabel, "modding:mod-folder");
                    AddLine(panel.ModHomepageLabel, "modding:mod-homepage");
                    AddLine(panel.ModVersionLabel, "modding:mod-version");
                    AddParagraph(panel.ModReleaseNotesLabel, "modding:mod-release-notes");
                }
                else
                {
                    Cells.AddReadout(_cells, panel.NoSelectionContent, "modding:no-selection");
                }
            }
            catch (Exception e)
            {
                Log.Warn("modding: reading the selected mod threw: " + e);
            }

            Cells.Emit(builder, _cells);
            builder.PopContext();
        }

        /// <summary>What the page has said about what the player did - one line per message, newest first,
        /// which is the order the panel inserts them in.</summary>
        private void Messages(GraphBuilder builder, GameModdingScreen window)
        {
            ModdingLogPanel panel = window.LogPanel;
            AgeTransform table = panel == null ? null : panel.LogMessageTable;
            IList<AgeTransform> lines = table == null ? null : table.Children;
            if (lines == null || lines.Count == 0)
            {
                return;
            }

            builder.PushContext(PanelName(panel.AgeTransform));
            _cells.Clear();
            try
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    ModLogMessageLine line =
                        lines[i] == null ? null : lines[i].GetComponent<ModLogMessageLine>();
                    // Pooled like the library above: a retired line keeps its old words at alpha zero.
                    if (line != null && AgeWidgets.Painted(lines[i]))
                    {
                        AddLine(line.MessageTitle, "modding:log/" + i);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("modding: reading the log threw: " + e);
            }

            Cells.Emit(builder, _cells);
            builder.PopContext();
        }

        private void AddLine(AgePrimitiveLabel label, string key)
        {
            AgeTransform widget = label == null ? null : label.AgeTransform;
            Cells.AddReadout(_cells, widget, key);
        }

        /// <summary>A block of prose the game put in a scroll view - said whole, and walkable line by line
        /// in the review buffer.</summary>
        private void AddParagraph(AgePrimitiveLabel label, string key)
        {
            AgeTransform widget = label == null ? null : label.AgeTransform;
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgePrimitiveLabel it = label;
            NodeVtable vtable = GraphNodes.Readout(
                () => AgeText.Label(it),
                () => null,
                () => AgeText.Lines(AgeText.Label(it)),
                AgeWidgets.Raw(widget)
            );
            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(_cells, widget, ControlId.Referenced(label, key), vtable);
        }

        private void AddCheckbox(
            AgeControlToggle toggle,
            string key,
            string nameKey = null,
            Func<bool> also = null
        )
        {
            AgeTransform widget = AgeWidgets.Transform(toggle);
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgeControlToggle box = toggle;
            AgeTransform at = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            string named = nameKey;
            Func<bool> extra = also;
            Func<bool> offered = () =>
                AgeWidgets.Offered(at) && (extra == null || extra());
            NodeVtable vtable = GraphNodes.Checkbox(
                () =>
                    named == null
                        ? AgeWidgets.TextOf(at)
                        : ModStrings.Get(named),
                () => box.State,
                () => AgeWidgets.Toggle(box),
                offered,
                tooltip
            );
            GraphNodes.AddRefusal(vtable, tooltip, offered);
            AgeWidgets.Point(vtable, box, tooltip, at);
            Cells.Add(_cells, widget, ControlId.Structural(key), vtable);
        }

        /// <summary>Whether the page is describing a mod at all - the screen's own answer, since the panel
        /// keeps both branches drawn.</summary>
        private static bool Chosen(GameModdingScreen window)
        {
            try
            {
                return window.SelectedGuiMod != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string PanelName(AgeTransform panel)
        {
            return AgeWidgets.PanelTitle(panel);
        }

        /// <summary>What keys a row: the mod's own name, which survives the table pooling its rows and
        /// rebinding them as the filters change. Its position is the fallback - a row still downloading has
        /// no mod behind it yet.</summary>
        private static string Named(AvailableModItem item, int index)
        {
            try
            {
                GuiModSlot slot = item.GuiModSlot;
                GuiMod mod = slot == null ? null : slot.GuiMod;
                string name = mod == null || mod.Name == null ? null : mod.Name.ToString();
                return string.IsNullOrEmpty(name) ? index.ToString() : name;
            }
            catch (Exception)
            {
                return index.ToString();
            }
        }

        private static string Drawn(AgePrimitiveLabel label)
        {
            return label == null || !AgeWidgets.Visible(label.AgeTransform)
                ? null
                : AgeText.Label(label);
        }

        /// <summary>The way back, which the page draws in its button band and exposes no field for.
        /// </summary>
        private static AgeTransform Back(GameModdingScreen window)
        {
            try
            {
                AgeTransform band = window.ButtonsGroup;
                AgeTransform workshop = SteamWorkshop.OpenButton(window);
                IList<AgeTransform> children = band == null ? null : band.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    if (
                        child != null
                        && !ReferenceEquals(child, window.ValidateButton)
                        && !ReferenceEquals(child, workshop)
                        && AgeWidgets.Visible(child)
                        && AgeWidgets.Button(child) != null
                    )
                    {
                        return child;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }
    }
}
