using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
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
    /// <c>…CostHeaderTitle</c>), so the columns are its own, and the headings are declared as a real
    /// header row over them (<see cref="Headers"/>). One deviation from the drawn order: the type is
    /// drawn FIRST and is a bare icon, while a sheet's column 0 has to be the row's name - so the name
    /// leads and the type follows it, and a row still reads type-and-cost as it is walked across. The
    /// heading band is declared in that same order, so that Up out of a cell reaches the heading of the
    /// column the player was standing in.
    ///
    /// <b>A row is keyed on the term, not on the line</b>: the tables are pooled and re-bound by index on
    /// every refresh (<c>ReserveChildren</c> / <c>RefreshChildrenIList</c>), so a cursor keyed on the
    /// widget would act on a different term a frame after a filter changed.
    ///
    /// <b>Enter toggles the term in and out of the deal.</b> It sends the window the same message the
    /// row's own click sends it (<c>TermLine.OnSelectTermCb</c> :398-403), which the window answers by
    /// posting <c>OrderChangeDiplomaticContractTermsCollection</c> with Add or Remove - so the same key
    /// that puts a term in the basket takes it out again, from either side, and the row says which of the
    /// two it now is. A term the game will not accept is left DRAWN and switched off with its failure
    /// sentences on its own tooltip, and reads refusing.
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
                AgeText.Title("%NegotiationModalWindowTermNameHeaderTitle"),
                AgeText.Title("%NegotiationModalWindowTermTypeHeaderTitle"),
                AgeText.Title("%NegotiationModalWindowTermCostHeaderTitle"),
            };
        }

        /// <summary>The prefab names of the three headings the game draws over a shelf, in the order
        /// <see cref="Columns"/> puts them - name first, which is the shelf's own identity column and
        /// therefore column 0, then the type and the cost. The game DRAWS them type-name-cost; the
        /// sheet's identity column has to be column 0, so the band is declared in the sheet's order and
        /// the first two headings read left-to-right in the other one (owner ruling 2026-08-27: the
        /// alignment between a heading and the cells under it is what a header row is for, and the
        /// sheet is not to be extended to move an identity column).</summary>
        private static readonly string[] HeaderNames = { "Name", "Type", "Cost" };

        /// <summary>
        /// The row of column headings over a shelf, one node per heading the game drew.
        ///
        /// The same shape the economy grid's family band has (<c>ResourceGrid.Headings</c>) and the same
        /// shape a sort band has minus the press: these headings sort nothing, so nothing is wired to
        /// Enter and a press on one answers with nothing, which is what a click there does. What each
        /// node is FOR is the sentence the game hung on it - what a Type is, what a Name specifies, what
        /// the Cost is counted in - which lives on the heading and nowhere else, and would otherwise be
        /// repeated into the buffer of every cell of the column.
        ///
        /// Each is stamped with the column it stands over (<see cref="NodeVtable.Column"/>), which is
        /// what <c>GraphBuilder.StitchModeBoundaries</c> pairs the seam by: Up out of a cell reaches the
        /// heading of the column the player was in rather than the first one. Searched by their own
        /// words, since a heading is not a cell of the rows below it.
        /// </summary>
        public static void Headers(
            GraphBuilder builder,
            NegotiationTermsPanel panel,
            string keyPrefix
        )
        {
            AgeTransform band = panel == null
                ? null
                : AgeWidgets.ChildNamed(panel.AgeTransform, "TermsHeader", 3);
            // Flow control: whether a heading band is opened at all.
            if (band == null || !AgeWidgets.Visible(band))
            {
                return;
            }

            bool open = false;
            for (int i = 0; i < HeaderNames.Length; i++)
            {
                AgeTransform widget = AgeWidgets.ChildNamed(band, HeaderNames[i], 1);
                if (widget == null)
                {
                    continue;
                }

                AgeTransform it = widget;
                AgeTooltip tooltip = AgeWidgets.Raw(widget);
                NodeVtable vtable = new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => AgeWidgets.TextOf(it)),
                    },
                    Sections = GraphNodes.Sections(null, tooltip),
                    Column = i,
                    SearchesAsItself = true,
                };
                AgeWidgets.PointAt(vtable, widget, tooltip);
                if (!open)
                {
                    builder.StartRow(positions: false);
                    open = true;
                }

                ScrollIntoView.Anchor(vtable, widget);
                builder.AddItem(Nodes.Drawn(
                    ControlId.For(widget, keyPrefix + "/header/" + HeaderNames[i]),
                    vtable,
                    widget
                ));
            }

            if (open)
            {
                builder.EndRow();
            }
        }

        /// <summary>
        /// The strip of category filters over a shelf - All, Treaty, Resource, Technology, System, and on
        /// the player's own side the contextual terms.
        ///
        /// They are RADIOS because that is what the panel makes them: ticking one resets every other
        /// (<c>OnToggleFilter</c> :477-497). Most are drawn as bare icons, so a filter is named by the
        /// game's own title for its category rather than by what the widget draws.
        ///
        /// <b>ONE row, walked with left and right</b>, because that is what the game draws: six icons
        /// side by side in a strip, one of which is in force. A node per row made six Down presses out
        /// of a strip the eye takes in at once (owner-reported 2026-08-27). And the strip is ENTERED at
        /// the filter currently in force rather than at its first: arriving on "All" while the shelf is
        /// showing resources says the wrong thing about what the player is looking at, and the landing
        /// is the one place a one-of-N group can say which one it is without a word per member.
        /// </summary>
        public static void Filters(
            GraphBuilder builder,
            NegotiationTermsPanel panel,
            string keyPrefix
        )
        {
            AgeTransform table = panel == null ? null : panel.TermFiltersTable;
            IList<AgeTransform> children = table == null ? null : table.Children;
            // Flow control: whether a row is opened at all - an empty row is a build error, and the
            // strip is a pool the panel may have drawn nothing in.
            if (children == null || children.Count == 0)
            {
                return;
            }

            ControlId landing = null;
            bool open = false;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                // The strip is a pool like the shelf below it, so a category the panel has stopped
                // offering is a faded tick rather than a removed one, and only the fade says so.
                AgeTransform widget = AgeWidgets.DrawnChild(children, i);
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
                ControlId id = ControlId.Structural(keyPrefix + "/filter/" + FilterKey(it, i));
                if (!open)
                {
                    builder.StartRow();
                    open = true;
                }

                builder.AddItem(Nodes.Drawn(id, vtable, widget));
                // Which one the strip is entered at. Asked of the tick the game drew rather than
                // remembered, so a filter the game itself changed is where the player arrives.
                if (landing == null && toggle != null && toggle.State)
                {
                    landing = id;
                }
            }

            if (open)
            {
                builder.EndRow();
            }

            if (landing != null)
            {
                builder.LandStopOn(landing);
            }
        }

        /// <summary>A filter's own name: the label where the game drew one, else its category's title -
        /// these are drawn as icons wherever the category has an icon at all
        /// (<c>TermTypeFilter.Bind</c> :47-77).</summary>
        private static string FilterName(TermTypeFilter filter)
        {
            try
            {
                string drawn = AgeWidgets.DrawnLabel(filter.Label);
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
        public static int Shelf(
            GraphSheet sheet,
            AgeTransform table,
            string keyPrefix,
            NegotiationModalWindow window
        )
        {
            int count = 0;
            _seen.Clear();
            IList<AgeTransform> children = table == null ? null : table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                // Which rows the sheet HAS - the row keys, their count and the "3 of 9" every cell is
                // stamped with are all settled here, before a cell is declared.
                //
                // RETIREMENT ON THIS TABLE IS THE FADE, NOT THE BINDING. The panel sizes its pool with
                // ReserveChildren, which only ever GROWS it, and then RefreshChildrenIList
                // (firstpass/AgeTransform.cs :2404-2414) sets every child past the list's end to
                // Alpha 0 without calling the refresh delegate on it - so a surplus line keeps the term
                // it was last bound to and stays Visible. TermLine.Unbind (:69-77) would clear GuiTerm,
                // but the panel only calls it when the whole panel is torn down. Measured 2026-08-27 on
                // a shelf of five: the table held nine bound children, rows 5-8 being stale duplicates
                // of rows 1-4, and asking Visible declared all nine. The gate then withheld the four
                // unpainted ones, which is the gate working - but the count had already promised nine
                // rows, so the table said "5 of 9" and Down off row 5 reached nothing
                // (owner-reported). DrawnChild is the blessed way through a pooled table's children
                // and asks the one question that separates a live row from a retired one.
                AgeTransform widget = AgeWidgets.DrawnChild(children, i);
                TermLine line = widget == null ? null : widget.GetComponent<TermLine>();
                if (line == null || line.GuiTerm == null)
                {
                    continue;
                }

                TermLine it = line;
                NegotiationModalWindow owner = window;
                sheet.Row(
                    Primary(it, window, () => InContract(owner, it)),
                    Distinct(Key(it, keyPrefix)),
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
        /// plus button, and they are one value between them. Enter opens the game's editor on the box and
        /// the figure is TYPED - a stock of thousands is not something to step to, and the game's own
        /// buttons step by one, five or the whole stock only for a mouse holding a physical modifier.
        /// Left and right on the focused cell walk the row's columns like every other cell in the mod;
        /// they are not wired to the buttons (owner ruling 2026-08-27, reversing the shipped stepper:
        /// arrows that move a value the player only meant to walk past are a value changed by accident).
        /// Whatever the edit commits is written to the contract half a second later, by the game's own
        /// debounce (<c>UpdateQuantityCoroutine</c> :333-350).
        /// </summary>
        public static int Basket(
            GraphSheet sheet,
            AgeTransform table,
            string keyPrefix,
            TextFieldEditor editor,
            NegotiationModalWindow window
        )
        {
            int count = 0;
            _seen.Clear();
            IList<AgeTransform> children = table == null ? null : table.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                // Which rows the sheet HAS, by the same reading the shelf uses: the basket is pooled by
                // the same ReserveChildren/RefreshChildrenIList pair, so a line it has finished with is
                // a faded one and only the fade says so. This table happened to be caught with its
                // surplus line unbound as well, which is belt and braces rather than the rule.
                AgeTransform widget = AgeWidgets.DrawnChild(children, i);
                ContributionTermLine line = widget == null
                    ? null
                    : widget.GetComponent<ContributionTermLine>();
                if (line == null || line.GuiTerm == null)
                {
                    continue;
                }

                ContributionTermLine it = line;
                // Settled once and used for both the row and the stepper's edit request: the two are
                // the same row, so a repeat that needed disambiguating needs it in both places.
                string rowKey = Distinct(Key(it, keyPrefix));
                List<KeyValuePair<int, NodeVtable>> cells =
                    new List<KeyValuePair<int, NodeVtable>>(3);
                TermLine row = it;
                cells.Add(
                    new KeyValuePair<int, NodeVtable>(1, Text(() => TypeName(row)))
                );
                cells.Add(
                    new KeyValuePair<int, NodeVtable>(2, Text(() => Words(row.CostLabel)))
                );
                NodeVtable quantity = Quantity(it, editor);
                if (quantity != null)
                {
                    cells.Add(new KeyValuePair<int, NodeVtable>(3, quantity));
                }

                // No membership word here: everything in the basket is in the deal by definition, and a
                // list where every row says "selected" says nothing.
                sheet.RowAt(Primary(it, window, null), rowKey, cells, widget);
                count++;
            }

            return count;
        }

        /// <summary>The quantity cell of a basket line, or nothing at all for a term that is not
        /// quantified (a treaty is in or out).</summary>
        private static NodeVtable Quantity(ContributionTermLine line, TextFieldEditor editor)
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

            AgeControlTextField box = field;
            AgeTransform host = AgeWidgets.Transform(field);
            Func<bool> enabled = () => AgeWidgets.Operable(line.QuantityGroup);
            NodeVtable vtable = GraphNodes.EditField(
                () => ModStrings.Get(ModStrings.NegotiationQuantity),
                () => TextFieldEditor.Typing(box) ? null : AgeWidgets.TextOf(host),
                () => Edit(editor, box),
                enabled,
                AgeWidgets.Raw(host)
            );
            // It is a NUMBER the player types, not free text - so the role word says so. What it is NOT
            // is an adjustable: left and right on a FOCUSED cell walk the row's columns, the same as on
            // every other cell of every other table in the mod, and the number is changed by opening the
            // edit and typing it (owner-reported 2026-08-27: "left / right when my focus is on it
            // increments it, even though this should only happen when I'm editing it"). The game's own
            // plus and minus buttons step by one, five or the whole stock depending on a modifier the
            // keyboard cannot reach from here, and typing the figure outright is both shorter and the
            // only way to reach a stock of thousands.
            vtable.ControlType = ControlTypes.NumericEditField;
            AgeWidgets.PointAt(vtable, host);
            return vtable;
        }

        /// <summary>The edit is requested against the NODE the player activated, which the sheet keyed
        /// and this does not know: a hand-made id here names no node, and the editor - which abandons a
        /// request the cursor has walked away from - read that as having walked away immediately and
        /// cancelled every edit on the frame it was asked for. Null is the editor being told to ask the
        /// cursor (<c>TextFieldEditor.CurrentRow</c>), and the cursor is on this cell because this is its
        /// own activation.</summary>
        private static void Edit(TextFieldEditor editor, AgeControlTextField field)
        {
            if (editor != null)
            {
                editor.Request(field, null, null, null);
            }
        }

        /// <summary>
        /// The row itself: what the term is, whether it is in the deal, and the click that puts it there
        /// or takes it out again.
        ///
        /// The name is the one the line DRAWS, because the game has already written the quantity and the
        /// cooldown into it (<c>RefreshNameLabel</c> :87-102: "Titanium (12)", "Peace Treaty (5 turns)").
        /// The computer's opinion of the term - a bare "+" or "-" the game paints beside the name when it
        /// is being asked of an AI (<c>RefreshAIFeedback</c> :333-363) - is read as the value it is, and
        /// only while the game is drawing it; so is the cooldown label beside it, which this build of the
        /// game leaves unwired on every prefab (measured 2026-08-27: <c>TermLine.CooldownLabel</c> is
        /// null and nothing in the assembly assigns it) and which therefore contributes nothing until
        /// some build does draw it.
        ///
        /// <b>Enter is the game's own path, not a replayed click.</b> The line's click handler sends
        /// <c>OnSelectTerm</c> to the window it was bound to (<c>TermLine.OnSelectTermCb</c> :398-403),
        /// and the window answers by posting <c>OrderChangeDiplomaticContractTermsCollection</c> - Add,
        /// or Remove where the contract already holds the term (<c>SelectTerm</c> :1275-1282). So the
        /// message is sent to the same object the line would have sent it to, and one key both adds and
        /// removes, from either side of the table.
        ///
        /// <paramref name="member"/> is null for a row that cannot be out of the deal - every line of the
        /// basket is in it - and otherwise the membership the row reads.
        ///
        /// It is WATCHED rather than said back as the keypress's own answer, which is the one place this
        /// row differs from an ordinary checkbox: the key posts an ORDER and the contract does not change
        /// until the server answers it, so a state word read at press time says what the row still was.
        /// Measured 2026-08-27: Enter on an unselected term answered "not selected" and then, a frame
        /// later, "selected". The watched part alone says it once, when it is true.
        /// </summary>
        private static NodeVtable Primary(
            TermLine line,
            NegotiationModalWindow window,
            Func<bool> member
        )
        {
            TermLine it = line;
            NegotiationModalWindow owner = window;
            AgeTooltip tooltip = line.Tooltip;
            Func<bool> offered = () => AgeWidgets.Offered(it.AgeTransform);
            List<NodeAnnouncement> parts = new List<NodeAnnouncement>
            {
                GraphNodes.LabelPart(() => Words(it.NameLabel)),
                GraphNodes.DisabledPart(offered),
                GraphNodes.ValuePart(() => Words(it.AIPreviewLabel)),
                GraphNodes.ValuePart(() => Words(it.CooldownLabel)),
            };
            if (member != null)
            {
                Func<bool> membership = member;
                parts.Insert(
                    1,
                    new NodeAnnouncement(
                        () => SelectionText.Membership(membership()),
                        live: true,
                        kind: AnnouncementKinds.Selected
                    )
                );
            }

            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = parts,
                Sections = GraphNodes.Sections(() => Consequences(it), tooltip),
                OnActivate = () =>
                {
                    if (offered())
                    {
                        SelectTerm(owner, it);
                    }
                },
            };
            AgeWidgets.PointAt(vtable, it.AgeTransform);
            return vtable;
        }

        /// <summary>Put the term in the deal or take it out, by the route the line's own click takes:
        /// the window is the <c>client</c> every term line is bound to
        /// (<c>NegotiationModalWindow</c> :753-755 hands it <c>base.gameObject</c>), and the click sends
        /// it this message.</summary>
        private static void SelectTerm(NegotiationModalWindow window, TermLine line)
        {
            try
            {
                if (window != null && line.GuiTerm != null)
                {
                    window.gameObject.SendMessage("OnSelectTerm", line.GuiTerm);
                }
            }
            catch (Exception e)
            {
                Log.Warn("negotiation: selecting a term threw: " + e);
            }
        }

        /// <summary>
        /// Whether the deal as it stands holds this term - the same question the window asks before
        /// deciding whether its own click adds or removes (<c>SelectTerm</c> :1277).
        ///
        /// A CONTEXTUAL term is not in the contract's term list at all: picking one puts it in the
        /// window's own <c>SelectedGuiContextualDiplomaticTerm</c> and the basket is built from there
        /// (<c>OnSelectTerm</c> :1331-1340), so that is where its membership is read.
        /// </summary>
        private static bool InContract(NegotiationModalWindow window, TermLine line)
        {
            try
            {
                IGuiDiplomaticTerm gui = line.GuiTerm;
                GuiDiplomaticTerm plain = gui as GuiDiplomaticTerm;
                if (plain != null)
                {
                    DiplomaticContract contract = window == null ? null : window.CurrentContract;
                    return contract != null
                        && contract.Options != null
                        && contract.Options.Count > 0
                        && contract.Options[0].Terms.Contains(plain.Term);
                }

                GuiContextualDiplomaticTerm contextual = gui as GuiContextualDiplomaticTerm;
                return contextual != null
                    && window != null
                    && window.SelectedGuiContextualDiplomaticTerm != null
                    && window.SelectedGuiContextualDiplomaticTerm.Name == contextual.Name;
            }
            catch (Exception)
            {
                return false;
            }
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

        /// <summary>
        /// The occurrence counter <see cref="Distinct"/> keeps for one table's walk. Static and cleared
        /// per walk rather than allocated in it: a shelf is rebuilt every frame.
        /// </summary>
        private static readonly Dictionary<string, int> _seen = new Dictionary<string, int>();

        /// <summary>
        /// The row's identity, made unique within the table it is drawn in.
        ///
        /// <see cref="Key"/> names the TERM, and that is what a row should be keyed by - but a shelf can
        /// draw the same definition twice. Measured on the live table 2026-08-27: a peacetime shelf drew
        /// 14 lines of which two were <c>ResourceDealAdvanced/ReceiverOnly</c> (Empire Dust and
        /// Superspuds, which the definition and the application method cannot tell apart), the second
        /// threw <c>Duplicate control id</c> out of <see cref="GraphSheet"/>, the caller's own catch
        /// swallowed it, and 14 drawn lines reached the player as 8 declared rows.
        ///
        /// So the term key stays the key and an occurrence ORDINAL is appended only to a repeat. The
        /// ordinal is taken among the rows sharing that key rather than from the line's place in the
        /// table, because the shelf is FILTERED: picking a category rebinds every line, so a line's index
        /// moves while its term does not, and keying by index outright would walk the cursor onto a
        /// different term on every refilter and make type-ahead land on a slot instead of a thing. This
        /// way the terms that are unique - which is nearly all of them - keep the identity they always
        /// had, and only the repeats pay.
        /// </summary>
        private static string Distinct(string key)
        {
            int seen;
            if (!_seen.TryGetValue(key, out seen))
            {
                _seen[key] = 1;
                return key;
            }

            _seen[key] = seen + 1;
            return key + "/" + seen;
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
                return AgeWidgets.DrawnLabel(label);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The word the game keeps for one of its own elements, guarded the way every other
        /// borrowed title is (<see cref="AgeText.Title"/>): a key it never finished writing is
        /// silence, not "%TermTypeFilter...".</summary>
        private static string Title(string guiElementName)
        {
            try
            {
                return AgeText.Title(Gui.GetLocalizedTitle(guiElementName));
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
