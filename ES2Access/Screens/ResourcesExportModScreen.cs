using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

// The game's own window is ResourcesExportScreen in the global namespace; the mod's adapter has to
// wear a different name, and both are used in this file.
using GameResourcesExportScreen = ResourcesExportScreen;

namespace ES2Access.Screens
{
    /// <summary>
    /// The asset exporter (<c>ResourcesExportScreen</c>) - the modding tool that writes the game's own
    /// ships, weapons and planets out as COLLADA meshes, PNG textures and the remapping XML a reskin
    /// mod needs.
    ///
    /// Two panels side by side, each entered under the heading the game wrote on it ("Available
    /// assets", "Selected asset"), and a Back button under them.
    ///
    /// THE LIST IS ONE FLAT SELECT-THEN-LOOK LIST, not a tree. The three ticks under it - Ships,
    /// Weapons, Planets - are FILTERS over one list of 1565 assets (191 ships, 1286 weapons, 88
    /// planets), not section headings: the game builds a single table in type order and hides the rows
    /// whose type is switched off (<c>RefreshResourceItem</c>), so there is no grouping on screen to
    /// model. They are declared where the game draws them, in the footer BELOW the list, on a stop of
    /// their own so they are one Tab away rather than 191 rows away. A row is a
    /// <see cref="GraphNodes.Radio"/> for the same reason a mod-library row is: picking one is not
    /// doing anything, it is choosing what the right-hand panel describes.
    ///
    /// The list ARRIVES LATE and CHANGES SIZE UNDER THE PLAYER. Opening the page starts a coroutine
    /// that reads the three asset manifests over several frames (<c>LoadResourcesMetas</c>) - the page
    /// is a "Loading ships"/"Loading weapons modules"/"Loading planet materials" curtain until it
    /// finishes - and afterwards every filter tick adds or removes hundreds of rows at once. A sighted
    /// player watches both happen; so the count is announced whenever the loading curtain lifts or a
    /// filter changes, from ONE watch over the same four flags.
    ///
    /// THE RIGHT PANEL IS THE GAME'S OWN GROUPING, and that is where the tree is: the selected asset's
    /// name and its mesh figures, then one expandable group per MATERIAL holding the texture each of
    /// its shader properties uses (<c>ResourceExportMaterialItem</c> over
    /// <c>ResourceExportPropertyItem</c>) - which is exactly the list the two export buttons write out,
    /// so a modder can read what an export will produce before running it. Everything on this panel is
    /// read with <see cref="AgeText.FullLabel"/>: every string on it is a resource path, a shader
    /// property, a texture name or - on the result line - a full file path, none of which the game
    /// sizes its boxes to.
    ///
    /// NOTHING HERE IS PRESSED BY A TEST. "Export meshes &amp; textures" and "Export textures only"
    /// WRITE FILES to <c>&lt;game&gt;\Resources Export\&lt;asset&gt;</c> and can raise the game's own
    /// overwrite confirmation; the folder button hands the path to the operating system's file manager
    /// (<c>Process.Start</c>), which leaves the game. All three are declared - a player must be able to
    /// do what the page is for - and each is left to the player.
    ///
    /// AN EXPORT IS A LONG SILENCE otherwise. It disables the whole panel, swallows every input
    /// including Escape (<c>HandleInput</c>) and reports itself only by rewriting one label - "Exporting
    /// vertices 41984/99379", then the path it wrote - hundreds of times a second. So that label is
    /// watched and spoken as it changes, no more than once every <see cref="ProgressGap"/> frames while
    /// the export runs and immediately once it ends, and <see cref="IsWorkable"/> goes false for the
    /// duration so the controls the engine has just switched off do not each announce themselves
    /// unavailable.
    ///
    /// Escape: <c>HandleInput</c> shows the main menu again - except while an export is running, when
    /// the window swallows every action until it finishes. That is the game's decision and is left
    /// alone.
    /// </summary>
    public sealed class ResourcesExportModScreen : MenuDestinationScreen
    {
        private static readonly object AssetsStop = "resources-export:assets";
        private static readonly object FiltersStop = "resources-export:filters";
        private static readonly object SelectedStop = "resources-export:selected";
        private static readonly object ExportStop = "resources-export:export";
        private static readonly object ActionsStop = "resources-export:actions";

        /// <summary>How long the count announcement waits after the list's shape changed: a filter's
        /// handler only marks the window dirty, and the rows are not hidden or shown until the window's
        /// own refresh runs, so counting on the frame the tick flipped counts the OLD list.</summary>
        private const int SettleFrames = 3;

        /// <summary>How rarely a running export's progress line may speak. The label is rewritten every
        /// thousand vertices, which is a stream nobody can listen to; the player needs to know it is
        /// still working and roughly where it is, not every number.
        ///
        /// In SECONDS, not frames, unlike <see cref="SettleFrames"/> - and that is not a style choice.
        /// A settle waits for the game's own refresh, which is measured in frames whatever they cost; a
        /// speaking gap is a promise to the player's ear, and this page is the one that makes the
        /// difference visible. An export runs with up to 1565 asset rows drawn, where a frame here
        /// measures 70-90 ms, so the same 90 frames that mean 1.5 s on a fast machine meant seven
        /// SECONDS of silence between progress lines when this was counted in frames (measured).
        /// </summary>
        private const float ProgressSeconds = 1.5f;

        private readonly List<Cell> _cells = new List<Cell>();

        // What the list's shape was last time it was looked at, and whether a change to it is still
        // owed an announcement.
        private bool _loading;
        private bool _ships;
        private bool _weapons;
        private bool _planets;
        private bool _pending;
        private int _settling;

        // The export's own watch: what the result line last said, whether an export was running then,
        // and the earliest time the progress line may speak again.
        private bool _baselined;
        private bool _exporting;
        private string _progress;
        private float _speakAt;

        public override string Key
        {
            get { return ModStrings.ScreenResourcesExport; }
        }

        protected override string Prefix
        {
            get { return "resources-export"; }
        }

        protected override string ScreenNameKey
        {
            get { return ModStrings.ScreenResourcesExport; }
        }

        /// <summary>The asset list, because picking an asset is what the page is for - not the filter
        /// the game happens to draw first in the footer.</summary>
        public override object InitialFocusStop
        {
            get { return AssetsStop; }
        }

        /// <summary>False while an export runs. The engine switches the whole panel off for the
        /// duration (<c>Refresh</c> writes <c>Enable</c> from <c>ExportInProgress</c>), so without this
        /// every control under the cursor would announce itself unavailable and then available again
        /// around an export the player started deliberately.</summary>
        public override bool IsWorkable
        {
            get { return !Exporting(Panel()); }
        }

        protected override GuiWindow Window()
        {
            return Get<GameResourcesExportScreen>();
        }

        /// <summary>A visit starts owing the player a count: the page rebuilds its manifests from
        /// scratch every time it is shown (<c>OnEndHide</c> clears them), so it always opens on the
        /// loading curtain and always fills in a moment later.</summary>
        public override void OnPush()
        {
            _pending = true;
            _settling = SettleFrames;
            _baselined = false;
        }

        public override void OnUpdate()
        {
            GameResourcesExportScreen window = Page();
            if (window == null)
            {
                return;
            }

            Listing(window);
            Progress(Panel());
        }

        /// <summary>How many assets the list is showing, said once the shape it is showing has settled.
        /// One watch covers both ways the list changes on its own: the loading curtain lifting, and a
        /// filter tick adding or removing hundreds of rows.</summary>
        private void Listing(GameResourcesExportScreen window)
        {
            // A window the engine is still fading in counts as loading, and not as a nicety: its own
            // transform is not visible yet, so the curtain inside it reads as DOWN, and the count would
            // be announced against a table the game has not filled - "0 assets listed" on arrival,
            // followed by the real number a second later (measured).
            bool loading =
                !AgeWidgets.Visible(window.AgeTransform)
                || AgeWidgets.Visible(window.LoadingGroup);
            bool ships = Ticked(window.ShipsToggle);
            bool weapons = Ticked(window.WeaponsToggle);
            bool planets = Ticked(window.PlanetsToggle);

            if (loading != _loading || ships != _ships || weapons != _weapons || planets != _planets)
            {
                _loading = loading;
                _ships = ships;
                _weapons = weapons;
                _planets = planets;
                _pending = true;
                _settling = SettleFrames;
                return;
            }

            if (loading || !_pending || _settling-- > 0)
            {
                return;
            }

            _pending = false;
            Voice.Say(
                ModStrings.Plural(
                    ModStrings.ResourcesExportAssetListed,
                    ModStrings.ResourcesExportAssetsListed,
                    Listed(window)
                ),
                false
            );
        }

        /// <summary>What a running export has got to, and what it produced. Read off the one line the
        /// panel writes it on rather than off its private state, because that line is also where the
        /// game puts the failure and the path of the file it wrote.</summary>
        private void Progress(ResourceExportPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            bool exporting = Exporting(panel);
            string said = AgeText.FullLabel(panel.ExportResultLabel);

            // The first frame of a visit only records: the panel keeps the last export's success line
            // for the whole session, and arriving at the page is not that export finishing.
            if (!_baselined)
            {
                _baselined = true;
                _exporting = exporting;
                _progress = said;
                return;
            }

            // An export ending clears the throttle rather than announcing on the spot: the panel writes
            // its final message through its own refresh, a frame or more after the job says it is done.
            if (exporting != _exporting)
            {
                _exporting = exporting;
                _speakAt = 0f;
            }

            float now = UnityEngine.Time.unscaledTime;
            if (string.IsNullOrEmpty(said) || said == _progress || now < _speakAt)
            {
                return;
            }

            _progress = said;
            _speakAt = exporting ? now + ProgressSeconds : 0f;
            Voice.Say(said, false);
        }

        public override void Build(GraphBuilder builder)
        {
            GameResourcesExportScreen window = Page();
            if (window == null)
            {
                return;
            }

            ResourceExportPanel panel = window.ResourceExportPanel;

            builder.BeginStop(AssetsStop);
            Assets(builder, window);

            builder.BeginStop(FiltersStop);
            Filters(builder, window);

            builder.BeginStop(SelectedStop);
            Selected(builder, panel);

            builder.BeginStop(ExportStop);
            Exports(builder, panel);

            builder.BeginStop(ActionsStop);
            _cells.Clear();
            Cells.AddControl(
                _cells,
                AgeWidgets.ChildNamed(window.AgeTransform, "BackButton", 2),
                "resources-export:back"
            );
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>The assets the game found, or the curtain it is still finding them behind.</summary>
        private void Assets(GraphBuilder builder, GameResourcesExportScreen window)
        {
            builder.PushContext(
                AgeWidgets.PanelTitle(
                    AgeWidgets.ChildNamed(window.AgeTransform, "ResourcesListPanel", 2)
                )
            );
            try
            {
                // Flow control: which of the two states the panel is in - still loading, or listing what it found.
                if (AgeWidgets.Visible(window.LoadingGroup))
                {
                    Line(builder, window.LoadingLabel, "resources-export:loading");
                }
                else if (!Rows(builder, window))
                {
                    // Every filter switched off is an empty page, and the player has to be able to
                    // land on the answer rather than on a stop with nothing in it.
                    builder.AddItem(Nodes.Synthetic(
                        ControlId.Structural("resources-export:empty"),
                        GraphNodes.Readout(
                            () =>
                                ModStrings.Plural(
                                    ModStrings.ResourcesExportAssetListed,
                                    ModStrings.ResourcesExportAssetsListed,
                                    0
                                ),
                            () => null,
                            null,
                            null
                        )
                    ));
                }
            }
            catch (Exception e)
            {
                Log.Warn("resources-export: reading the asset list threw: " + e);
            }

            builder.PopContext();
        }

        /// <summary>One row per asset the filters are letting through. PAINTED, not merely visible: the
        /// table is pooled and never shrinks, and a row a filter switched off keeps the asset it used to
        /// show.</summary>
        private static bool Rows(GraphBuilder builder, GameResourcesExportScreen window)
        {
            AgeTransform table = window.ResourcesTable;
            IList<AgeTransform> rows = table == null ? null : table.Children;
            bool any = false;
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                AgeTransform widget = rows[i];
                // Flow control: a row the table is not drawing is not one of this search's resources, and is not walked.
                if (widget == null || !AgeWidgets.Painted(widget))
                {
                    continue;
                }

                ResourceExportItem item = widget.GetComponent<ResourceExportItem>();
                if (item == null)
                {
                    continue;
                }

                any = true;
                ResourceExportItem it = item;
                AgeControlToggle toggle = item.Toggle;
                AgeTransform at = widget;
                NodeVtable vtable = GraphNodes.Radio(
                    () => AgeText.FullLabel(it.ResourceTitleLabel),
                    () => toggle != null && toggle.State,
                    () => AgeWidgets.Toggle(toggle),
                    () => AgeWidgets.Offered(at)
                );
                AgeWidgets.Point(vtable, toggle);
                builder.AddItem(Nodes.Drawn(ControlId.ForObject(widget), vtable, widget));
            }

            return any;
        }

        /// <summary>The three ticks that decide which kinds of asset the list holds, on the one row the
        /// game draws them in.</summary>
        private void Filters(GraphBuilder builder, GameResourcesExportScreen window)
        {
            _cells.Clear();
            Filter(window.ShipsToggle, "resources-export:filter/ships");
            Filter(window.WeaponsToggle, "resources-export:filter/weapons");
            Filter(window.PlanetsToggle, "resources-export:filter/planets");
            Cells.EmitLinear(builder, _cells);
        }

        private void Filter(AgeControlToggle toggle, string key)
        {
            AgeTransform widget = AgeWidgets.Transform(toggle);
            if (widget == null)
            {
                return;
            }

            AgeControlToggle box = toggle;
            AgeTransform at = widget;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            Func<bool> offered = () => AgeWidgets.Offered(at);
            NodeVtable vtable = GraphNodes.Checkbox(
                () => AgeWidgets.TextOf(at),
                () => box.State,
                () => AgeWidgets.Toggle(box),
                offered,
                tooltip
            );
            AgeWidgets.Point(vtable, box, tooltip, at);
            Cells.Add(_cells, widget, ControlId.Structural(key), vtable);
        }

        /// <summary>What the game says about the asset the player picked, or its own invitation to pick
        /// one. Which of the two is the panel's own answer - it shows one content block and hides the
        /// other, and the hidden one keeps the prefab's authoring text ("SS_Cravers_Large_01",
        /// "Submeshes: 15; Vertices: 150000; Triangles: 250000").</summary>
        private void Selected(GraphBuilder builder, ResourceExportPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            builder.PushContext(AgeWidgets.PanelTitle(panel.AgeTransform));
            try
            {
                // Flow control: which of the two the panel is drawing - a chosen resource, or its invitation to pick one.
                if (!AgeWidgets.Visible(panel.SelectedResourceGroup))
                {
                    _cells.Clear();
                    Cells.AddReadout(
                        _cells,
                        panel.NoResourceSelectedGroup,
                        "resources-export:no-selection"
                    );
                    Cells.EmitLinear(builder, _cells);
                }
                else
                {
                    Line(builder, panel.SelectedResourceNameLabel, "resources-export:asset-name");
                    Line(
                        builder,
                        panel.SelectedResourceDetailsLabel,
                        "resources-export:asset-details"
                    );
                    Materials(builder, panel);
                }
            }
            catch (Exception e)
            {
                Log.Warn("resources-export: reading the selected asset threw: " + e);
            }

            builder.PopContext();
        }

        /// <summary>One group per material, holding the texture each of its shader properties uses.
        /// Collapsed by default and not even BUILT while collapsed: a ship carries a dozen materials
        /// with a score of properties each, and this runs on every navigation operation.</summary>
        private static void Materials(GraphBuilder builder, ResourceExportPanel panel)
        {
            AgeTransform table = panel.ResourceMaterialsTable;
            IList<AgeTransform> materials = table == null ? null : table.Children;
            for (int i = 0; materials != null && i < materials.Count; i++)
            {
                AgeTransform widget = materials[i];
                if (widget == null || !AgeWidgets.Painted(widget))
                {
                    continue;
                }

                ResourceExportMaterialItem material =
                    widget.GetComponent<ResourceExportMaterialItem>();
                if (material == null)
                {
                    continue;
                }

                ResourceExportMaterialItem it = material;
                ControlId id = ControlId.ForObject(widget);
                builder.BeginGroup(Nodes.Drawn(id, GraphNodes.Group(() => AgeText.FullLabel(it.MaterialNameLabel)), widget));
                if (builder.IsExpanded(id))
                {
                    Textures(builder, material);
                }

                builder.EndGroup();
            }
        }

        private static void Textures(GraphBuilder builder, ResourceExportMaterialItem material)
        {
            AgeTransform table = material.PropertyItemsTable;
            IList<AgeTransform> properties = table == null ? null : table.Children;
            for (int i = 0; properties != null && i < properties.Count; i++)
            {
                AgeTransform widget = properties[i];
                // Flow control: a line the property table is not drawing is not one of this material's.
                if (widget == null || !AgeWidgets.Painted(widget))
                {
                    continue;
                }

                ResourceExportPropertyItem property =
                    widget.GetComponent<ResourceExportPropertyItem>();
                if (property == null)
                {
                    continue;
                }

                ResourceExportPropertyItem it = property;
                NodeVtable vtable = GraphNodes.Readout(
                    () => AgeText.FullLabel(it.PropertyNameLabel),
                    () => AgeText.FullLabel(it.TextureNameLabel),
                    null,
                    null
                );
                AgeWidgets.PointAt(vtable, widget);
                builder.AddItem(Nodes.Drawn(ControlId.ForObject(widget), vtable, widget));
            }
        }

        /// <summary>The two exports and the folder button, on the row the game draws them in, with the
        /// line the export reports itself on under them. Declared only while an asset is selected,
        /// which is the only time the game draws any of them.</summary>
        private void Exports(GraphBuilder builder, ResourceExportPanel panel)
        {
            if (panel == null || !AgeWidgets.Visible(panel.SelectedResourceGroup))
            {
                return;
            }

            AgeTransform band = Parent(panel.ExportAllButton);
            _cells.Clear();
            Cells.AddControl(_cells, panel.ExportAllButton, "resources-export:export-all");
            Cells.AddControl(
                _cells,
                AgeWidgets.ChildNamed(band, "ExportTexturesButton", 1),
                "resources-export:export-textures"
            );
            OpenFolder(panel);
            Cells.EmitLinear(builder, _cells);

            Line(builder, panel.ExportResultLabel, "resources-export:result");
        }

        /// <summary>The button that hands the export folder to the file manager. The game draws it as a
        /// bare icon and hangs the only words it has - the sentence that explains it - on the CONTAINER
        /// around it, so the container is what is declared and the button inside it is what decides
        /// whether there is anything to declare (the game hides the button and leaves the container
        /// standing).</summary>
        private void OpenFolder(ResourceExportPanel panel)
        {
            AgeTransform button = panel.OpenExportFolderButton;
            AgeTransform container = Parent(button);
            if (button == null || container == null || !AgeWidgets.Visible(button))
            {
                return;
            }

            _cells.Add(
                Cells.Control(
                    container,
                    AgeWidgets.Button(button),
                    AgeWidgets.TextOf(button),
                    "resources-export:open-folder"
                )
            );
        }

        /// <summary>A line of the game's own text, read WHOLE. Every one of these holds a resource
        /// path, a shader property, a texture name or a file path, none of which fits the box it is
        /// drawn in.</summary>
        private static void Line(GraphBuilder builder, AgePrimitiveLabel label, string key)
        {
            AgeTransform widget = AgeWidgets.Transform(label);
            if (widget == null || !AgeWidgets.Visible(widget))
            {
                return;
            }

            AgePrimitiveLabel it = label;
            if (string.IsNullOrEmpty(AgeText.FullLabel(it)))
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Readout(
                () => AgeText.FullLabel(it),
                () => null,
                null,
                AgeWidgets.Raw(widget)
            );
            AgeWidgets.PointAt(vtable, widget);
            builder.AddItem(Nodes.Drawn(ControlId.For(label, key), vtable, label));
        }

        /// <summary>How many rows the list is drawing. Counted off the widgets rather than off the
        /// manifests, which the window keeps to itself - and only ever on the frame the count is about
        /// to be spoken. The drawing test stays asked here, and is not left to the node gate: this is
        /// a NUMBER the player hears, not a node's existence, and a ghost counted in it would be
        /// spoken before anything had a chance to drop it.</summary>
        private static int Listed(GameResourcesExportScreen window)
        {
            return AgeWidgets.DrawnCount(window.ResourcesTable);
        }

        private static bool Ticked(AgeControlToggle toggle)
        {
            try
            {
                return toggle != null && toggle.State;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool Exporting(ResourceExportPanel panel)
        {
            try
            {
                return panel != null && panel.ExportInProgress;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static AgeTransform Parent(AgeTransform widget)
        {
            try
            {
                return widget == null ? null : widget.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private ResourceExportPanel Panel()
        {
            GameResourcesExportScreen window = Page();
            return window == null ? null : window.ResourceExportPanel;
        }

        private GameResourcesExportScreen Page()
        {
            return Window() as GameResourcesExportScreen;
        }
    }
}
