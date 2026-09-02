using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Input;

namespace ES2Access.Screens
{
    /// <summary>The marketplace tab: the buy and sell tables, the band that carries one transaction,
    /// and the stepper and buttons that place it.</summary>
    public sealed partial class EconomyScreen
    {
        // ---- the marketplace tab ----

        /// <summary>The marketplace's panels, in the order they are drawn. Read from where they are
        /// rather than from a list here: this half of the page is behind a technology the fixture cannot
        /// research, so its layout was never measured and any fixed order would be a guess.</summary>
        private void BuildMarket(GraphBuilder builder, MarketplacePanel panel)
        {
            _boards.Clear();
            Board(_boards, panel.BuyableItemsPanel);
            Board(_boards, panel.SalableItemsPanel);
            Board(_boards, panel.TaxesPanel);
            Board(_boards, panel.ExchangeInformationsPanel);
            Board(_boards, panel.EventsBanner);
            Board(_boards, panel.AdsBanner);
            _boards.Sort(PanelsInReadingOrder);

            for (int i = 0; i < _boards.Count; i++)
            {
                GuiPanel board = _boards[i];
                try
                {
                    MarketplaceBuyableItemsPanel buy = board as MarketplaceBuyableItemsPanel;
                    if (buy != null)
                    {
                        BuildBuy(builder, buy);
                        continue;
                    }

                    MarketplaceSalableItemsPanel sell = board as MarketplaceSalableItemsPanel;
                    if (sell != null)
                    {
                        BuildSell(builder, sell);
                        continue;
                    }

                    MarketplaceTaxesPanel taxes = board as MarketplaceTaxesPanel;
                    if (taxes != null)
                    {
                        BuildTaxes(builder, taxes);
                        continue;
                    }

                    MarketplaceExchangeInformationsPanel log =
                        board as MarketplaceExchangeInformationsPanel;
                    if (log != null)
                    {
                        BuildLog(builder, log);
                        continue;
                    }

                    MarketplaceEventsBanner events = board as MarketplaceEventsBanner;
                    if (events != null)
                    {
                        BuildEvents(builder, events);
                        continue;
                    }

                    BuildAds(builder, board as MarketplaceAdBanner);
                }
                catch (Exception e)
                {
                    Log.Warn("economy: reading " + board.GetType().Name + " threw: " + e);
                }
            }
        }

        /// <summary>
        /// The buying half, as ONE stop with the three bands the game draws down it as regions: the
        /// heading, the section filters, the list of what is on offer, and the strip the trade is set up
        /// in.
        ///
        /// The list stays a real TABLE (owner ruling 2026-08-30, reversing a same-day ruling that had
        /// made it one node per row): the game binds this <c>GuiTable</c> to the column set its SECTION
        /// declares - three for the resource sections, ten for ships and heroes
        /// (<c>Public/Gui/GuiElements[Marketplace].xml</c>) - so the columns are a fact of the game's own
        /// data and a walk across them is a walk across what the game says about each offer. Nothing here
        /// counts columns: the shared table reading pairs each cell to the heading the game drew over it,
        /// so a section with ten of them inherits ten.
        ///
        /// The price graph is declared AFTER this stop rather than where the game draws it, between the
        /// filters and the table (owner ruling 2026-08-30): it is a page of its own to read, and putting
        /// it in the middle of the buying walk makes the table the far side of it.
        /// </summary>
        private void BuildBuy(GraphBuilder builder, MarketplaceBuyableItemsPanel panel)
        {
            builder.BeginStop(BuyStop);
            builder.PushContext(AgeText.Clean(Gui.Localize(BuyButtonTitleKey)));
            AddCaption(builder, panel, "economy:buy/");
            BuildSections(builder, panel, "economy:buy/");

            GuiTable table = panel.BuyableItemsGuiTable;
            // Flow control: the shared table reading walks every line of it, and a region would be
            // opened around nothing.
            if (table != null && AgeWidgets.Visible(table.AgeTransform))
            {
                // The band's name and its role are pushed HERE rather than left to the sheet, because
                // the sort headers are part of it and the sheet opens after them; the sheet is then
                // given no title of its own, so the region is announced once, on the heading row the
                // jump lands on. It still sets the stop's landing on the first data row.
                builder.SetRegion("economy:buy/available");
                builder.PushContext(
                    ModStrings.Get(ModStrings.EconomyAvailable),
                    GraphSheet.TableRoleText == null ? null : GraphSheet.TableRoleText()
                );
                _buyTable.Headers(builder, table);
                _buyTable.Rows(builder, table, null);
                builder.PopContext();
            }

            builder.PopContext();

            BuildBand(builder, panel, BuyBandStop, "economy:buy/", BuyButtonTitleKey, BandName(panel));
            BuildHistory(builder, panel);
        }

        /// <summary>The selling half, the same three bands in one stop. Its items are not a table at all
        /// - they are plain toggles the panel pools
        /// (<c>MarketplaceSalableItemsPanel.RefreshTradableItemsTable</c> :117-130) - and they read one
        /// per row, as the buying list beside them now does: the dense strip the game wraps them into is
        /// a rendering accident, and walking it sideways bought nothing.</summary>
        private void BuildSell(GraphBuilder builder, MarketplaceSalableItemsPanel panel)
        {
            builder.BeginStop(SellStop);
            builder.PushContext(AgeText.Clean(Gui.Localize(SellButtonTitleKey)));
            AddCaption(builder, panel, "economy:sell/");
            BuildSections(builder, panel, "economy:sell/");

            AgeTransform table = panel.SalableItemsTable;
            // Flow control: a region and a context would be opened around nothing, and every item under
            // the table would be read in the band above.
            if (table != null && AgeWidgets.Visible(table))
            {
                builder.SetRegion("economy:sell/available");
                builder.PushContext(ModStrings.Get(ModStrings.EconomyAvailable));
                _cells.Clear();
                IList<AgeTransform> items = table.Children;
                for (int i = 0; items != null && i < items.Count; i++)
                {
                    AddSalableItem(_cells, panel, items[i], i);
                }

                Cells.EmitLinear(builder, _cells);
                builder.PopContext();
                if (_cells.Count > 0)
                {
                    builder.LandStopOn(_cells[0].Id);
                }
            }

            builder.PopContext();

            BuildBand(builder, panel, SellBandStop, "economy:sell/", SellButtonTitleKey, BandName(panel));
        }

        /// <summary>The panel's own drawn heading, in a region of its own so the stop is regioned all
        /// the way through and the jump out of the filters reaches it.</summary>
        private void AddCaption(GraphBuilder builder, GuiPanel panel, string keyPrefix)
        {
            _cells.Clear();
            AddPanelCaption(_cells, panel, keyPrefix + "title");
            if (_cells.Count == 0)
            {
                return;
            }

            builder.SetRegion(keyPrefix + "heading");
            Cells.EmitLinear(builder, _cells);
        }

        /// <summary>The section radios - strategic resources, luxuries, ships, heroes - filtered and
        /// ordered by the game (<c>BuildGuiTradableSections</c>). A section the empire may not trade in
        /// is drawn switched off with the game's own reason on it, and stays declared while it refuses:
        /// which markets exist, and why not this one, is what the player came here to find out.
        ///
        /// Each label opens with the category's ICON, which reads as the word the engine's symbol
        /// registry gives it - and for the strategics section that word is "Titanium", the name of one
        /// of the resources in it. The leading icon is dropped for the same reason it is dropped
        /// everywhere else: it is a picture standing beside the words, not a word of them.</summary>
        private void BuildSections(
            GraphBuilder builder,
            MarketplaceTradableItemsPanel panel,
            string keyPrefix
        )
        {
            AgeTransform table = panel.MarketTabRadiosTable;
            IList<AgeTransform> children = table == null ? null : table.Children;
            // Flow control: same - a region would be opened around nothing and every radio read in the
            // band above it.
            if (children == null || !AgeWidgets.Visible(table))
            {
                return;
            }

            builder.SetRegion(keyPrefix + "filters");
            builder.PushContext(ModStrings.Get(ModStrings.EconomyFilters));
            _cells.Clear();
            for (int i = 0; i < children.Count; i++)
            {
                AgeTransform widget = children[i];
                MarketTabRadio radio =
                    widget == null ? null : widget.GetComponent<MarketTabRadio>();
                if (radio == null || radio.Toggle == null)
                {
                    continue;
                }

                MarketTabRadio it = radio;
                AgeTransform at = AgeWidgets.Transform(radio.Toggle);
                AgeTooltip tooltip = radio.Tooltip ?? AgeWidgets.Raw(widget);
                // A section the empire may not trade in is blocked the same way the marketplace tab is
                // (<c>MarketTabRadio.Bind</c> :19-31), so the shared availability test rather than the
                // enable flag alone.
                Func<bool> offered = () => AgeWidgets.Offered(at);
                NodeVtable vtable = GraphNodes.Radio(
                    () => AgeText.LabelWithoutLeadingIcon(it.Label),
                    () => it.Toggle.State,
                    () => AgeWidgets.Toggle(it.Toggle),
                    offered,
                    null,
                    tooltip
                );
                GraphNodes.AddRefusal(vtable, tooltip, offered);

                AgeWidgets.Point(vtable, radio.Toggle, tooltip, widget);
                Cells.Add(
                    _cells,
                    widget,
                    ControlId.For(widget, keyPrefix + "filter/" + i),
                    vtable
                );
            }

            Cells.Emit(builder, _cells);
            builder.PopContext();
        }

        /// <summary>
        /// One thing the empire could sell. The item draws a picture and a stock figure and nothing else,
        /// so it is named by the wrapper the game hangs on its tooltip - and for a luxury the empire has
        /// never located, by the one sentence the game writes there instead
        /// (<c>SalableItem.SetTooltip</c>).
        ///
        /// Enter is the item's own click: it makes this the selection and adds to the quantity, with Ctrl
        /// and Shift the game's own multipliers.
        /// </summary>
        private void AddSalableItem(
            List<Cell> cells,
            MarketplaceSalableItemsPanel panel,
            AgeTransform widget,
            int index
        )
        {
            SalableItem item = widget == null ? null : widget.GetComponent<SalableItem>();
            if (item == null || !SettingRows.Drawn(widget) || item.SelectionToggle == null)
            {
                return;
            }

            SalableItem it = item;
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            string label = SalableName(widget);
            Func<bool> offered = () => AgeWidgets.Operable(widget);
            NodeVtable vtable = GraphNodes.Radio(
                () => label,
                () => it.SelectionToggle.State,
                () => AgeWidgets.Toggle(it.SelectionToggle),
                offered,
                null,
                tooltip
            );
            vtable.Announcements.Add(GraphNodes.ValuePart(() => AgeText.Label(it.StockLabel)));
            MarketplaceSalableItemsPanel owner = panel;
            MarketGestures(
                vtable,
                () => AgeWidgets.Toggle(it.SelectionToggle),
                offered,
                () => QuantityText(owner)
            );
            AgeWidgets.Point(vtable, it.SelectionToggle, tooltip, widget);
            Cells.Add(cells, widget, ControlId.For(widget, "economy:salable/" + index), vtable);
        }

        /// <summary>
        /// The gestures a market row has beyond its plain click: the game's own multiplier clicks.
        ///
        /// <c>MarketplacePanel.GetQuantityToAddFromClick</c> (:124-136) reads the modifier the player is
        /// physically holding, so the chords replay the SAME click rather than reimplementing the
        /// arithmetic - Ctrl five at a time, Shift the whole stock - and the game's clamping and its
        /// selection rules apply unchanged. The quantity it arrived at is spoken back, because the press
        /// changes a number in the strip along the bottom of the panel and nothing else would say so.
        /// </summary>
        private void MarketGestures(
            NodeVtable vtable,
            Action click,
            Func<bool> offered,
            Func<string> quantity
        )
        {
            Action press = click;
            Func<bool> can = offered;
            Func<string> landed = quantity;
            Action guarded = () =>
            {
                if (can())
                {
                    press();
                }
            };
            vtable.OnSelectToggle = guarded;
            vtable.OnSelectRange = guarded;
            vtable.StateText = () => can() ? landed() : null;
            MarketChordHints(vtable, can);
        }

        /// <summary>The two multiplier chords, said in the buffer of everything that has them - the
        /// sellable tiles, the buy table.s rows and the strip.s own steppers. The game.s tooltip on some
        /// of those already says what a modified CLICK does; what the keyboard.s chords do is the mod.s
        /// to say, and it is said in one place so no surface offering the gesture can forget it.
        /// </summary>
        private static void MarketChordHints(NodeVtable vtable, Func<bool> offered)
        {
            NodeHints.Add(vtable, ModStrings.HintMarketFive, UiActions.SelectToggle, 0, offered);
            NodeHints.Add(vtable, ModStrings.HintMarketAll, UiActions.SelectRange, 0, offered);
        }

        /// <summary>
        /// What the strip is currently set up to trade, which is what its stop is called: nothing else
        /// in the strip says WHICH resource its price and its total belong to, and a stop the player
        /// tabs into has to name itself.
        ///
        /// A strip with nothing picked keeps a name of the mod's own rather than none: the stop is
        /// there, it refuses, and a nameless one would announce a bare role word.
        /// </summary>
        private static string BandName(MarketplaceTradableItemsPanel panel)
        {
            bool buying = panel is MarketplaceBuyableItemsPanel;
            string what = SelectedName(panel);
            return string.IsNullOrEmpty(what)
                ? ModStrings.Get(
                    buying
                        ? ModStrings.EconomyBuyTransaction
                        : ModStrings.EconomySellTransaction
                )
                : ModStrings.Format(
                    buying ? ModStrings.EconomyBuyingWhat : ModStrings.EconomySellingWhat,
                    what
                );
        }

        /// <summary>
        /// The thing the panel has picked, in the words its own list calls it by.
        ///
        /// The buying half is the game's own answer: it writes the picked buyable's title across the
        /// left of the strip and empties that label again when nothing is picked
        /// (<c>RefreshBottomGroup</c> :185-207). The selling half draws no such label, so the picked
        /// TILE is asked - and asked through <see cref="SalableName"/>, so a luxury the empire has never
        /// located is named here by the same sentence the tile is named by rather than by the name the
        /// player is not allowed to have.
        /// </summary>
        private static string SelectedName(MarketplaceTradableItemsPanel panel)
        {
            try
            {
                MarketplaceBuyableItemsPanel buy = panel as MarketplaceBuyableItemsPanel;
                if (buy != null)
                {
                    return AgeText.Label(buy.SelectedBuyableNameLabel);
                }

                MarketplaceSalableItemsPanel sell = panel as MarketplaceSalableItemsPanel;
                AgeTransform table = sell == null ? null : sell.SalableItemsTable;
                IList<AgeTransform> items = table == null ? null : table.Children;
                for (int i = 0; items != null && i < items.Count; i++)
                {
                    SalableItem item =
                        items[i] == null ? null : items[i].GetComponent<SalableItem>();
                    if (
                        item != null
                        && item.SelectionToggle != null
                        && item.SelectionToggle.State
                        && SettingRows.Drawn(items[i])
                    )
                    {
                        return SalableName(items[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("economy: reading what is being traded threw: " + e);
            }

            return null;
        }

        /// <summary>What a sellable tile is called: the game's own wrapper title where the empire has
        /// located the resource, and the one sentence the game writes in its place where it has not
        /// (<c>SalableItem.SetTooltip</c>). Decided in ONE place, so the tile and the name of the strip
        /// that trades it cannot disagree about a resource the player may not be told the name of.
        /// </summary>
        private static string SalableName(AgeTransform widget)
        {
            AgeTooltip tooltip = AgeWidgets.Raw(widget);
            return Identified(tooltip)
                ? AgeWidgets.TooltipTitle(tooltip)
                : CardActions.FirstLine(tooltip);
        }

        private void MarketRow(GuiTableLine line, NodeVtable vtable)
        {
            GuiTableLine row = line;
            MarketGestures(
                vtable,
                () => AgeWidgets.Toggle(row.SelectionToggle),
                () => AgeWidgets.Enabled(row.AgeTransform),
                () => QuantityText(BuyPanel())
            );
        }

        /// <summary>
        /// How much the panel is currently set to trade, off the box the game writes it into - which is
        /// the number a sighted player reads, and which the game writes SYNCHRONOUSLY from its own
        /// quantity setter (<c>MarketplaceTradableItemsPanel.SelectedTradableQuantity</c> :80-92 calls
        /// <c>ReplaceInputText</c>), so it is already the new number when a press asks for it.
        ///
        /// Asked of the panel whose control was pressed. Buying and selling are two panels with a box
        /// each: this used to read the BUYING one whatever was pressed, so every sell-side gesture
        /// announced the buy side's quantity - "Quantity 0" after a click that had just set it to one
        /// (measured 2026-08-30).
        /// </summary>
        private static string QuantityText(MarketplaceTradableItemsPanel panel)
        {
            try
            {
                string text = SettingRows.FieldText(panel == null ? null : panel.QuantityTextField);
                return string.IsNullOrEmpty(text)
                    ? null
                    : ModStrings.Format(ModStrings.EconomyQuantity, text);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The price-and-quantity strip along the bottom of a trading panel, as the one row the game
        /// writes it as: what a unit costs, how many are being traded, and what the trade comes to.
        ///
        /// Read as that equation rather than as the widgets it is made of. The game draws a bare "-"
        /// between the price and the quantity and a bare "=" before the total, and those two glyphs are
        /// punctuation, not controls - a walk of the shape of the tree used to declare each of them as a
        /// node whose name was a hyphen. The two steppers are the game's own buttons, named by the mod
        /// because the game writes no word on them (its sentence about what a click does, and what Ctrl
        /// and Shift do to it, stays in their buffers where every other explanation is), and the trade
        /// button is named by the game's own Buy or Sell title with its running total as the value.
        ///
        /// The quantity box is the game's own editor, handed the keyboard a frame after the request. Its
        /// arrows are NOT wired to the steppers: left and right on a focused box navigate, and the value
        /// is changed by opening the edit and typing it (owner ruling 2026-08-27, first made on the
        /// negotiation basket - arrows that move a value the player only meant to walk past are a value
        /// changed by accident).
        /// </summary>
        private void BuildBand(
            GraphBuilder builder,
            MarketplaceTradableItemsPanel panel,
            object stop,
            string keyPrefix,
            string titleKey,
            string name
        )
        {
            _cells.Clear();

            // What the buying half is currently set to trade, which the game writes across the left of
            // the strip once something is picked (<c>RefreshBottomGroup</c> :185-207 leaves it blank
            // otherwise, and a blank one contributes no line). Not in the approved shape for this row -
            // the fixture it was measured on had nothing selected, so nothing was drawn there - and kept
            // because it is drawn: it is the only place the strip says WHAT is being bought.
            MarketplaceBuyableItemsPanel buy = panel as MarketplaceBuyableItemsPanel;
            if (buy != null)
            {
                Cells.AddReadout(_cells, buy.SelectedBuyableNameGroup, keyPrefix + "selected");
            }

            // Where a bought ship would appear, which the game draws in this strip only while what is
            // selected is a ship. It is the other member that is not part of the equation, and the game
            // draws it between the name and the price.
            // Flow control on a wired prefab field: the panel hides the group for anything that is not a
            // ship (<c>RefreshBottomGroup</c> :185-207), so this is which of its two shapes is drawn.
            if (
                buy != null
                && buy.ShipSpawnPointButtonGroup != null
                && AgeWidgets.Visible(buy.ShipSpawnPointButtonGroup)
            )
            {
                Cells.AddControl(_cells, buy.ShipSpawnPointButtonGroup, keyPrefix + "spawn-point");
            }

            AddPrice(panel, keyPrefix);
            AddStepper(
                panel.QuantityMinusButton,
                ModStrings.EconomyDecrement,
                keyPrefix + "minus",
                () => QuantityText(panel)
            );
            AddQuantity(panel, keyPrefix);
            AddStepper(
                panel.QuantityPlusButton,
                ModStrings.EconomyIncrement,
                keyPrefix + "plus",
                () => QuantityText(panel)
            );
            AddTradeButtons(panel, keyPrefix, titleKey);

            // Flow control: a stop would be opened around nothing on a frame where the game has taken
            // the whole strip away.
            if (_cells.Count == 0)
            {
                return;
            }

            builder.BeginStop(stop);
            builder.PushContext(name);
            Cells.EmitRow(builder, _cells);
            builder.PopContext();
        }

        /// <summary>What one unit is going for, under the game's own word for it - the strip draws the
        /// number with nothing beside it but the "-" that separates it from the quantity.</summary>
        private void AddPrice(MarketplaceTradableItemsPanel panel, string keyPrefix)
        {
            AgePrimitiveLabel label = panel.UnitPriceLabel;
            AgeTransform at = label == null ? null : label.AgeTransform;
            // No visibility test: the gate asks whether the game is drawing this label, and its whole
            // ancestry with it.
            if (at == null)
            {
                return;
            }

            AgeTransform group = at.Parent ?? at;
            AgeTooltip tooltip = AgeWidgets.Raw(group) ?? AgeWidgets.Raw(at);
            AgePrimitiveLabel it = label;
            NodeVtable vtable = GraphNodes.Readout(
                () => AgeText.Clean(Gui.Localize(PriceTitleKey)),
                () => AgeText.Label(it),
                null,
                tooltip
            );
            Cells.Add(_cells, group, ControlId.For(group, keyPrefix + "unit-price"), vtable);
        }

        /// <summary>One of the two arrows beside a number the game lets the player step. The game writes
        /// no word on them at all, so the mod names them; what a press does is the game's own sentence,
        /// and the number it lands on is spoken back, because the press changes a figure somewhere else
        /// in the strip and nothing else would say so.</summary>
        private void AddStepper(
            AgeControlButton button,
            string nameKey,
            string key,
            Func<string> state
        )
        {
            AgeTransform at = AgeWidgets.Transform(button);
            // A branch chooser on a wired prefab field: the tax box keeps a pair of these inside the
            // form it is not drawing, and this is which pair is being read.
            if (at == null || !AgeWidgets.Visible(at))
            {
                return;
            }

            AgeControlButton it = button;
            AgeTooltip tooltip = AgeWidgets.Raw(at);
            Func<string> landed = state;
            // Whether the press that is being reported on actually happened. Asking the button again
            // afterwards is not the same question: the press the game accepts LAST is the one that takes
            // the value to its limit and switches the button off, so a refusal test run after the fact
            // swallowed exactly the number the player most wanted (measured: incrementing to a stock of
            // five said "unavailable" and never said five).
            bool[] acted = new bool[1];
            NodeVtable vtable = GraphNodes.Button(
                () => ModStrings.Get(nameKey),
                () =>
                {
                    acted[0] = true;
                    AgeWidgets.Press(it);
                },
                () => AgeWidgets.Offered(at),
                tooltip
            );
            // Nothing at all on a press the game refuses: the player heard "unavailable" on the way in.
            vtable.StateText = () =>
            {
                if (!acted[0])
                {
                    return null;
                }

                acted[0] = false;
                return landed();
            };
            // The same press again for the two multiplier chords. A node with only an activation does
            // NOTHING for them - they are their own vtable entries - so the button the game reads the
            // held modifier inside (<c>OnQuantityPlusCb</c> :368-379) has to be wired three times to be
            // reachable three ways. The arithmetic is never the mod's: what Ctrl and Shift turn one
            // press into is decided inside the game's own handler.
            Func<bool> offered = () => AgeWidgets.Offered(at);
            Action chord = () =>
            {
                if (offered())
                {
                    acted[0] = true;
                    AgeWidgets.Press(it);
                }
            };
            vtable.OnSelectToggle = chord;
            vtable.OnSelectRange = chord;
            MarketChordHints(vtable, offered);
            AgeWidgets.Point(vtable, button, tooltip, at);
            Cells.Add(_cells, at, ControlId.For(at, key), vtable);
        }

        /// <summary>The box the quantity is typed into, as the game's own editor.</summary>
        private void AddQuantity(MarketplaceTradableItemsPanel panel, string keyPrefix)
        {
            AgeControlTextField field = panel.QuantityTextField;
            AgeTransform at = AgeWidgets.Transform(field);
            // No visibility test here either - same reason as the price beside it.
            if (at == null)
            {
                return;
            }

            Cell cell = SettingRows.TextFieldCell(
                field,
                null,
                null,
                null,
                null,
                ControlId.For(field, keyPrefix + "quantity"),
                _editor
            );
            if (cell == null)
            {
                return;
            }

            cell.Vtable.StateText = () => QuantityText(panel);
            // A NUMBER is typed here rather than free text, so the role word says so.
            cell.Vtable.ControlType = ControlTypes.NumericEditField;
            _cells.Add(cell);
        }

        /// <summary>The button that makes the trade - or, once the empire can buy with influence as well
        /// as with dust, the two of them the game swaps in for it
        /// (<c>MarketplaceBuyableItemsPanel.RefreshTradeButtons</c> :291-303). Those two carry no title of
        /// their own, only a running total beside a currency, so the game's own Buy title names them and
        /// the total is the value - currency and all, exactly as the game wrote it into the label.
        /// </summary>
        private void AddTradeButtons(
            MarketplaceTradableItemsPanel panel,
            string keyPrefix,
            string titleKey
        )
        {
            AddTradeButton(panel.TradeButton, panel.TotalPriceLabel, titleKey, keyPrefix + "trade");
            MarketplaceBuyableItemsPanel buy = panel as MarketplaceBuyableItemsPanel;
            if (buy != null)
            {
                AddTradeButton(
                    buy.DustTradeButton,
                    buy.TotalDustPriceLabel,
                    titleKey,
                    keyPrefix + "trade-dust"
                );
                AddTradeButton(
                    buy.EmpirePointTradeButton,
                    buy.TotalEmpirePointPriceLabel,
                    titleKey,
                    keyPrefix + "trade-influence"
                );
            }
        }

        private void AddTradeButton(
            AgeControlButton button,
            AgePrimitiveLabel total,
            string titleKey,
            string key
        )
        {
            AgeTransform at = AgeWidgets.Transform(button);
            // A branch chooser, not an existence gate: the panel keeps all three trade buttons wired and
            // shows either the one or the pair (<c>RefreshTradeButtons</c> :291-303).
            if (at == null || !AgeWidgets.Visible(at))
            {
                return;
            }

            AgeControlButton it = button;
            AgePrimitiveLabel amount = total;
            AgeTooltip tooltip = AgeWidgets.Raw(at);
            NodeVtable vtable = GraphNodes.Button(
                () => AgeText.Clean(Gui.Localize(titleKey)),
                () => AgeWidgets.Press(it),
                () => AgeWidgets.Offered(at),
                tooltip
            );
            vtable.Announcements.Add(GraphNodes.ValuePart(() => AgeText.Label(amount)));
            AgeWidgets.Point(vtable, button, tooltip, at);
            Cells.Add(_cells, at, ControlId.For(at, key), vtable);
        }
    }
}
