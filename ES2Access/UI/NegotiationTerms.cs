using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Screens;

namespace ES2Access.UI
{
    /// <summary>
    /// The terms of a treaty, wherever the negotiation table draws them: the two shelves of things that
    /// COULD go into the deal (one per empire, <c>NegotiationTermsPanel</c>) and the basket of things that
    /// are in it (<c>NegotiationContributionPanel</c>). All three are tables of the same prefab family -
    /// <c>ContributionTermLine</c> derives from <c>TermLine</c> - so the row is written once here and each
    /// panel only says which table it drew and what extra a row of its own carries.
    ///
    /// <b>A shelf is a real table</b> and is declared as one: the game draws headers over it
    /// (<c>%NegotiationModalWindowTermTypeHeaderTitle</c> / <c>…NameHeaderTitle</c> /
    /// <c>…CostHeaderTitle</c>), so the columns are its own. One deviation from the drawn order: the type
    /// is drawn FIRST and is a bare icon, while a sheet's column 0 has to be the row's name - so the name
    /// leads and the type follows it, and a row still reads type-and-cost as it is walked across.
    ///
    /// <b>A row is keyed on the term, not on the line</b>: the tables are pooled and re-bound by index on
    /// every refresh (<c>ReserveChildren</c> / <c>RefreshChildrenIList</c>), so a cursor keyed on the
    /// widget would act on a different term a frame after a filter changed.
    ///
    /// <b>Enter toggles the term in and out of the deal.</b> It is the row's own click
    /// (<c>TermLine.OnSelectTermCb</c> :396-402), which the window answers by posting
    /// <c>OrderChangeDiplomaticContractTermsCollection</c> with Add or Remove - so the same key that puts a
    /// term in the basket takes it out again, from either side. A term the game will not accept is left
    /// DRAWN and switched off with its failure sentences on its own tooltip, and reads refusing.
    ///
    /// <b>There are hundreds of terms</b> - every resource, every technology, every system - so type-ahead
    /// is the practical way through them. It comes free: every cell of a sheet row matches on the row's
    /// name.
    /// </summary>
    public static class NegotiationTerms
    {
        /// <summary>The game's own headers over a shelf of terms, in the order the sheet's columns are:
        /// the NAME first, because that is the column a row's own cell is, then type and cost. All three
        /// are the game's, drawn over the shelf; the sheet says each as the edge crossed into its
        /// column, the name's included.</summary>
        public static string[] Columns()
        {
            return new string[]
            {
                Localized("%NegotiationModalWindowTermNameHeaderTitle"),
                Localized("%NegotiationModalWindowTermTypeHeaderTitle"),
                Localized("%NegotiationModalWindowTermCostHeaderTitle"),
            };
        }

        /// <summary>
        /// The strip of category filters over a shelf - All, Treaty, Resource, Technology, System, and on
        /// the player's own side the contextual terms.
        ///
        /// They are RADIOS because that is what the panel makes them: ticking one resets every other
        /// (<c>OnToggleFilter</c> :477-497). Most are drawn as bare icons, so a filter is named by the
        /// game's own title for its category rather than by what the widget draws.
        /// </summary>
        public static void Filters(
            GraphBuilder builder,
            NegotiationTermsPanel panel,
            string keyPrefix
        )
        {
            AgeTransform table = panel == null ? null : panel.TermFiltersTable;
            IList<AgeTransform> children = table == null ? null : table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform widget = children[i];
                TermTypeFilter filter = widget == null
                    ? null
                    : widget.GetComponent<TermTypeFilter>();
                if (filter == null)
                {
                    continue;
                }

                TermTypeFilter it = filter;
                AgeControlToggle toggle = filter.Toggle;
                NodeVtable vtable = GraphNodes.Radio(
                    () => FilterName(it),
                    () => toggle != null && toggle.State,
                    () => AgeWidgets.Toggle(toggle),
                    () => AgeWidgets.Offered(AgeWidgets.Transform(toggle)),
                    () => Alert(it),
                    filter.Tooltip
                );
                AgeWidgets.Point(vtable, toggle, filter.Tooltip, widget);
                ScrollIntoView.Anchor(vtable, widget);
                // Keyed by the CATEGORY, since the strip's widgets are a pool the panel rebinds - but
                // the widget the filter was read off is the tick the game draws, and that is what
                // vouches for it.
                builder.AddItem(Nodes.Drawn(
                    ControlId.Structural(keyPrefix + "/filter/" + FilterKey(it, i)),
                    vtable,
                    widget
                ));
            }
        }

        /// <summary>A filter's own name: the label where the game drew one, else its category's title -
        /// these are drawn as icons wherever the category has an icon at all
        /// (<c>TermTypeFilter.Bind</c> :47-77).</summary>
        private static string FilterName(TermTypeFilter filter)
        {
            try
            {
                // Content: which STRING names the filter.
                string drawn = filter.Label != null && AgeWidgets.Visible(filter.Label.AgeTransform)
                    ? AgeText.Label(filter.Label)
                    : null;
                return string.IsNullOrEmpty(drawn) ? Title("TermTypeFilter" + filter.Category) : drawn;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The badge a filter wears when terms the player has not seen have become available in
        /// its category - the same wordless mark the diplomacy ring paints on an empire, so it is read
        /// with the same words.</summary>
        private static IList<string> Alert(TermTypeFilter filter)
        {
            try
            {
                return filter.ContextualAlertMarker != null
                    // Content: whether the badge contributes a line.
                    && AgeWidgets.Visible(filter.ContextualAlertMarker)
                    ? new List<string> { ModStrings.Get(ModStrings.DiplomacyNewOptions) }
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string FilterKey(TermTypeFilter filter, int index)
        {
            try
            {
                return string.IsNullOrEmpty(filter.Category)
                    ? index.ToString()
                    : filter.Category;
            }
            catch (Exception)
            {
                return index.ToString();
            }
        }

        /// <summary>Every term the shelf is drawing, as rows of <paramref name="sheet"/>. A shelf the
        /// game has emptied contributes no rows, and the caller's empty-state words stand in its
        /// place.</summary>
        public static int Shelf(GraphSheet sheet, AgeTransform table, string keyPrefix)
        {
            int count = 0;
            IList<AgeTransform> children = table == null ? null : table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform widget = children[i];
                TermLine line = widget == null ? null : widget.GetComponent<TermLine>();
                // Which rows the sheet HAS - the row keys and their count are settled here, before any
                // cell is declared, and the pool keeps retired lines around.
                if (line == null || line.GuiTerm == null || !AgeWidgets.Visible(widget))
                {
                    continue;
                }

                TermLine it = line;
                sheet.Row(
                    Primary(it, keyPrefix),
                    Key(it, keyPrefix),
                    widget,
                    () => TypeName(it),
                    () => Words(it.CostLabel)
                );
                count++;
            }

            return count;
        }

        /// <summary>
        /// One line of the basket, which is a shelf row plus the two things only the basket draws: the
        /// stepper a resource term is haggled with, and the consequences the term spells out under itself.
        ///
        /// The stepper is ONE cell, not three: the game draws a minus button, a number in a text box and a
        /// plus button, and they are one value between them. Left and right replay the game's own buttons -
        /// which read the PHYSICAL Shift for "all the way to the end" and the physical Control for five at
        /// a time, so Shift+Left/Right is the game's own min and max with nothing here reimplementing it.
        /// Enter opens the game's editor on the box, because a stock of thousands is not something to step
        /// to. Each change is committed to the contract half a second later, by the game's own debounce
        /// (<c>UpdateQuantityCoroutine</c> :333-350), so holding a key does not post an order per frame.
        ///
        /// Control+Left/Right - the game's five-at-a-time step - is NOT bound: those are the review-buffer
        /// chords. A player who wants five presses right five times.
        /// </summary>
        public static int Basket(
            GraphSheet sheet,
            AgeTransform table,
            string keyPrefix,
            TextFieldEditor editor
        )
        {
            int count = 0;
            IList<AgeTransform> children = table == null ? null : table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform widget = children[i];
                ContributionTermLine line = widget == null
                    ? null
                    : widget.GetComponent<ContributionTermLine>();
                // Which rows the sheet HAS - the row keys and their count are settled here, before any
                // cell is declared, and the pool keeps retired lines around.
                if (line == null || line.GuiTerm == null || !AgeWidgets.Visible(widget))
                {
                    continue;
                }

                ContributionTermLine it = line;
                List<KeyValuePair<int, NodeVtable>> cells =
                    new List<KeyValuePair<int, NodeVtable>>(3);
                TermLine row = it;
                cells.Add(
                    new KeyValuePair<int, NodeVtable>(1, Text(() => TypeName(row)))
                );
                cells.Add(
                    new KeyValuePair<int, NodeVtable>(2, Text(() => Words(row.CostLabel)))
                );
                NodeVtable quantity = Quantity(it, keyPrefix, editor);
                if (quantity != null)
                {
                    cells.Add(new KeyValuePair<int, NodeVtable>(3, quantity));
                }

                sheet.RowAt(Primary(it, keyPrefix), Key(it, keyPrefix), cells, widget);
                count++;
            }

            return count;
        }

        /// <summary>The stepper cell of a basket line, or nothing at all for a term that is not
        /// quantified (a treaty is in or out).</summary>
        private static NodeVtable Quantity(
            ContributionTermLine line,
            string keyPrefix,
            TextFieldEditor editor
        )
        {
            AgeControlTextField field = line.QuantityTextField;
            if (
                field == null
                || line.QuantityGroup == null
                // Whether the row HAS a stepper cell - a column of the sheet, decided before the row is
                // built.
                || !AgeWidgets.Visible(line.QuantityGroup)
            )
            {
                return null;
            }

            ContributionTermLine it = line;
            AgeControlTextField box = field;
            AgeTransform host = AgeWidgets.Transform(field);
            Func<bool> enabled = () => AgeWidgets.Operable(line.QuantityGroup);
            NodeVtable vtable = GraphNodes.EditField(
                () => ModStrings.Get(ModStrings.NegotiationQuantity),
                () => TextFieldEditor.Typing(box) ? null : AgeWidgets.TextOf(host),
                () => Edit(editor, box, keyPrefix, it),
                enabled,
                AgeWidgets.Raw(host)
            );
            // The arrows work the stepper here rather than a caret, which is the whole of the
            // difference between this box and every other one - so the role word says so.
            vtable.ControlType = ControlTypes.NumericEditField;
            vtable.OnAdjust = (sign, large) =>
            {
                if (!enabled())
                {
                    return;
                }

                AgeWidgets.Press(sign < 0 ? it.QuantityMinusButton : it.QuantityPlusButton);
            };
            AgeWidgets.PointAt(vtable, host);
            return vtable;
        }

        private static void Edit(
            TextFieldEditor editor,
            AgeControlTextField field,
            string keyPrefix,
            ContributionTermLine line
        )
        {
            if (editor != null)
            {
                editor.Request(field, null, null, RowId(line, keyPrefix + "/quantity"));
            }
        }

        /// <summary>
        /// The row itself: what the term is, and the click that puts it in the deal or takes it out.
        ///
        /// The name is the one the line DRAWS, because the game has already written the quantity and the
        /// cooldown into it (<c>RefreshNameLabel</c> :87-102: "Titanium (12)", "Peace Treaty (5 turns)").
        /// The computer's opinion of the term - a bare "+" or "-" the game paints beside the name when it
        /// is being asked of an AI (<c>RefreshAIFeedback</c> :333-363) - is read as the value it is, and
        /// only while the game is drawing it.
        /// </summary>
        private static NodeVtable Primary(TermLine line, string keyPrefix)
        {
            TermLine it = line;
            AgeTooltip tooltip = line.Tooltip;
            Func<bool> offered = () => AgeWidgets.Offered(it.AgeTransform);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => Words(it.NameLabel)),
                    GraphNodes.DisabledPart(offered),
                    GraphNodes.ValuePart(() => Words(it.AIPreviewLabel)),
                },
                Sections = GraphNodes.Sections(() => Consequences(it), tooltip),
                OnActivate = () =>
                {
                    if (offered())
                    {
                        AgeWidgets.Press(it.AgeTransform);
                    }
                },
            };
            GraphNodes.AddRefusal(vtable, tooltip, offered);
            AgeWidgets.PointAt(vtable, it.AgeTransform);
            return vtable;
        }

        /// <summary>What the line spells out under itself about signing this term - the upkeep a treaty
        /// costs, who else a declaration of war would drag in (<c>BuildDeclareWarConsequences</c>
        /// :107-140). Drawn, so read as drawn.</summary>
        private static IList<string> Consequences(TermLine line)
        {
            try
            {
                AgeTransform table = line.ConsequencesTable;
                // Content: which drawn lines feed a tooltip section.
                return table == null || !AgeWidgets.Visible(table)
                    ? null
                    : AgeWidgets.DrawnLines(table);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The category the term belongs to, in the game's own word for it - the same word its
        /// filter is labelled with. The type COLUMN is drawn as an icon and nothing else, so there is
        /// nothing on the widget to read.</summary>
        private static string TypeName(TermLine line)
        {
            try
            {
                IGuiDiplomaticTerm term = line.GuiTerm;
                return term == null ? null : Title("TermTypeFilter" + term.Category);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The row's identity: the TERM's own name, never the pooled line's.</summary>
        private static string Key(TermLine line, string keyPrefix)
        {
            try
            {
                IGuiDiplomaticTerm term = line.GuiTerm;
                return keyPrefix
                    + "/term/"
                    + (term == null ? "?" : term.Name.ToString())
                    + "/"
                    + (term == null ? "?" : term.ApplicationMethod.ToString());
            }
            catch (Exception)
            {
                return keyPrefix + "/term/?";
            }
        }

        private static ControlId RowId(TermLine line, string keyPrefix)
        {
            return ControlId.Structural(Key(line, keyPrefix));
        }

        private static NodeVtable Text(Func<string> value)
        {
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GraphNodes.LabelPart(value) },
            };
        }

        private static string Words(AgePrimitiveLabel label)
        {
            try
            {
                // Content: which STRING is returned.
                return label == null || !AgeWidgets.Visible(label.AgeTransform)
                    ? null
                    : AgeText.Label(label);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Title(string guiElementName)
        {
            try
            {
                return AgeText.Clean(Gui.GetLocalizedTitle(guiElementName));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Localized(string key)
        {
            try
            {
                return AgeText.Clean(Gui.Localize(key));
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
