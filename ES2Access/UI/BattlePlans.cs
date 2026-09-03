using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// THE BATTLE PLAN CAROUSEL, as a thing four surfaces ask questions of.
    ///
    /// One decision - which of an empire's battle plans this fight is fought under - drawn four
    /// different ways: the setup popup's one card with arrows either side of it, the report popups'
    /// read-only card, the advanced setup window's whole hand, and the advanced report's pair of
    /// cards. Every one of them needs the same answers (how many plans are there, which is chosen,
    /// what is this one called, what does it DO, what does its card explain about itself) and the
    /// game answers none of them directly: the count and the current index are read off the play
    /// group, the effects off the card's own printed lines, and the range diagrams are told apart by
    /// their position alone.
    ///
    /// It lived inside the setup popup's own reader while three other screens reached into that
    /// screen to use it. It is a reading of the GAME's plan model rather than of any one popup, so it
    /// sits beside the other battle readers instead.
    ///
    /// STEPPING is the one thing here that changes the game: <see cref="Turn"/> presses the window's
    /// own arrows rather than setting the plan, because the arrows are what the game wires the change
    /// to.
    /// </summary>
    internal static class BattlePlans
    {
        internal const string PlanKey = "battle-setup/plan";

        /// <summary>The one card the setup popup draws, whichever plan it is currently bound to.
        /// </summary>
        internal static BattlePlayCard PlanCard(BattleSetupNotificationWindow window)
        {
            return window == null ? null : Card(window.SelectedPlayCardContainer);
        }

        /// <summary>The game's own title for the plan carousel ("Battle Plan") - the closed row's name
        /// and the chooser's.</summary>
        internal static string PlanTitle()
        {
            return AgeText.Clean(BattleRows.SetupPlanTitleKey);
        }

        /// <summary>A row standing on the one card the window draws.
        ///
        /// Synthetic only where the window has not built its card yet: what stands behind the row is
        /// then the group's own plan list, a game fact the window's arrows step through, and the
        /// drawn test above it (the play group the window is showing) has already asked whether the
        /// carousel is on the screen at all. With a card - which is every frame after the first bind -
        /// the card is the widget that vouches for the row and the gate asks it.</summary>
        internal static NodeDeclaration Drawn(ControlId id, NodeVtable vtable, AgeTransform carrier)
        {
            return carrier == null
                ? (NodeDeclaration)Nodes.Synthetic(id, vtable)
                : Nodes.Drawn(id, vtable, carrier);
        }

        /// <summary>
        /// Turn the card to plan <paramref name="index"/> the way clicking its arrow does, if it is not
        /// showing it already.
        ///
        /// The step is the game's own arrow, pressed the way a mouse presses it, taking whichever way
        /// round the set is shorter - the window's handlers are what wrap the index and re-bind the
        /// card, and nothing here reproduces them. It is idempotent: a card already on the plan
        /// presses nothing, and an arrow that answers with nothing stops the run rather than being
        /// pressed again as a louder way of not moving.
        ///
        /// TURNING THE CARD CHOOSES THE PLAN - the game has no confirm step of its own - so the only
        /// caller that turns it while the player browses is the chooser
        /// (<see cref="BattlePlanScreen"/>), which knows how to put it back.
        /// </summary>
        internal static void Turn(BattleSetupNotificationWindow window, int index)
        {
            try
            {
                if (window == null || !Steppable(window))
                {
                    return;
                }

                int count = PlayCount(window);
                int current = CurrentPlay(window);
                if (count < 2 || current < 0 || index < 0 || index >= count || current == index)
                {
                    return;
                }

                int forwards = (index - current + count) % count;
                AgeControlButton arrow = forwards * 2 <= count
                    ? window.NextPlayButton
                    : window.PreviousPlayButton;
                for (int step = 0; step < count && CurrentPlay(window) != index; step++)
                {
                    int before = CurrentPlay(window);
                    AgeWidgets.Press(arrow);
                    if (CurrentPlay(window) == before)
                    {
                        // The arrow answered with nothing - the window is not stepping, and pressing
                        // it again would only be a louder way of not moving.
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle: turning to a battle plan threw: " + e);
            }
        }

        /// <summary>Whether the plan can still be changed - which the game answers by switching both
        /// arrows off once this side has committed to the fight.</summary>
        internal static bool Steppable(BattleSetupNotificationWindow window)
        {
            try
            {
                return window != null
                    && (AgeWidgets.Operable(AgeWidgets.Transform(window.PreviousPlayButton))
                        || AgeWidgets.Operable(AgeWidgets.Transform(window.NextPlayButton)));
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The side of the battle whose plan this popup is choosing.</summary>
        private static EncounterGroup PlayerGroup(BattleSetupNotificationWindow window)
        {
            return SetupGroup(window == null ? null : window.LeftBattleGroupSetupPanel);
        }

        /// <summary>The group a setup panel is bound to, asked defensively: a panel the window has not
        /// finished binding answers by throwing.</summary>
        private static EncounterGroup SetupGroup(BattleGroupSetupPanel panel)
        {
            try
            {
                return panel == null ? null : panel.EncounterGroup;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>How many plans this fleet may fight under - the list the window's own arrows step
        /// through. -1 where the group will not say.</summary>
        internal static int PlayCount(BattleSetupNotificationWindow window)
        {
            try
            {
                EncounterGroup group = PlayerGroup(window);
                return group == null || group.AvailablePlays == null
                    ? -1
                    : group.AvailablePlays.Count;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        /// <summary>Which of them is in force, or -1 where the group will not say.</summary>
        internal static int CurrentPlay(BattleSetupNotificationWindow window)
        {
            try
            {
                EncounterGroup group = PlayerGroup(window);
                EncounterGroupSetup setup = group == null ? null : group.Setup;
                EncounterPlayDefinition chosen = setup == null ? null : setup.PlayDefinition;
                if (chosen == null || group.AvailablePlays == null)
                {
                    return -1;
                }

                for (int i = 0; i < group.AvailablePlays.Count; i++)
                {
                    if (group.AvailablePlays[i] == chosen)
                    {
                        return i;
                    }
                }

                return -1;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        /// <summary>What a plan is called: the game's own title for it, which is exactly what the card
        /// prints when the window turns to it - so an unread row and the drawn card say one word.
        /// The card's own label answers for the plan in force where the list will not.</summary>
        internal static string PlayName(
            BattleSetupNotificationWindow window,
            int index,
            BattlePlayCard card
        )
        {
            try
            {
                EncounterGroup group = PlayerGroup(window);
                if (group != null && group.AvailablePlays != null
                    && index < group.AvailablePlays.Count)
                {
                    GuiBattlePlayCard wrapper = Gui.GuiWrapperProviderService.GetGuiBattlePlayCard(
                        group.AvailablePlays[index]
                    );
                    string title = wrapper == null ? null : AgeText.Clean(wrapper.Title);
                    if (!string.IsNullOrEmpty(title))
                    {
                        return title;
                    }
                }
            }
            catch (Exception)
            {
                // Fall through to what the card drew.
            }

            return card == null || CurrentPlay(window) != index
                ? null
                : AgeText.Label(card.PlayTitle);
        }

        /// <summary>
        /// The same words off a card that is showing its OWN plan, with nothing to turn first.
        ///
        /// The guard above is a fact about the CAROUSEL - one card the window pages between the
        /// plans - and not about a card. The advanced window lays the same plans out as a HAND, three
        /// cards drawn at once with each permanently on the plan it stands for, so there is no
        /// mis-describing to guard against and this is the whole of the reading.
        /// </summary>
        internal static string PlanEffects(BattlePlayCard card)
        {
            try
            {
                if (card == null)
                {
                    return null;
                }

                GuiEffectMapper mapper = card.FamilyEffectsMapper;
                AgeTransform table = mapper == null ? null : mapper.EffectLinesTable;
                // Content read: the card draws EITHER its effect lines or the game's own word for a
                // plan that has none, and the table is the switch the game throws between them.
                if (table == null || !AgeWidgets.Visible(table))
                {
                    return AgeWidgets.DrawnLabel(card.NoEffectsLabel);
                }

                MessageBuilder said = new MessageBuilder();
                List<AgeTransform> lines = table.Children;
                for (int i = 0; lines != null && i < lines.Count; i++)
                {
                    said.ListItem(AgeWidgets.PaintedText(lines[i]));
                }

                return said.Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// What the card prints under the plan's name: the effects the plan applies, or the game's own
        /// word for a plan that applies none.
        ///
        /// Always-drawn text, so it is part of what the row says rather than something to review - but
        /// only for the row whose plan the card is actually showing. The other rows have no card of
        /// their own and would otherwise all read the effects of whichever plan is in force.
        /// </summary>
        internal static string PlanEffects(
            BattleSetupNotificationWindow window,
            int index,
            BattlePlayCard card
        )
        {
            return CurrentPlay(window) != index ? null : PlanEffects(card);
        }

        /// <summary>
        /// The hover surfaces the card carries besides its own: the badge naming the family of tactic
        /// the plan belongs to, and one range diagram per flotilla.
        ///
        /// The three diagrams say the same three words as each other ("Short Range") and are told apart
        /// by their position on the card alone, so each entry is named with the flotilla it belongs to
        /// in the game's own words for both halves - the same joining the tactics deck's rows already
        /// use. That name CARRIES the diagram's own sentence, so the entry says it once
        /// (<see cref="TooltipChildren.AddPlain"/>'s named-entry rule): "Flotilla 1: Short Range", not
        /// "Flotilla 1: Short Range, Short Range" (owner-reported 2026-08-29). The family badge is
        /// named with the game's title for that family ("Aggressive"), the word it draws the picture
        /// for and never writes down, and keeps its own different sentence.
        ///
        /// <paramref name="turn"/> is the chooser's: inside it every entry belongs to one plan of
        /// several, and the card has to be showing that plan before the entry says anything. It runs
        /// where each entry's NAME resolves, which is the one moment between the cursor arriving and
        /// the landing being spoken. Null everywhere else - the closed row's card is always the plan
        /// in force and there is nothing to turn.
        /// </summary>
        internal static List<TooltipChildren.Dossier> PlanDossiers(
            BattlePlayCard card,
            Action turn
        )
        {
            List<TooltipChildren.Dossier> dossiers = new List<TooltipChildren.Dossier>(4);
            if (card == null)
            {
                return dossiers;
            }

            try
            {
                BattlePlayCard it = card;
                Action turned = turn;
                AgeTransform badge = card.FamilyIcon == null ? null : card.FamilyIcon.AgeTransform;
                TooltipChildren.AddPlain(
                    dossiers,
                    AgeWidgets.Raw(badge),
                    badge,
                    () =>
                    {
                        Turned(turned);
                        return FamilyName(it);
                    }
                );

                AgeTransform ranges = card.FlotillaRangeIndicators;
                List<AgeTransform> indicators = ranges == null ? null : ranges.Children;
                for (int i = 0; indicators != null && i < indicators.Count; i++)
                {
                    AgeTransform indicator = indicators[i];
                    AgeTooltip tip = AgeWidgets.Raw(indicator);
                    int flotilla = i;
                    // Named by the sentence the game wrote for it, beside the flotilla it belongs to
                    // - a label fallback, see RangeName.
                    TooltipChildren.AddPlain(
                        dossiers,
                        tip,
                        indicator,
                        () => RangeName(turned, flotilla, tip)
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle: reading a plan card's badges threw: " + e);
            }

            return dossiers;
        }

        /// <summary>What a range diagram's entry is called, with the card first put on the plan the
        /// entry belongs to.
        ///
        /// A LABEL FALLBACK, not a second reading of the tooltip: the diagram draws no words at all,
        /// so the only thing that could NAME its entry is the sentence the game wrote for it - the
        /// same rung the ordinary naming ladder would have reached - joined to the flotilla the card
        /// says nowhere. The words are not silenced by being read here: they are this entry's own
        /// section, which the named-entry rule then keeps from saying them a second time
        /// (<see cref="TooltipChildren.AddPlain"/>).</summary>
        private static string RangeName(Action turn, int flotilla, AgeTooltip tip)
        {
            Turned(turn);
            return TooltipFeatures.FlotillaRange(flotilla, CardActions.FirstLine(tip));
        }

        /// <summary>Put the card on the plan whose entry is about to speak, where the caller is the
        /// chooser and there is one. Failures are the turn's own to log.</summary>
        private static void Turned(Action turn)
        {
            if (turn != null)
            {
                turn();
            }
        }

        /// <summary>The game's own title for the family of tactic the card's badge draws the picture
        /// for - the same element the badge takes its sentence from
        /// (<c>BattlePlayCard.RefreshMainGroup</c>). Null where the game will not say, and the
        /// ordinary naming ladder answers instead.</summary>
        private static string FamilyName(BattlePlayCard card)
        {
            try
            {
                GuiBattlePlaySlot slot = card.GuiBattlePlaySlot;
                GuiBattlePlayCard wrapper = slot == null ? null : slot.GuiCard;
                EncounterPlayDefinition definition =
                    wrapper == null ? null : wrapper.EncounterPlayDefinition;
                if (definition == null || Amplitude.StaticString.IsNullOrEmpty(definition.FamilyName))
                {
                    return null;
                }

                // The game never draws a family's title - the badge is a picture - and it ships no
                // title for every family it ships a description for (measured 2026-09-03:
                // "%PlayFamilyPostBalancedTitle" comes back as the key). An unresolved key is not
                // a name; null lets the naming ladder answer with the badge's own sentence.
                string title = AgeText.Clean(Gui.GetLocalizedTitle("Play" + definition.FamilyName));
                return string.IsNullOrEmpty(title) || Gui.IsLocalizationKey(title) ? null : title;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// What a card's pictures say, read off the plan MODEL rather than off hover tooltips - for
        /// the military screen's tiny deck cards, which draw the family badge and the three range
        /// rows exactly as the full card does but hang no tooltip on any of them (measured
        /// 2026-09-03: every indicator's and the badge's <c>AgeTooltip</c> is null), so
        /// <see cref="PlanDossiers"/> finds nothing to make a node of.
        ///
        /// The family's title first, then one line per flotilla in the words the full card's own
        /// range tooltip uses (<c>BattlePlayCardRangeIndicator.Refresh</c> :75 -
        /// <c>%AdvancedPlayFlotillaOptimalRangeTitle</c> over the range's localized title), joined to
        /// the flotilla the same way the tactics deck and the setup card join it. Reviewable content:
        /// it is all on the screen, in pictures.
        /// </summary>
        internal static IList<string> Markings(BattlePlayCard card)
        {
            List<string> lines = new List<string>();
            try
            {
                string family = FamilyName(card);
                if (!string.IsNullOrEmpty(family))
                {
                    lines.Add(family);
                }

                GuiBattlePlaySlot slot = card.GuiBattlePlaySlot;
                AgeTransform ranges = card.FlotillaRangeIndicators;
                List<AgeTransform> indicators = ranges == null ? null : ranges.Children;
                for (int i = 0; slot != null && indicators != null && i < indicators.Count; i++)
                {
                    // Content: a range row the card is not drawing says nothing.
                    if (!AgeWidgets.Visible(indicators[i]))
                    {
                        continue;
                    }

                    int index = slot.GetFlotillaOptimalRangeIndex(i);
                    if (index < 0 || index >= (int)GuiShip.ShipEfficiencyRange.Max)
                    {
                        continue;
                    }

                    string range = AgeText.Clean(
                        Gui.Localize(
                            "%AdvancedPlayFlotillaOptimalRangeTitle",
                            Gui.GetLocalizedTitle(((GuiShip.ShipEfficiencyRange)index).ToString())
                        )
                    );
                    if (!string.IsNullOrEmpty(range))
                    {
                        lines.Add(TooltipFeatures.FlotillaRange(i, range));
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("battle: reading a plan card's markings off its model threw: " + e);
            }

            return lines;
        }

        private static BattlePlayCard Card(AgeTransform container)
        {
            try
            {
                return container == null
                    ? null
                    : container.GetComponentInChildren<BattlePlayCard>(true);
            }
            catch (Exception)
            {
                return null;
            }
        }

    }
}
