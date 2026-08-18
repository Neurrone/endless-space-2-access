using System;
using System.Collections.Generic;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>
    /// Building a system development project: the window a free project slot on the economy screen opens.
    ///
    /// The shape is the shape it is drawn in (measured): a heading across the top, then the components
    /// the empire could use - a row of family icons over a grid of resources - then the project as it
    /// stands with its own row of slots, then the effects it would have, then Cancel, Reset and Confirm.
    /// The strategic half of the components is only drawn for an empire whose technology allows it
    /// (<c>RecipeCreationModalWindow.Refresh</c> :214-222), so what is declared is whichever halves are
    /// there.
    ///
    /// The game's model is put-a-component-in, take-one-out, then confirm, and it is copied rather than
    /// shortened: Enter on a component adds it to the project - REPLACING the last one when the project
    /// is already full, which is the game's own rule (<c>OnClickIngredientItem</c> :354-364) and not
    /// something this screen decides - and Enter on one of the project's own slots takes that component
    /// back out (<c>OnClickIngredientSlot</c> :366-377). Nothing is committed until Confirm, which the
    /// game keeps switched off while the project is not valid and explains why on itself.
    ///
    /// A resource the empire has never located is drawn as a question mark with no name
    /// (<c>IngredientItem.Bind</c> fades it to nothing when it does not exist for this empire), so the
    /// grid declares only the components a sighted player can see, alpha included - and the walk is a
    /// LIST rather than a table of crossed edges: most columns have no drawn cell, so column-preserving
    /// vertical moves would pair a lone cell with the wrong family (measured on the economy screen's own
    /// copy of the same grid). The family icons are a legend the player can walk, a region of their own
    /// inside the same stop, and every component says which family it belongs to for itself - in the
    /// economy screen's words for those families, which is the one reader both grids share.
    ///
    /// There is no screen name. The window's heading is a drawn element with its own explanation on its
    /// tooltip, so it is declared where it is drawn and focus lands on it - which says what has just
    /// opened, once, instead of saying it as a screen name and then again as a control.
    ///
    /// Escape is the game's, and it is not a plain close: the window answers it with the game's own
    /// "lose your changes?" box whenever the project has been touched
    /// (<c>HandleInput</c> :125-140), which the shared message-box screen already speaks.
    /// </summary>
    public sealed class RecipeCreationScreen : Screen
    {
        private static readonly object HeadingStop = "recipe:heading";
        private static readonly object LuxuriesStop = "recipe:luxuries";
        private static readonly object StrategicsStop = "recipe:strategics";
        private static readonly object ProjectStop = "recipe:project";
        private static readonly object EffectsStop = "recipe:effects";
        private static readonly object ActionsStop = "recipe:actions";

        // Reused across builds rather than allocated per frame: Build runs every tick.
        private readonly List<Cell> _cells = new List<Cell>();
        private readonly List<float> _columnCentres = new List<float>();

        public override string Key
        {
            get { return "screen.recipe-creation"; }
        }

        /// <summary>Over the economy screen that opens it, and under the message box its own Escape
        /// raises.</summary>
        public override int Layer
        {
            get { return 36; }
        }

        /// <summary>The heading, because it is drawn first and Tab does not wrap.</summary>
        public override object InitialFocusStop
        {
            get { return HeadingStop; }
        }

        public override bool IsActive()
        {
            try
            {
                RecipeCreationModalWindow window = Window();
                return window != null && window.Shown && window.IsReady;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Escape is the game's: the window handles it itself, and answers it with its own
        /// confirmation once the project has been changed.</summary>
        public override bool Back()
        {
            return false;
        }

        public override void Build(GraphBuilder builder)
        {
            RecipeCreationModalWindow window = Window();
            if (window == null)
            {
                return;
            }

            try
            {
                BuildHeading(builder, window);
                BuildComponents(
                    builder,
                    LuxuriesStop,
                    window.AvailableIngredientsLuxuriesGroup,
                    window.IngredientHeaderLuxuriesTable,
                    window.IngredientItemsLuxuriesTable,
                    ResourceDefinition.Type.Luxury
                );
                BuildComponents(
                    builder,
                    StrategicsStop,
                    window.AvailableIngredientsStrategicsGroup,
                    window.IngredientHeaderStrategicsTable,
                    window.IngredientItemsStrategicsTable,
                    ResourceDefinition.Type.Strategic
                );
                BuildProject(builder, window);
                BuildEffects(builder, window);
                BuildActions(builder, window);
            }
            catch (Exception e)
            {
                Log.Warn("recipe: reading the window threw: " + e);
            }
        }

        /// <summary>The heading, taken from the LABEL rather than from the group around it: the sentence
        /// explaining what a development project is for hangs on the label, and a readout of the group
        /// would say the words and lose the explanation.</summary>
        private void BuildHeading(GraphBuilder builder, RecipeCreationModalWindow window)
        {
            builder.BeginStop(HeadingStop);
            _cells.Clear();
            Cells.AddReadout(
                _cells,
                AgeWidgets.ChildNamed(window.AgeTransform, "WindowTitle", 3)
                    ?? AgeWidgets.ChildNamed(window.AgeTransform, "TitleGroup", 2),
                "recipe:title"
            );
            Cells.Emit(builder, _cells);
        }

        /// <summary>
        /// One half of the components on offer: the caption the window writes over it, the family icons
        /// heading the columns, and the resources the empire can see.
        ///
        /// Enter on a component is the item's own click, which puts it into the project - and a component
        /// already in the project is drawn switched off, which is how the game says it is spent.
        /// </summary>
        private void BuildComponents(
            GraphBuilder builder,
            object stop,
            AgeTransform group,
            AgeTransform headers,
            AgeTransform items,
            ResourceDefinition.Type type
        )
        {
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            builder.BeginStop(stop);
            bool named = AddCaption(builder, group, stop);

            EconomyScreen.ReadColumnCentres(headers, _columnCentres);
            string[] columns = EconomyScreen.FamilyNames(
                type,
                _columnCentres.Count,
                _columnCentres.Count
            );

            builder.SetRegion(stop + "/legend");
            _cells.Clear();
            AddFamilyHeaders(_cells, headers, columns, stop);
            Cells.EmitLinear(builder, _cells);

            builder.SetRegion(stop + "/items");
            _cells.Clear();
            IList<AgeTransform> children = items == null ? null : items.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AddIngredient(
                    _cells,
                    children[i],
                    stop,
                    i,
                    EconomyScreen.Column(
                        columns,
                        EconomyScreen.ColumnOf(children[i], _columnCentres)
                    )
                );
            }

            Cells.EmitLinear(builder, _cells);
            builder.SetRegion(null);
            if (named)
            {
                builder.PopContext();
            }
        }

        /// <summary>The family icons, named by the effect each family improves - the legend the player
        /// walks, one per row. The header widget keeps no reference to the resource it was built from, so
        /// the names come from the game's own resource list, asked for the same first-N-of-a-type the
        /// table was filled from (<c>OnGameCreated</c> :155-179) - the same list, in the same order, by
        /// the same reader the economy screen's copy of this grid uses.</summary>
        private static void AddFamilyHeaders(
            List<Cell> cells,
            AgeTransform headers,
            string[] columns,
            object stop
        )
        {
            IList<AgeTransform> children = headers == null ? null : headers.Children;
            if (children == null || !AgeWidgets.Visible(headers))
            {
                return;
            }

            for (int i = 0; i < children.Count; i++)
            {
                AgeTransform widget = children[i];
                if (widget == null || !SettingRows.Drawn(widget))
                {
                    continue;
                }

                string name = EconomyScreen.Column(columns, i);
                AgeTooltip tooltip = AgeWidgets.Raw(widget);
                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => name),
                    },
                    Sections = GraphNodes.Sections(null, tooltip),
                };
                AgeWidgets.PointAt(vtable, widget);
                Cells.Add(
                    cells,
                    widget,
                    ControlId.Referenced(widget, stop + "/family/" + i),
                    vtable
                );
            }
        }

        /// <summary>One component the empire could put into a project: what it is, how much of it there
        /// is, which family it belongs to, and its own dossier in the review buffer. Named off the
        /// wrapper the game hangs on its tooltip, because the item draws a picture and a stock figure and
        /// no words; the family word trails, because the linearised grid has no column left to say it
        /// with.</summary>
        private static void AddIngredient(
            List<Cell> cells,
            AgeTransform widget,
            object stop,
            int index,
            string family
        )
        {
            IngredientItem item = widget == null ? null : widget.GetComponent<IngredientItem>();
            if (item == null || !SettingRows.Drawn(widget))
            {
                return;
            }

            IngredientItem it = item;
            AgeTooltip tooltip = item.Tooltip ?? AgeWidgets.Raw(widget);
            Func<bool> offered = () => AgeWidgets.Operable(widget);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeWidgets.TooltipTitle(tooltip)),
                    GraphNodes.ValuePart(() => AgeText.Label(it.StockLabel)),
                    GraphNodes.DisabledPart(offered),
                },
                Sections = GraphNodes.Sections(null, tooltip),
                OnActivate = () =>
                {
                    if (offered())
                    {
                        AgeWidgets.Press(it.Button);
                    }
                },
            };
            if (!string.IsNullOrEmpty(family))
            {
                string said = family;
                vtable.Announcements.Add(GraphNodes.ValuePart(() => said, false));
            }

            GraphNodes.AddRefusal(vtable, tooltip, offered);

            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(
                cells,
                widget,
                ControlId.Referenced(widget, stop + "/component/" + index),
                vtable
            );
        }

        /// <summary>
        /// The project as it stands: what it would be called, and one node per slot it has.
        ///
        /// A slot holding a component says which one and takes it back out on Enter; an empty one says so
        /// with the game's own sentence about what it is for and refuses, because the game switches its
        /// button off. The line's own button is switched off in this window
        /// (<c>RecipeLine.Bind</c> sets it so in creation mode), so the line is a readout.
        /// </summary>
        private void BuildProject(GraphBuilder builder, RecipeCreationModalWindow window)
        {
            AgeTransform group = window.RecipeContentGroup;
            if (group == null || !AgeWidgets.Visible(group))
            {
                return;
            }

            builder.BeginStop(ProjectStop);
            bool named = AddCaption(builder, group, ProjectStop);
            RecipeLine line = Line(window);
            _cells.Clear();
            if (line != null)
            {
                Cells.AddReadout(
                    _cells,
                    line.RecipeTitleLabel == null ? null : line.RecipeTitleLabel.AgeTransform,
                    "recipe:project/name"
                );
                Cells.EmitLinear(builder, _cells);

                // The caption the line draws over its strip of slots. It captions several controls and
                // carries no explanation of its own, so it names the level the slots sit in rather than
                // taking a node - there is nothing on it that a buffer would have to hold.
                AgeTransform caption = AgeWidgets.ChildNamed(
                    line.AgeTransform,
                    "RecipeIngredients",
                    2
                );
                string label =
                    caption == null || !AgeWidgets.Visible(caption)
                        ? null
                        : AgeWidgets.TextOf(caption);
                bool captioned = !string.IsNullOrEmpty(label);
                if (captioned)
                {
                    builder.PushContext(label);
                }

                _cells.Clear();
                AgeTransform table = line.IngredientSlotsTable;
                IList<AgeTransform> slots = table == null ? null : table.Children;
                for (int i = 0; slots != null && i < slots.Count; i++)
                {
                    AddSlot(_cells, slots[i], i);
                }

                Cells.EmitLinear(builder, _cells);
                if (captioned)
                {
                    builder.PopContext();
                }
            }

            if (named)
            {
                builder.PopContext();
            }
        }

        /// <summary>The click target inside a widget, for a control the game builds as a group with its
        /// button underneath.</summary>
        private static AgeControlButton Press(AgeTransform widget)
        {
            try
            {
                AgeControlButton own = AgeWidgets.Button(widget);
                return own != null ? own : widget.GetComponentInChildren<AgeControlButton>(true);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static RecipeLine Line(RecipeCreationModalWindow window)
        {
            try
            {
                AgeTransform container = window.RecipeContainer;
                return container == null
                    ? null
                    : container.GetComponentInChildren<RecipeLine>(true);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void AddSlot(List<Cell> cells, AgeTransform widget, int index)
        {
            IngredientSlot slot = widget == null ? null : widget.GetComponent<IngredientSlot>();
            if (slot == null || !SettingRows.Drawn(widget))
            {
                return;
            }

            // The slot's click target is a CHILD of it, not the slot itself, and it is the child the game
            // switches off for an empty slot - so an empty slot refuses, which is what the mouse gets.
            IngredientSlot it = slot;
            AgeControlButton button = Press(widget);
            AgeTransform target = AgeWidgets.Transform(button);
            AgeTooltip tooltip = slot.Tooltip ?? AgeWidgets.Raw(widget);
            Func<bool> offered = () => target != null && AgeWidgets.Operable(target);
            bool named = EconomyScreen.Identified(tooltip);
            NodeVtable vtable = GraphNodes.Button(
                () => named ? AgeWidgets.TooltipTitle(tooltip) : CardActions.FirstLine(tooltip),
                () => AgeWidgets.Press(button),
                offered,
                tooltip,
                named ? GraphNodes.ModeFor(tooltip) : TooltipMode.None
            );
            if (named)
            {
                vtable.Announcements.Add(
                    GraphNodes.ValuePart(() => AgeText.Label(it.IngredientAmountLabel))
                );
            }

            GraphNodes.AddRefusal(vtable, tooltip, offered);

            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(cells, widget, ControlId.Referenced(widget, "recipe:slot/" + index), vtable);
        }

        /// <summary>What the project would do, off the lines the game writes into its effects box - each
        /// one a sentence of its own, so each is a line of its own. The special-effect box under it is
        /// only drawn for a project that has one and is read the same way.</summary>
        private void BuildEffects(GraphBuilder builder, RecipeCreationModalWindow window)
        {
            _cells.Clear();
            AddEffects(_cells, window.RecipeEffectMapper, "recipe:effect/");
            AddEffects(
                _cells,
                window.RecipeSpecialEffectMapper,
                "recipe:special-effect/",
                window.RecipeSpecialEffectPanel
            );
            if (_cells.Count == 0)
            {
                return;
            }

            builder.BeginStop(EffectsStop);
            bool named = AddCaption(builder, window.AgeTransform, EffectsStop, "RecipeEffectsGroup");
            Cells.Emit(builder, _cells);
            if (named)
            {
                builder.PopContext();
            }
        }

        private static void AddEffects(
            List<Cell> cells,
            GuiEffectMapper mapper,
            string keyPrefix,
            GuiPanel gate = null
        )
        {
            try
            {
                if (
                    mapper == null
                    || (gate != null && !AgeWidgets.Visible(gate.AgeTransform))
                    || mapper.EffectLinesTable == null
                    || !AgeWidgets.Visible(mapper.EffectLinesTable)
                )
                {
                    return;
                }

                IList<AgeTransform> lines = mapper.EffectLinesTable.Children;
                for (int i = 0; lines != null && i < lines.Count; i++)
                {
                    AgeTransform line = lines[i];
                    if (line != null && SettingRows.Drawn(line))
                    {
                        Cells.AddReadout(cells, line, keyPrefix + i);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("recipe: reading the effects threw: " + e);
            }
        }

        /// <summary>Cancel, Reset and Confirm, taken from the band they share rather than named one by
        /// one, so they read in the order they are drawn. Reset refuses while nothing has been put in;
        /// Confirm refuses while the project is not one the game would accept, and says which of its
        /// three reasons applies (<c>RefreshCreateRecipeButton</c> :269-292).</summary>
        private void BuildActions(GraphBuilder builder, RecipeCreationModalWindow window)
        {
            AgeTransform band = Band(window);
            IList<AgeTransform> children = band == null ? null : band.Children;
            if (children == null)
            {
                return;
            }

            builder.BeginStop(ActionsStop);
            _cells.Clear();
            for (int i = 0; i < children.Count; i++)
            {
                Cells.AddControl(_cells, children[i], "recipe:action/" + i);
            }

            Cells.EmitLinear(builder, _cells);
        }

        private static AgeTransform Band(RecipeCreationModalWindow window)
        {
            try
            {
                AgeTransform confirm = AgeWidgets.Transform(window.CreateRecipeButton);
                return confirm == null ? null : confirm.Parent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The caption the window writes over a box, as the box's name and its first node - the
        /// same three-part treatment every captioned band in the mod gets.</summary>
        private bool AddCaption(
            GraphBuilder builder,
            AgeTransform group,
            object key,
            string named = null
        )
        {
            AgeTransform root =
                named == null ? group : AgeWidgets.ChildNamed(group, named, 3);
            AgeTransform caption = root == null ? null : Caption(root);
            string text = caption == null ? null : AgeWidgets.TextOf(caption);
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            builder.PushContext(text);
            Cell cell = Cells.Readout(caption, AgeWidgets.Raw(caption), key.ToString());
            builder.AddItem(cell.Id, cell.Vtable);
            return true;
        }

        /// <summary>The first label a box draws, which is the caption it draws across its top - the three
        /// boxes in this window name theirs differently ("…Title") so the shape is what finds it.
        /// </summary>
        private static AgeTransform Caption(AgeTransform group)
        {
            try
            {
                IList<AgeTransform> children = group.Children;
                for (int i = 0; children != null && i < children.Count; i++)
                {
                    AgeTransform child = children[i];
                    if (
                        child != null
                        && SettingRows.Drawn(child)
                        && child.GetComponent<AgePrimitiveLabel>() != null
                    )
                    {
                        return child;
                    }
                }
            }
            catch (Exception) { }

            return null;
        }

        private static RecipeCreationModalWindow Window()
        {
            try
            {
                return Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<RecipeCreationModalWindow>(false)
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
