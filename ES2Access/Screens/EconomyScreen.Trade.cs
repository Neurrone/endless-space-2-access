using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The trade tab: the trade companies, the luxury and strategic resource grids, and the
    /// recipes a resource can be turned into.</summary>
    public sealed partial class EconomyScreen
    {
        // ---- the trade and resources tab ----

        /// <summary>The four boxes of the first tab, in the order they are read - the trading companies
        /// down the left, the resource grids and the development projects down the right. The order comes
        /// from where they are drawn, because whether the strategics grid is there at all depends on the
        /// empire (<c>EconomyPanel.Refresh</c> :160-175) and it moves what is under it.</summary>
        private void BuildEconomy(GraphBuilder builder, EconomyPanel panel)
        {
            // Flow control: the four boxes under it are each collected and walked.
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            _bands.Clear();
            Band(_bands, GroupOf(panel.TradeCompaniesPanel));
            Band(_bands, GroupOf(panel.LuxuriesPanel));
            Band(_bands, panel.StrategicsGroup);
            Band(_bands, GroupOf(panel.RecipesPanel));
            _bands.Sort(AgeLayout.TopThenLeft);

            for (int i = 0; i < _bands.Count; i++)
            {
                AgeTransform band = _bands[i];
                if (ReferenceEquals(band, GroupOf(panel.TradeCompaniesPanel)))
                {
                    BuildCompanies(builder, panel.TradeCompaniesPanel, band);
                }
                else if (ReferenceEquals(band, GroupOf(panel.LuxuriesPanel)))
                {
                    BuildResources(
                        builder,
                        LuxuriesStop,
                        band,
                        panel.LuxuriesPanel,
                        panel.LuxuryResourcesHeaderTable,
                        ResourceDefinition.Type.Luxury,
                        EconomyPanel.LuxuriesResourcesFamiliesNumber,
                        _luxuries
                    );
                }
                else if (ReferenceEquals(band, panel.StrategicsGroup))
                {
                    BuildResources(
                        builder,
                        StrategicsStop,
                        band,
                        panel.StrategicsPanel,
                        panel.StrategicResourcesHeaderTable,
                        ResourceDefinition.Type.Strategic,
                        EconomyPanel.StrategicResourcesFamiliesNumber,
                        _strategics
                    );
                }
                else
                {
                    BuildRecipes(builder, panel.RecipesPanel, band);
                }
            }
        }

        /// <summary>
        /// The trading companies, or the one line saying there are none.
        ///
        /// That line is declared BY NAME rather than left to the shape reader: the panel it sits in draws
        /// nothing else, so the shape reader takes the panel as one line and the panel carries no tooltip
        /// - which loses the technology the game names as the thing that is missing. The label carries it
        /// (<c>TradeCompaniesPanel.Refresh</c> :164-172), so the label is the node.
        ///
        /// The companies themselves are read from the shape of what is drawn rather than modelled line by
        /// line: a company draws its name, level, headquarters, income and route counts as a strip of
        /// captioned groups, and the two things it can be TOLD to do - be renamed, and have one of its
        /// two improvements bought - are drawn as controls inside that strip, so they come out as
        /// controls. The panel's own click answers with a technology unlock that only happens in the
        /// developers' god mode (<c>OnPanelCb</c> :215-222), so it is never declared as a button.
        /// </summary>
        private void BuildCompanies(
            GraphBuilder builder,
            TradeCompaniesPanel panel,
            AgeTransform band
        )
        {
            builder.BeginStop(CompaniesStop);
            bool named = AddHeading(builder, band, "economy:companies/heading");
            _cells.Clear();
            try
            {
                if (panel != null)
                {
                    Cells.AddReadout(
                        _cells,
                        panel.NoTradeCompaniesLabel == null
                            ? null
                            : panel.NoTradeCompaniesLabel.AgeTransform,
                        "economy:companies/none"
                    );
                    SidePanels.Content(
                        _cells,
                        panel.TradeCompanyLinesTablesTable,
                        "economy:companies/",
                        null,
                        null
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("economy: reading the trading companies threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            Unname(builder, named);
        }

        /// <summary>
        /// One of the resource grids, read as the table it is drawn as: a row of family icons across the
        /// top, then the resources the empire can see, laid out in the column of the family they belong
        /// to.
        ///
        /// The icons ARE the columns' headings - measured: each sits centred over one column of items -
        /// and each is named by the effect its family improves, taken from the game's own list in the
        /// order the game filled the header table from it (<c>EconomyPanel.RefreshResourcesTables</c>
        /// :187-206). The FAMILY IS MEANINGFUL: the game's own data gives every eighth luxury the same
        /// target effect (<c>GuiElements[Luxuries].xml</c>, <c>RecipeIngredientDefinitions.xml</c>), so
        /// the resource under the Food heading is the one that improves Food - which is why the family
        /// is what the columns are.
        ///
        /// The lattice is SPARSE: the panel keeps every resource in the table and fades the ones the
        /// empire has nothing of (<c>ResourcesPanel.RefreshResourceItem</c> :190-225), which is why the
        /// drawn test asks for alpha and not just visibility - the game leaves those items Visible and
        /// simply makes them invisible. Measured on the luxury grid, turn 21 of the beginner save: 24
        /// items in three lines of eight, the whole third line at alpha 0 and four of the second.
        ///
        /// What the table reading is, and how the families are both its column captions and a header
        /// ROW above the first line, is <see cref="ResourceGrid"/>. BOTH grids read that way (owner
        /// ruling 2026-08-19, amended 2026-08-21): they are the same lattice of the same shape, and a
        /// player who has learnt one has learnt the other.
        /// </summary>
        private void BuildResources(
            GraphBuilder builder,
            object stop,
            AgeTransform band,
            ResourcesPanel panel,
            AgeTransform headers,
            ResourceDefinition.Type type,
            int families,
            ResourceGrid grid
        )
        {
            // Flow control: the grid below reads a whole lattice of cells.
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            builder.BeginStop(stop);
            // The panel's own caption belongs to the same region as the table under it. Set BEFORE the
            // heading rather than in the emitter, because a stop that regions everything except its
            // first node is a stop where the jump key does nothing exactly where the player lands.
            builder.SetRegion(stop + "/legend");
            bool named = AddHeading(builder, band, stop + "/heading");
            string[] columns = null;
            try
            {
                columns = FamilyNames(type, families, grid.Columns(headers));
                grid.Read(panel.ResourceItemsTable, ResourceCell);
            }
            catch (Exception e)
            {
                Log.Warn("economy: reading a resource grid threw: " + e);
            }

            grid.Emit(builder, columns, stop, HeadingText(band));
            Unname(builder, named);
        }

        /// <summary>What each family is called, off the game's own registry, in the order the game
        /// filled the heading table from it. Shared with the recipe window, which draws the same grid of
        /// the same families.</summary>
        internal static string[] FamilyNames(
            ResourceDefinition.Type type,
            int families,
            int drawn
        )
        {
            string[] names = new string[Math.Max(drawn, families)];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = FamilyName(type, families, i);
            }

            return names;
        }

        /// <summary>
        /// What a resource family's column is called - the resource it improves, in the game's own words
        /// for it.
        ///
        /// The heading is drawn as an ICON and nothing else (<c>EconomyPanel.RefreshResourceHeader</c>
        /// :177-185 sets an image, a tint and a tooltip - no label), so what it says is the name of the
        /// thing pictured: "Industry". The family's own title is a sentence about what the family does
        /// ("Improves Industry", and one of them carries an icon typo that reads a second resource's
        /// name), which is a description - it stays where the game puts it, on the heading's tooltip.
        ///
        /// A family with no short name of its own - the compound strategic ones - keeps that title, which
        /// is the only thing the game says about them. A name the corpus never wrote comes back as its
        /// own key - parked text, which is not a name to speak.
        ///
        /// Shared with the recipe window, which draws the same grid of the same families: the copy it
        /// had went straight to the title and so read the family DESCRIPTION - including the icon typo
        /// ("Improves Industry Food") this one exists to step around.
        /// </summary>
        internal static string FamilyName(ResourceDefinition.Type type, int families, int index)
        {
            try
            {
                GuiResource resource = NthResource(type, families, index);
                if (resource == null)
                {
                    return null;
                }

                string improved = ImprovedResource(resource.TargetEffect.ToString());
                if (improved != null)
                {
                    return improved;
                }

                Amplitude.Unity.Gui.ExtendedGuiElement element =
                    Gui.GetExtendedGuiElement(resource.TargetEffect);
                return element == null ? null : AgeText.Title(element.Title);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What each resource family improves, as the game's own short titles for those
        /// resources - the words the rest of the game calls Food and Manpower by. Keyed by the family's
        /// target effect, which is the only thing that says which resource a family is for; a family the
        /// game has no single word for (two resources at once, or an effect that is not a resource at
        /// all) is absent and reads its sentence instead.</summary>
        private static readonly Dictionary<string, string[]> ImprovedResources =
            new Dictionary<string, string[]>
            {
                { "TargetEffectFood", new[] { "%SubCategoryFoodTitle" } },
                { "TargetEffectIndustry", new[] { "%SubCategoryIndustryTitle" } },
                { "TargetEffectDust", new[] { "%SubCategoryDustTitle" } },
                { "TargetEffectScience", new[] { "%SubCategoryScienceTitle" } },
                { "TargetEffectEmpirePoint", new[] { "%SubCategoryInfluenceTitle" } },
                { "TargetEffectHappiness", new[] { "%SubCategoryApprovalTitle" } },
                { "TargetEffectMilitary", new[] { "%CategoryManpowerTitle" } },
                { "TargetEffectTrade", new[] { "%SubCategoryTradeTitle" } },
                { "TargetEffectHonor", new[] { "%HonorTitle" } },
                {
                    "TargetEffectFoodIndustry",
                    new[] { "%SubCategoryFoodTitle", "%SubCategoryIndustryTitle" }
                },
                {
                    "TargetEffectDustScience",
                    new[] { "%SubCategoryDustTitle", "%SubCategoryScienceTitle" }
                },
                {
                    "TargetEffectFoodIndustryDustScience",
                    new[]
                    {
                        "%SubCategoryFoodTitle",
                        "%SubCategoryIndustryTitle",
                        "%SubCategoryDustTitle",
                        "%SubCategoryScienceTitle",
                    }
                },
            };

        /// <summary>See <see cref="ImprovedResources"/>. Null for a family that is not in it, and for one
        /// whose names the corpus never wrote.</summary>
        private static string ImprovedResource(string targetEffect)
        {
            string[] keys;
            if (
                string.IsNullOrEmpty(targetEffect)
                || !ImprovedResources.TryGetValue(targetEffect, out keys)
            )
            {
                return null;
            }

            MessageBuilder names = new MessageBuilder();
            for (int i = 0; i < keys.Length; i++)
            {
                string name = AgeText.Title(keys[i]);
                if (name == null)
                {
                    return null;
                }

                names.ListItem(name);
            }

            string said = names.Build();
            return string.IsNullOrEmpty(said) ? null : said;
        }

        private static GuiResource NthResource(
            ResourceDefinition.Type type,
            int families,
            int index
        )
        {
            System.Collections.Generic.IList<GuiResource> all = Gui.GuiWrapperProviderService.GuiResources;
            int found = 0;
            for (int i = 0; all != null && i < all.Count && found < families; i++)
            {
                if (all[i] != null && all[i].ResourceType == type)
                {
                    if (found == index)
                    {
                        return all[i];
                    }

                    found++;
                }
            }

            return null;
        }

        /// <summary>
        /// One resource in a grid: what it is, how much of it there is and what it is earning per turn.
        ///
        /// The holding and the per-turn change are two adjacent numbers ("0", "+0") with nothing on the
        /// row saying which is which (<c>ResourceItem.Refresh</c> :124-142), so they read as ONE value
        /// through the same template the empire banner's own stocks use - the game leaves the net label
        /// empty for a resource it has no figure for, and then the holding reads alone.
        ///
        /// A resource the empire has not located yet has NO name here. The game draws a question mark
        /// for it and, instead of the resource's own dossier, hangs the one sentence saying it has not
        /// been found (<c>ResourceItem.SetTooltipProperties</c> - the target is set only for a resource
        /// the empire knows). That sentence is what the item is called, and the readout drops that line
        /// from the tooltip it goes on to announce.
        ///
        /// The cell never says which family it belongs to: the column it sits in is announced as the
        /// edge the player crossed to reach it - or, on a landing that crossed no edge, as the column
        /// heading itself - and a word on the cell as well would say it twice. What the family DOES is
        /// on the heading node a step above the column (<see cref="ResourceGrid"/>), not in this cell's
        /// buffer, which holds the resource's own dossier and nothing else.
        /// </summary>
        private static NodeVtable ResourceCell(AgeTransform widget)
        {
            ResourceItem item = widget == null ? null : widget.GetComponent<ResourceItem>();
            if (item == null || !SettingRows.Drawn(widget))
            {
                return null;
            }

            ResourceItem it = item;
            AgeTooltip tooltip = item.Tooltip ?? AgeWidgets.Raw(widget);
            bool named = Identified(tooltip);
            string label = named
                ? AgeWidgets.TooltipTitle(tooltip)
                : CardActions.FirstLine(tooltip);
            NodeVtable vtable = new NodeVtable
            {
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => label),
                    GraphNodes.ValuePart(() => ResourceRows.Figures(it)),
                },
                Sections = GraphNodes.Sections(null, tooltip),
            };
            AgeWidgets.PointAt(vtable, widget);
            return vtable;
        }


        /// <summary>
        /// The system development projects: one line per slot the empire has, or the one line saying it
        /// has none.
        ///
        /// A slot already holding a project is drawn with its button switched off
        /// (<c>RecipeLine.Bind</c> :50-64) - it is a readout of what the project is and what went into
        /// it - and an empty one the empire could fill draws "click to create" and opens the project
        /// window. Enter is that button either way, so a slot the game will not let the player fill
        /// refuses with whatever the game wrote on it.
        /// </summary>
        private void BuildRecipes(GraphBuilder builder, RecipesPanel panel, AgeTransform band)
        {
            // Flow control: a stop and a heading context would be opened around nothing, and every
            // project line under the panel would be read first.
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            builder.BeginStop(RecipesStop);
            bool named = AddHeading(builder, band, "economy:recipes/heading");
            _cells.Clear();
            try
            {
                Cells.AddReadout(
                    _cells,
                    panel.NoAvailableRecipeLabel == null
                        ? null
                        : panel.NoAvailableRecipeLabel.AgeTransform,
                    "economy:recipes/none"
                );
                AgeTransform table = panel.RecipeLinesTable;
                IList<AgeTransform> lines = table == null ? null : table.Children;
                for (int i = 0; lines != null && i < lines.Count; i++)
                {
                    AddRecipeLine(_cells, lines[i], i);
                }
            }
            catch (Exception e)
            {
                Log.Warn("economy: reading the development projects threw: " + e);
            }

            Cells.EmitLinear(builder, _cells);
            Unname(builder, named);
        }

        private static void AddRecipeLine(List<Cell> cells, AgeTransform widget, int index)
        {
            RecipeLine line = widget == null ? null : widget.GetComponent<RecipeLine>();
            if (line == null)
            {
                return;
            }

            RecipeLine it = line;
            AgeTransform press = AgeWidgets.Transform(line.LineButton);
            AgeTooltip tooltip = line.Tooltip ?? AgeWidgets.Raw(widget);
            Func<bool> offered = () => press != null && AgeWidgets.Operable(press);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => AgeText.Label(it.RecipeTitleLabel)),
                    GraphNodes.ValuePart(() => Creatable(it)),
                    GraphNodes.DisabledPart(offered),
                },
                Sections = GraphNodes.Sections(() => Ingredients(it), tooltip),
                OnActivate = () =>
                {
                    if (offered())
                    {
                        AgeWidgets.Press(it.LineButton);
                    }
                },
            };

            AgeWidgets.PointAt(vtable, widget);
            Cells.Add(cells, widget, ControlId.For(widget, "economy:recipe/" + index), vtable);
        }

        /// <summary>The invitation the game writes on an empty slot the empire could fill, and nothing on
        /// a slot already holding a project.</summary>
        private static string Creatable(RecipeLine line)
        {
            try
            {
                return AgeWidgets.DrawnLabel(line.ClickToCreateRecipeTitleLabel);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What went into a project, off the row of slots the line draws - each one a picture
        /// with the resource's own words on it and nothing written.</summary>
        private static IList<string> Ingredients(RecipeLine line)
        {
            List<string> lines = new List<string>();
            try
            {
                AgeTransform table = line.IngredientSlotsTable;
                IList<AgeTransform> slots = table == null ? null : table.Children;
                for (int i = 0; slots != null && i < slots.Count; i++)
                {
                    AgeTransform slot = slots[i];
                    if (slot == null || !SettingRows.Drawn(slot))
                    {
                        continue;
                    }

                    AgeTooltip tooltip = AgeWidgets.Raw(slot);
                    string said =
                        AgeWidgets.TooltipTitle(tooltip) ?? CardActions.FirstLine(tooltip);
                    if (!string.IsNullOrEmpty(said) && !lines.Contains(said))
                    {
                        lines.Add(said);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("economy: reading a project's components threw: " + e);
            }

            return lines;
        }
    }
}
