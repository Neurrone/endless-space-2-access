using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    public static partial class ShipDesignRows
    {
        // ---- the module list ----

        /// <summary>
        /// The modules the empire can fit, the two switches that decide which of them are drawn, and
        /// the strip itself.
        ///
        /// The category strip is a <c>GuiRadioGroup</c> - exactly one filter is in force - and its
        /// toggles are drawn as bare icons except the first. What names them is the game's own word for
        /// the module category each one keeps, which the toggle's index gives (see
        /// <see cref="CategoryTitles"/>).
        ///
        /// Two regions: the switches that decide what is drawn, and what is drawn. The game draws them
        /// as one strip across the top and a wrapping list under it. The switches stay ONE row - the
        /// same reading the star system's constructible filters get (owner ruling): they are a
        /// select-one group the panel re-derives from the filter in force, and the row they are drawn in
        /// is the row the player walks. The modules under them are one per row, because a wrapping grid
        /// of tiles wraps where the table ran out of width. The band's own "Modules" caption is the
        /// stop's name.
        ///
        /// The game captions neither half, so each carries a word of the mod's own as its LEVEL -
        /// "Filters" over the switches, "Available" over the list. Without them the two halves are told
        /// apart only by what happens to be under the cursor, and a jump between them lands on a row
        /// with nothing saying which half it is in.
        /// </summary>
        private static void BuildModules(
            GraphBuilder builder,
            ShipDesignEditionPanel panel,
            string prefix,
            List<Cell> cells
        )
        {
            bool labelled = false;
            try
            {
                // Flow control: a stop and a caption context would be opened around nothing, and the
                // whole module list would be walked.
                if (panel.ModulesGroup == null || !AgeWidgets.Visible(panel.ModulesGroup))
                {
                    return;
                }

                builder.BeginStop(ModulesStop(prefix));
                // The panel names the label it writes "Modules" into after the statistics box it was
                // copied from; what it DRAWS is the caption over this band.
                labelled = Caption(builder, FirstLabel(panel.ModulesGroup));

                builder.SetRegion(prefix + "/modules/filters");
                builder.PushContext(ModStrings.Get(ModStrings.ShipDesignFilters));
                try
                {
                    cells.Clear();
                    AddCategories(cells, panel, prefix);
                    AddObsolete(cells, panel, prefix);
                    Cells.Emit(builder, cells);
                }
                finally
                {
                    builder.PopContext();
                }

                builder.SetRegion(prefix + "/modules/list");
                builder.PushContext(ModStrings.Get(ModStrings.ShipDesignAvailable));
                try
                {
                    cells.Clear();
                    AgeTransform table = panel.ModulesTable;
                    ShipDesignModuleItem[] items = Modules.Under(table);
                    for (int i = 0; i < items.Length; i++)
                    {
                        AddModule(cells, panel, items[i], prefix, i);
                    }

                    EmitLinear(builder, cells);
                }
                finally
                {
                    builder.PopContext();
                }
            }
            catch (Exception e)
            {
                Log.Warn("ship design: reading the module list threw: " + e);
            }
            finally
            {
                if (labelled)
                {
                    builder.PopContext();
                }
            }
        }

        private static void AddCategories(
            List<Cell> cells,
            ShipDesignEditionPanel panel,
            string prefix
        )
        {
            GuiRadioGroup group = panel.ModuleCategoriesGroup;
            AgeTransform table = group == null ? null : group.TogglesTable;
            // Flow control: the toggles are found by a component scrape.
            if (table == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            AgeControlToggle[] toggles = Categories.Under(table);
            for (int i = 0; i < toggles.Length; i++)
            {
                AgeControlToggle toggle = toggles[i];
                AgeTransform widget = AgeWidgets.Transform(toggle);
                if (toggle == null)
                {
                    continue;
                }

                AgeControlToggle it = toggle;
                int index = i;
                AgeTooltip tooltip = AgeWidgets.Raw(widget);
                NodeVtable vtable = GraphNodes.Radio(
                    () => CategoryName(widget, index, tooltip),
                    () => it.State,
                    () => AgeWidgets.Toggle(it),
                    () => AgeWidgets.Operable(widget),
                    null,
                    tooltip
                );
                AgeWidgets.Point(vtable, it);
                Cells.Add(
                    cells,
                    widget,
                    ControlId.For(toggle, prefix + "/modules/category/" + i),
                    vtable
                );
            }
        }

        /// <summary>What a category toggle is called: the word it draws where it draws one, else the
        /// game's own title for the module category it keeps, else the sentence it explains itself
        /// with.</summary>
        private static string CategoryName(AgeTransform widget, int index, AgeTooltip tooltip)
        {
            string drawn = AgeWidgets.TextOf(widget);
            if (!string.IsNullOrEmpty(drawn))
            {
                return drawn;
            }

            string element = index >= 0 && index < CategoryTitles.Length
                ? CategoryTitles[index]
                : null;
            string title = element == null ? null : AgeText.Title(Gui.GetTitle(element));
            return title ?? CardActions.FirstLine(tooltip);
        }

        private static void AddObsolete(
            List<Cell> cells,
            ShipDesignEditionPanel panel,
            string prefix
        )
        {
            AgeControlToggle toggle = panel.ShowObsoleteModulesToggle;
            AgeTransform widget = AgeWidgets.Transform(toggle);
            if (toggle == null)
            {
                return;
            }

            AgeControlToggle it = toggle;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            NodeVtable vtable = GraphNodes.Checkbox(
                () => AgeWidgets.TextOf(widget),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Operable(widget),
                tooltip
            );
            AgeWidgets.Point(vtable, it);
            Cells.Add(
                cells,
                widget,
                ControlId.For(toggle, prefix + "/modules/obsolete"),
                vtable
            );
        }

        /// <summary>
        /// One module the empire could fit. The tile draws a picture and nothing else, so the name is
        /// the one the game keeps on the wrapper behind its tooltip - and the dossier that tooltip
        /// assembles (what the module does, what it costs) is indicated and walkable in the review
        /// buffer rather than recited on every pass.
        ///
        /// Keyed on the WRAPPER rather than on the tile: the strip pools its tiles and rebinds them
        /// whenever the filter or the obsolete switch changes what is drawn, while the wrappers
        /// themselves are built once (<c>CreateGuiModulesByFamilies</c> :807-829) and outlive every
        /// refresh.
        ///
        /// A module the empire cannot fit yet is drawn disabled with its reasons on the wrapper
        /// (<c>ShipDesignModuleItem.Bind</c> :31), which is where the refusal comes from.
        /// </summary>
        private static void AddModule(
            List<Cell> cells,
            ShipDesignEditionPanel panel,
            ShipDesignModuleItem item,
            string prefix,
            int index
        )
        {
            if (item == null || item.GuiEditionModule == null)
            {
                return;
            }

            ShipDesignModuleItem it = item;
            AgeTooltip tooltip = item.Tooltip ?? AgeWidgets.Raw(item.AgeTransform);
            Func<bool> enabled = () => AgeWidgets.Operable(it.AgeTransform);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => ModuleName(it)),
                    GraphNodes.DisabledPart(enabled),
                },
                Sections = GraphNodes.Sections(null, tooltip),
                // A single click on a module tile does nothing at all (UseLeftClick is false, measured),
                // so Enter does nothing either; the double click is the auto-equip.
                OnDoubleClick = () => AutoEquip(it, enabled),
                OnPickUp = () => PickModule(it),
            };

            AgeWidgets.PointAt(vtable, item.AgeTransform);
            Cells.Add(
                cells,
                item.AgeTransform,
                ControlId.For(item.GuiEditionModule, prefix + "/module/" + index),
                vtable
            );
        }

        /// <summary>Put the module in the first slot that will take it - the tile's own double click,
        /// which is the only activation the game gives it.</summary>
        private static void AutoEquip(ShipDesignModuleItem item, Func<bool> enabled)
        {
            if (!enabled())
            {
                return;
            }

            AgeWidgets.DoubleClick(AgeWidgets.Button(AutoEquipButton(item)));
        }

        /// <summary>The tile's own button - a child of the drag area rather than the tile itself, which
        /// is where the game hangs the double click.</summary>
        private static AgeTransform AutoEquipButton(ShipDesignModuleItem item)
        {
            try
            {
                AgeControlButton button =
                    item.AgeTransform.GetComponentInChildren<AgeControlButton>(true);
                return button == null ? null : button.AgeTransform;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string ModuleName(ShipDesignModuleItem item)
        {
            try
            {
                return item.GuiEditionModule == null
                    ? null
                    : AgeText.Clean(item.GuiEditionModule.Title);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
