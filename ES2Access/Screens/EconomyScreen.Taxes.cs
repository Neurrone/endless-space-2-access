using System;
using System.Collections.Generic;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The panels along the bottom of the marketplace: the tax box and where it is levied,
    /// the exchange log, and the two banners the game tickers events and adverts across.</summary>
    public sealed partial class EconomyScreen
    {
        // ---- the tax box ----

        /// <summary>
        /// The marketplace's tax box, in whichever of its two forms the game is drawing: the owner's,
        /// with the rate to set and what setting it would cost, or everybody else's, with where the
        /// marketplace is, who owns it and the rate they have set
        /// (<c>MarketplaceTaxesPanel.Refresh</c> :112-188).
        ///
        /// The game draws its facts as a row of values with no word over any of them, so each is a line
        /// of its own under the mod's caption for what it states, with the game's own sentence about it
        /// in the buffer. The location is a button as well as a fact: it takes the map to the system the
        /// marketplace was built in, and is drawn switched off with the reason on it until somebody
        /// builds one.
        ///
        /// The owner's form is FIXTURE-BLOCKED on the save this was built against (its marketplace is
        /// unbuilt and unowned). Its rate box is the game's editor under the same typing rule as the
        /// trading strip's quantity, its two steppers move one percentage point per press with no coarse
        /// variant (<c>OnIncreaseTaxRateButtonClickCb</c> :240-252), and its Set button is named by the
        /// game's own drawn label, which states what the change costs.
        /// </summary>
        private void BuildTaxes(GraphBuilder builder, MarketplaceTaxesPanel panel)
        {
            builder.BeginStop(TaxesStop);
            builder.PushContext(PanelName(panel, ModStrings.EconomyTaxesPanel));
            _cells.Clear();
            AddPanelCaption(_cells, panel, "economy:taxes/title");
            // A branch chooser on a wired prefab field, not an existence gate: the panel keeps both
            // forms and shows one (<c>Refresh</c> :155-171), so this is which of the two is being read.
            if (panel.OwnedGroup != null && AgeWidgets.Visible(panel.OwnedGroup))
            {
                AddLocation(panel.OwnedLocationButton, panel.OwnedLocationLabel);
                AddTaxRate(panel);
            }
            else
            {
                AddLocation(panel.NotOwnedLocationButton, panel.NotOwnedLocationLabel);
                Cells.AddStat(
                    _cells,
                    panel.NotOwnedOwnerNameLabel,
                    ModStrings.Get(ModStrings.EconomyOwner),
                    "economy:taxes/owner"
                );
                Cells.AddStat(
                    _cells,
                    panel.NotOwnedTaxRateLabel,
                    ModStrings.Get(ModStrings.EconomyTaxRate),
                    "economy:taxes/rate"
                );
            }

            Cells.EmitLinear(builder, _cells);
            builder.PopContext();
        }

        /// <summary>Where the marketplace is, and the game's own button that takes the map there.
        /// </summary>
        private void AddLocation(AgeTransform group, AgePrimitiveLabel label)
        {
            // No visibility test: which of the two forms is drawn was decided by the caller, and the
            // gate asks the rest.
            if (group == null)
            {
                return;
            }

            AgeControlButton button = AgeWidgets.Button(group);
            AgeTooltip tooltip = AgeWidgets.Raw(group);
            AgePrimitiveLabel it = label;
            NodeVtable vtable;
            if (button == null)
            {
                vtable = GraphNodes.Readout(
                    () => ModStrings.Get(ModStrings.EconomyLocation),
                    () => AgeText.Label(it),
                    null,
                    tooltip
                );
            }
            else
            {
                AgeControlButton press = button;
                vtable = GraphNodes.Button(
                    () => ModStrings.Get(ModStrings.EconomyLocation),
                    () => AgeWidgets.Press(press),
                    () => AgeWidgets.Offered(group),
                    tooltip
                );
                vtable.Announcements.Add(GraphNodes.ValuePart(() => AgeText.Label(it)));
                AgeWidgets.Point(vtable, button, tooltip, group);
            }

            Cells.Add(_cells, group, ControlId.For(group, "economy:taxes/location"), vtable);
        }

        /// <summary>The owner's rate editor, its two steppers and the button that pays for the change -
        /// each a line of its own, because the game hangs no shared caption over them to read them
        /// under.</summary>
        private void AddTaxRate(MarketplaceTaxesPanel panel)
        {
            AgeControlTextField field = panel.TaxRateTextField;
            AgeTransform at = AgeWidgets.Transform(field);
            // A branch chooser again: the box lives in the owner form, which the panel keeps wired and
            // hides while somebody else owns the marketplace.
            if (at != null && AgeWidgets.Visible(at))
            {
                Cell cell = SettingRows.TextFieldCell(
                    field,
                    () => ModStrings.Get(ModStrings.EconomyTaxRate),
                    AgeWidgets.Raw(at.Parent ?? at),
                    null,
                    null,
                    ControlId.For(field, "economy:taxes/rate"),
                    _editor
                );
                if (cell != null)
                {
                    // Same ruling as the trading strip's quantity: the arrows are the player walking,
                    // not the player setting, and the rate is typed into the edit.
                    cell.Vtable.StateText = () => SettingRows.FieldText(field);
                    cell.Vtable.ControlType = ControlTypes.NumericEditField;
                    _cells.Add(cell);
                }
            }

            Func<string> rate = () => SettingRows.FieldText(panel.TaxRateTextField);
            AddStepper(
                panel.DecreaseTaxRateButton,
                ModStrings.EconomyDecrement,
                "economy:taxes/minus",
                rate
            );
            AddStepper(
                panel.IncreaseTaxRateButton,
                ModStrings.EconomyIncrement,
                "economy:taxes/plus",
                rate
            );
            Cells.AddControl(_cells, panel.ApplyTaxRateButton, "economy:taxes/apply");
        }

        // ---- the exchange log ----

        /// <summary>What has been traded, newest at the bottom - which is where the game scrolls the list
        /// to. The game groups the list by turn and draws a header above each group (a line with no
        /// transactions behind it IS the header -
        /// <c>MarketplaceExchangeInformationsPanel.Refresh</c> :16-62,
        /// <c>TradableTransactionLine.Bind</c>), so each turn is a region of its own named by the header
        /// the game drew and each transaction is a line under it. A transaction of somebody else's is
        /// already anonymised by the game before it is written, so what is drawn is what may be read.
        /// </summary>
        private void BuildLog(GraphBuilder builder, MarketplaceExchangeInformationsPanel panel)
        {
            builder.BeginStop(LogStop);
            builder.PushContext(PanelName(panel, ModStrings.EconomyLogPanel));
            try
            {
                // The panel's own heading, and the sentence it draws over the whole list while the empire
                // may not see one, in a region of their own: a stop is regioned all the way through or
                // not at all, and the jump out of the last turn has to land somewhere.
                builder.SetRegion("economy:log/head");
                _cells.Clear();
                AddPanelCaption(_cells, panel, "economy:log/title");
                Cells.AddReadout(
                    _cells,
                    panel.NotOwnerLabel == null ? null : panel.NotOwnerLabel.AgeTransform,
                    "economy:log/not-owner"
                );
                Cells.EmitLinear(builder, _cells);

                AgeTransform table = panel.TradableTransactionsTable;
                IList<AgeTransform> lines = table == null ? null : table.Children;
                bool turn = false;
                _cells.Clear();
                for (int i = 0; lines != null && i < lines.Count; i++)
                {
                    AgeTransform line = lines[i];
                    if (line == null || !SettingRows.Drawn(line))
                    {
                        continue;
                    }

                    string header = TurnHeader(line);
                    if (string.IsNullOrEmpty(header))
                    {
                        Cells.AddReadout(_cells, line, "economy:log/line/" + i);
                        continue;
                    }

                    Cells.EmitLinear(builder, _cells);
                    _cells.Clear();
                    if (turn)
                    {
                        builder.PopContext();
                    }

                    builder.SetRegion("economy:log/turn/" + i);
                    builder.PushContext(header);
                    turn = true;
                }

                Cells.EmitLinear(builder, _cells);
                if (turn)
                {
                    builder.PopContext();
                }
            }
            finally
            {
                builder.PopContext();
            }
        }

        /// <summary>The words a line the game drew names a turn with, or nothing where the line is a
        /// transaction. The header is the wrapper the game made with no transactions in it.</summary>
        private static string TurnHeader(AgeTransform line)
        {
            try
            {
                TradableTransactionLine drawn = line.GetComponent<TradableTransactionLine>();
                GuiTradableTransaction of = drawn == null ? null : drawn.GuiTradableTransaction;
                bool header =
                    of != null && of.TradableTransactions != null && of.TradableTransactions.Count == 0;
                return header ? AgeWidgets.TextOf(line) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The market events, as the list the game's own service holds rather than as the one item the
        /// conveyor happens to have on screen.
        ///
        /// This is the page's one deliberate departure from declaring what is drawn (owner-approved). The
        /// banner animates pooled items across the panel one at a time and sizes each to its text
        /// (<c>MarketplaceEventsBanner.QueueNext</c> :102-125), so a walk of what is drawn would find a
        /// single moving fragment. The rows here are composed from the same call and the same template
        /// the drawn item composes from (<c>MarketplaceEventItem.Bind</c>).
        /// </summary>
        private void BuildEvents(GraphBuilder builder, MarketplaceEventsBanner banner)
        {
            List<KeyValuePair<StaticString, StaticString>> feedback = Feedback();
            if (feedback == null || feedback.Count == 0)
            {
                return;
            }

            builder.BeginStop(EventsStop);
            builder.PushContext(ModStrings.Get(ModStrings.EconomyEventsPanel));
            for (int i = 0; i < feedback.Count; i++)
            {
                string said = EventText(feedback[i].Key, feedback[i].Value);
                if (string.IsNullOrEmpty(said))
                {
                    continue;
                }

                string text = said;
                builder.StartRow();
                // Synthetic: composed from the empire's own event record, which the panel draws
                // nowhere - the enumeration above is what says these are real.
                builder.AddItem(Nodes.Synthetic(
                    ControlId.Structural("economy:event/" + i),
                    new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => text),
                        },
                    }
                ));
                builder.EndRow();
            }

            builder.PopContext();
        }

        private static List<KeyValuePair<StaticString, StaticString>> Feedback()
        {
            try
            {
                ITradingManagementService service =
                    Amplitude.Unity.Framework.Services.GetService<ITradingManagementService>();
                if (service == null)
                {
                    return null;
                }

                List<KeyValuePair<StaticString, StaticString>> found =
                    new List<KeyValuePair<StaticString, StaticString>>();
                service.GetEventsFeedback(found);
                return found;
            }
            catch (Exception e)
            {
                Log.Warn("economy: reading the market events threw: " + e);
                return null;
            }
        }

        /// <summary>One event's sentence, composed the way the drawn item composes it: the affected
        /// thing's own title inside the template the event names.</summary>
        private static string EventText(StaticString element, StaticString effect)
        {
            try
            {
                Amplitude.Unity.Gui.GuiElement guiElement = Gui.GetGuiElement(element);
                string name =
                    guiElement == null ? null : AgeText.Clean(Gui.Localize(guiElement.Title));
                return string.IsNullOrEmpty(name)
                    ? null
                    : AgeText.Clean(Gui.Localize(effect.ToString(), name));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The advertisements, read the same way and for the same reason as the events - and untruncated,
        /// which the drawn item is not (<c>AdItem.Bind</c> cuts the empire's name to fit the strip).
        ///
        /// The Create-advertisement button the banner draws is not declared: the window it opens has no
        /// controls at all in this build and is not registered with the GUI service (measured), so the
        /// game's own handler logs an error and opens nothing.
        /// </summary>
        private void BuildAds(GraphBuilder builder, MarketplaceAdBanner banner)
        {
            if (banner == null)
            {
                return;
            }

            ITradingManagementService service = Trading();
            int count = 0;
            try
            {
                count = service == null ? 0 : service.ActiveAdvertisementsCount;
            }
            catch (Exception)
            {
                return;
            }

            if (count == 0)
            {
                return;
            }

            builder.BeginStop(AdsStop);
            builder.PushContext(ModStrings.Get(ModStrings.EconomyAdsPanel));
            for (int i = 0; i < count; i++)
            {
                string said = AdText(service, i);
                if (string.IsNullOrEmpty(said))
                {
                    continue;
                }

                string text = said;
                builder.StartRow();
                // Synthetic: composed from the trading company's own offer list, drawn nowhere.
                builder.AddItem(Nodes.Synthetic(
                    ControlId.Structural("economy:ad/" + i),
                    new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => text),
                        },
                    }
                ));
                builder.EndRow();
            }

            builder.PopContext();
        }

        /// <summary>One advertisement: who wants something - or the game's own word for an empire that
        /// asked not to be named - and what they want, in the game's own template for it.</summary>
        private static string AdText(ITradingManagementService service, int index)
        {
            try
            {
                MarketplaceAdvertisement ad = service.GetAdvertisement(index);
                if (ad == null)
                {
                    return null;
                }

                string who = ad.IsAnonymous
                    ? AgeText.Clean(Gui.Localize(AdItem.AnonymousEmpireLoc.ToString()))
                    : Gui.GuiWrapperProviderService
                        .GetGuiEmpire(ad.EmpireIndex)
                        .GetLeaderName(Gui.PlayerEmpire);
                Amplitude.Unity.Gui.ExtendedGuiElement element =
                    Gui.GetExtendedGuiElement(ad.ItemName);
                string what =
                    element == null ? null : AgeText.Clean(Gui.Localize(element.Title));
                if (string.IsNullOrEmpty(what))
                {
                    return null;
                }

                return new MessageBuilder()
                    .ListItem(AgeText.Clean(who))
                    .ListItem(
                        AgeText.Clean(
                            Gui.Localize(AdItem.AdvertisementDescLoc.ToString(), what)
                        )
                    )
                    .Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static ITradingManagementService Trading()
        {
            try
            {
                return Amplitude.Unity.Framework.Services.GetService<ITradingManagementService>();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
