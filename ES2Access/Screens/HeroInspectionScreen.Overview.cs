using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The overview page, in the columns the game draws it in: the ship box, the hero card,
    /// the skills already owned, and the hero story across the bottom.</summary>
    public sealed partial class HeroInspectionScreen
    {
        // ---- the overview page ----

        /// <summary>
        /// The middle page, in the three columns the game draws it in: the ship on the left, the hero's
        /// card in the middle, the skill wheel on the right, and then the hero's own story across the
        /// bottom.
        ///
        /// Focus lands on the CARD rather than on the leftmost column, because the hero is what every
        /// Inspect button in the game promised.
        /// </summary>
        private void BuildOverview(GraphBuilder builder, HeroInspectionModalWindow window)
        {
            HeroOverviewPanel panel = window.OverviewPanel;
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            BuildShipBox(builder, panel);
            BuildCard(builder, panel);
            BuildSkillsBox(builder, panel);
            BuildStory(builder, panel);
        }

        /// <summary>
        /// The read-only summary of the hero's ship: its name, hull, size and role, the modules fitted
        /// into it drawn as dots over a rendered ship, and the six figures the game puts along the
        /// bottom.
        ///
        /// Every one of them is a readout - there is not one control in the box except the pencil that
        /// opens the ship page. The box as a WHOLE is also a click that opens that page (a second
        /// <c>EditShipDesignButton</c> stretched over all 300x400 of it, measured), and that one is not
        /// declared: it is the mouse's way of doing what the pencil does, the same reason the Academy's
        /// hero pills are left out.
        /// </summary>
        private void BuildShipBox(GraphBuilder builder, HeroOverviewPanel panel)
        {
            ShipDesignOverviewPanel box = panel.ShipDesignOverviewPanel;
            if (box == null || !AgeWidgets.Visible(box.AgeTransform))
            {
                return;
            }

            builder.BeginStop(ShipOverviewStop);
            string title = AgeText.Label(box.TitleLabel);
            bool named = !string.IsNullOrEmpty(title);
            if (named)
            {
                builder.PushContext(title);
            }

            // Three runs, and a region each, because the box draws three things: what the design IS, the
            // dots over the rendered ship, and the figures along the bottom. Every one of them reads one
            // node per row - a grid of characteristics and a grid of figures are peers of one kind whose
            // wrap points belong to the box, not to the data - so the regions are what tells the three
            // apart, and they cover the whole stop so the jump key can leave from anywhere in it.
            builder.SetRegion("hero:ship/characteristics");
            _cells.Clear();
            AddPencil(box.AgeTransform, EditShipHandler, "overview/edit-ship");
            // The name comes from the design and not from the box the game squeezed it into - see
            // ShipDesignRows.OverviewName.
            ShipDesignOverviewPanel it = box;
            AddLine(box.NameLabel, null, "overview/name", () => ShipDesignRows.OverviewName(it));
            AddLine(box.HullLabel, box.HullTooltip, "overview/hull");
            AddLine(box.SizeLabel, box.SizeTooltip, "overview/size");
            AddLine(box.RoleLabel, box.RoleTooltip, "overview/role");
            AddLine(box.Bonus1Label, box.Bonus1Tooltip, "overview/bonus1");
            AddLine(box.Bonus2Label, box.Bonus2Tooltip, "overview/bonus2");
            Cells.EmitLinear(builder, _cells);

            builder.SetRegion("hero:ship/modules");
            _cells.Clear();
            AddFittedModules(box);
            Cells.EmitLinear(builder, _cells);

            builder.SetRegion("hero:ship/figures");
            _cells.Clear();
            AddStats(box);
            Cells.EmitLinear(builder, _cells);
            if (named)
            {
                builder.PopContext();
            }
        }

        /// <summary>
        /// The dots the game draws over the rendered ship, one per slot the hull has.
        ///
        /// They draw no words at all - a filled slot is a coloured sector, an empty one a ring - and
        /// what each stands for is on the wrapper behind its tooltip, which is the module's name or the
        /// game's own word for an empty slot (<c>ShipDesignOverviewSlotItem.Bind</c> sets
        /// <c>Target = GuiSlot</c>).
        ///
        /// Keyed on the ITEM and not on the slot behind it, unlike the ship page's rows: the two pages
        /// draw the same slots, and a shared backing object is what the graph's focus recovery follows
        /// first - so a page change would silently drag the cursor from one page's row onto the other
        /// page's, announcing a node nobody asked for on the way.
        /// </summary>
        private void AddFittedModules(ShipDesignOverviewPanel box)
        {
            try
            {
                AgeTransform container = box.ShipDesignSlotItemsContainer;
                if (container == null || !AgeWidgets.Visible(container))
                {
                    return;
                }

                ShipDesignOverviewSlotItem[] slots =
                    container.GetComponentsInChildren<ShipDesignOverviewSlotItem>(true);
                for (int i = 0; i < slots.Length; i++)
                {
                    ShipDesignOverviewSlotItem slot = slots[i];
                    // Flow control: a slot the design is not drawing contributes no module and is not walked.
                    if (
                        slot == null
                        || slot.GuiSlot == null
                        || !AgeWidgets.Painted(slot.AgeTransform)
                    )
                    {
                        continue;
                    }

                    AgeTooltip tooltip = slot.SlotTooltip ?? AgeWidgets.Raw(slot.AgeTransform);
                    string name = AgeWidgets.TooltipTitle(tooltip);
                    NodeVtable vtable = new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => name),
                        },
                        Sections = GraphNodes.Sections(null, tooltip),
                    };
                    AgeWidgets.PointAt(vtable, slot.AgeTransform);
                    Cells.Add(
                        _cells,
                        slot.AgeTransform,
                        ControlId.For(slot.AgeTransform, Keys + "overview/slot/" + i),
                        vtable
                    );
                }
            }
            catch (Exception e)
            {
                Log.Warn("hero inspection: reading the fitted modules threw: " + e);
            }
        }

        /// <summary>The six figures along the bottom of the box - named by the one map every host of this
        /// panel shares (<see cref="ShipDesignRows.AddSimpleStats"/>).</summary>
        private void AddStats(ShipDesignOverviewPanel box)
        {
            ShipDesignRows.AddSimpleStats(_cells, box, Keys + "overview/");
        }

        /// <summary>
        /// The hero's card, which on this page is also the rename button: the row the card draws the
        /// hero's name in is wired to <c>OnRenameCb</c> (measured - the transform is
        /// <c>HeroTitle</c>), and clicking it raises the game's own name box. So the card is one node
        /// saying the hero's name, doing what a click on that name does, and holding the whole drawn
        /// card in its review buffer.
        ///
        /// The card's own assignment row is a button too - it puts the galaxy view on wherever the hero
        /// is posted - and it is a child node, the only one this card draws that goes anywhere. It is
        /// switched off while the hero has no posting, which is the game's own answer and needs no test
        /// here.
        ///
        /// So the card is a GROUP: its buttons in the first region and, in the second, the pages it
        /// draws no words for at all - affinity, class, politics, one per mastery
        /// (<see cref="HeroCards.Dossiers"/>). A mouse reaches those by hovering inside the card and a
        /// single node could only ever point at one of them.
        /// </summary>
        private void BuildCard(GraphBuilder builder, HeroOverviewPanel panel)
        {
            HeroDetailedCard card = panel.HeroInspectionCard;
            if (card == null || !AgeWidgets.Visible(card.AgeTransform))
            {
                return;
            }

            builder.BeginStop(CardStop);
            _cells.Clear();
            AgeControlButton rename = HeroCards.Wired(card, RenameHandler);
            AgeTransform row = AgeWidgets.Transform(rename);
            HeroDetailedCard it = card;
            AgeTooltip tooltip = AgeWidgets.Raw(row);
            NodeVtable vtable = GraphNodes.Button(
                HeroCards.Name(card),
                () => AgeWidgets.PressPropagating(rename),
                () => AgeWidgets.Operable(row),
                tooltip
            );
            vtable.Sections = GraphNodes.Sections(() => HeroCards.Lines(it), tooltip);
            AgeWidgets.Point(vtable, rename, tooltip, card.AgeTransform);
            string key = Keys + "card";
            ControlId id = ControlId.For(card.AgeTransform, key);
            ScrollIntoView.Anchor(vtable, card.AgeTransform);
            HeroCards.Buttons(_cells, card, key);
            List<TooltipChildren.Dossier> dossiers = HeroCards.Dossiers(card);
            if (_cells.Count == 0 && dossiers.Count == 0)
            {
                builder.AddItem(Nodes.Drawn(id, vtable, card.AgeTransform));
                return;
            }

            builder.BeginGroup(Nodes.Drawn(id, vtable, card.AgeTransform));
            if (builder.IsExpanded(id))
            {
                object outer = TooltipChildren.Actions(builder, key);
                Cells.EmitLinear(builder, _cells);
                TooltipChildren.Emit(builder, key, dossiers, outer);
            }

            builder.EndGroup();
        }

        /// <summary>
        /// The skill wheel as the overview draws it: the points the hero has left to spend, the three
        /// branches as coloured pies, and the pencil that opens the page where they can be spent.
        ///
        /// It is not the wheel again - reading the wheel twice would be reading it wrong. What the
        /// overview draws of a branch is a pie with an arc for progress and no words anywhere, so a
        /// branch here is its name and the game's own sentence about what it contains, and how far along
        /// it is belongs to the page that draws the figures.
        ///
        /// <b>Except for the skills the hero already has</b> (owner ruling 2026-09-02). The pie is not
        /// blank: the overview binds a dot per skill and shows exactly the ones this hero has unlocked
        /// (<c>HeroSkillTreeSkillItemBase.Refresh</c> :30-33 sets
        /// <c>Visible = GetHeroSkillLevel(definition) >= 0</c>), which is a picture of what the hero can
        /// do that a player had to open the skill page to read. So a branch holding at least one of them
        /// is a GROUP whose children are those skills, in the branch's own order, and a branch holding
        /// none stays the leaf it was. The children are READ-ONLY - spending a point is the skill page's,
        /// and the pencil above is the way there.
        /// </summary>
        private void BuildSkillsBox(GraphBuilder builder, HeroOverviewPanel panel)
        {
            SkillTreeBasePanel box = panel.SkillTreeOverviewPanel;
            if (box == null || !AgeWidgets.Visible(box.AgeTransform))
            {
                return;
            }

            builder.BeginStop(SkillsOverviewStop);
            string title = AgeText.Label(box.TitleLabel);
            bool named = !string.IsNullOrEmpty(title);
            if (named)
            {
                builder.PushContext(title);
            }

            _cells.Clear();
            AddPencil(box.AgeTransform, EditSkillsHandler, "overview/edit-skills");
            AddRow(box.SkillPointsGroup, "overview/points");
            Cells.EmitLinear(builder, _cells);

            // The branches are drawn as slices of one circle - the icons sit wherever the middle of each
            // slice happens to fall - so they read as the list of three they are, in the order the game
            // slices the circle up in. That order, and not where the icons landed, is also the order the
            // skill page's own branches and the figures beside them are in.
            try
            {
                HeroSkillTreeItem[] trees =
                    box.AgeTransform.GetComponentsInChildren<HeroSkillTreeItem>(true);
                for (int i = 0; i < trees.Length; i++)
                {
                    HeroSkillTreeItem tree = trees[i];
                    if (tree == null || tree.SkillTreeDefinition == null)
                    {
                        continue;
                    }

                    AgeTransform icon = AgeWidgets.Transform(tree.IconImage);
                    // Flow control: a tree the page is not drawing is not one this hero has, and is not walked.
                    if (!AgeWidgets.Visible(icon))
                    {
                        continue;
                    }

                    AgeTooltip tooltip = AgeWidgets.Raw(icon);
                    string name = TreeName(tree);
                    NodeVtable vtable = new NodeVtable
                    {
                        Announcements = new List<NodeAnnouncement>
                        {
                            GraphNodes.LabelPart(() => name),
                        },
                        Sections = GraphNodes.Sections(null, tooltip),
                    };
                    AgeWidgets.PointAt(vtable, icon);
                    // Keyed on the ICON, not on the branch definition the skill page's groups are keyed
                    // on: the two pages draw the same three branches, and a shared backing object is
                    // what the graph's focus recovery follows first, so a page change would drag the
                    // cursor onto the other page's node and announce it on the way.
                    ControlId id = ControlId.For(icon, Keys + "overview/branch/" + i);
                    List<AgeTransform> owned = OwnedSkillDots(tree);
                    if (owned.Count == 0)
                    {
                        builder.AddItem(Nodes.Drawn(id, vtable, icon));
                        continue;
                    }

                    vtable.ControlType = ControlTypes.Group;
                    builder.BeginGroup(Nodes.Drawn(id, vtable, icon));
                    if (builder.IsExpanded(id))
                    {
                        BuildOwnedSkills(builder, box, owned, i);
                    }

                    builder.EndGroup();
                }
            }
            catch (Exception e)
            {
                Log.Warn("hero inspection: reading the overview's branches threw: " + e);
            }

            if (named)
            {
                builder.PopContext();
            }
        }

        /// <summary>
        /// The dots one branch of the OVERVIEW's wheel is drawing, which are exactly the skills this hero
        /// has unlocked in it - rings inner-first, and within a ring the order the branch's own definition
        /// lists the skills in, which is the order the skill page walks too.
        ///
        /// The test is what is PAINTED rather than the hero's skill level, because that is the same
        /// question: the overview's dot prefab carries a bare <c>HeroSkillTreeSkillItemBase</c> (no
        /// tooltip, no toggle, no level arcs - the skill page's <c>HeroSkillTreeSkillItem</c> is the one
        /// with those) and its <c>Refresh</c> shows the dot if and only if the hero owns the skill. A
        /// locked ring fades its whole table to a quarter rather than hiding it, so a dot in one is still
        /// drawn and still counts.
        /// </summary>
        private static List<AgeTransform> OwnedSkillDots(HeroSkillTreeItem tree)
        {
            List<AgeTransform> dots = new List<AgeTransform>();
            try
            {
                IList<AgeTransform> stages = tree.SkillTreeStagesTable == null
                    ? null
                    : tree.SkillTreeStagesTable.Children;
                for (int i = 0; stages != null && i < stages.Count; i++)
                {
                    HeroSkillTreeStageItem stage =
                        stages[i] == null ? null : stages[i].GetComponent<HeroSkillTreeStageItem>();
                    IList<AgeTransform> skills =
                        stage == null || stage.SkillTreeSkillsTable == null
                            ? null
                            : stage.SkillTreeSkillsTable.Children;
                    for (int k = 0; skills != null && k < skills.Count; k++)
                    {
                        AgeTransform dot = skills[k];
                        HeroSkillTreeSkillItemBase item =
                            dot == null ? null : dot.GetComponent<HeroSkillTreeSkillItemBase>();
                        // Content and flow control, not existence: this dot's paint IS the answer
                        // to "has the hero unlocked this skill", and the count of them is what
                        // decides whether the branch above is a group at all. The gate can only
                        // ever withhold a node it is handed, so it cannot answer either question.
                        if (
                            item != null
                            && item.HeroSkillDefinition != null
                            && AgeWidgets.Painted(dot)
                        )
                        {
                            dots.Add(dot);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("hero inspection: reading a branch's own skills threw: " + e);
            }

            return dots;
        }

        /// <summary>The skills the hero has in one branch, as the overview can tell them: what each is
        /// called, how far along it is, and the dossier the skill page hangs on its own dot for it.
        /// </summary>
        private void BuildOwnedSkills(
            GraphBuilder builder,
            SkillTreeBasePanel box,
            List<AgeTransform> dots,
            int branch
        )
        {
            for (int i = 0; i < dots.Count; i++)
            {
                AgeTransform dot = dots[i];
                HeroSkillTreeSkillItemBase item = dot.GetComponent<HeroSkillTreeSkillItemBase>();
                HeroSkillDefinition definition = item.HeroSkillDefinition;
                AgeTooltip carrier = OwnedSkillCarrier(dot, item);
                SkillTreeBasePanel owner = box;
                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(() => OwnedSkillName(carrier, definition)),
                        GraphNodes.ValuePart(() => OwnedSkillLevel(owner, definition)),
                    },
                    Sections = GraphNodes.Sections(null, carrier),
                };
                AgeWidgets.PointAt(vtable, dot, carrier);
                builder.AddItem(Nodes.Drawn(
                    ControlId.For(dot, Keys + "overview/branch/" + branch + "/skill/" + i),
                    vtable,
                    dot
                ));
            }
        }

        /// <summary>
        /// The dossier for a skill the overview is drawing a dot for.
        ///
        /// The dot itself carries no tooltip at all (measured 2026-09-02: every one of them answers
        /// <c>AgeTooltip</c> null), so there is nothing on this page to aim at - the words exist only as
        /// data, in the same <c>GuiHeroSkill</c> wrapper the skill page's dot builds and hands the
        /// tooltip window (<c>HeroSkillTreeSkillItem.Refresh</c> :159-161 writes Content, Class and
        /// Target off one). A carrier bound with the same three therefore assembles the same panel, and
        /// is parked over the dot so the panel opens where the picture is.
        ///
        /// The wrapper needs the SKILL PAGE's panel (<c>GuiHeroSkill</c> asks it for the levels), and
        /// that panel is bound to this same hero the whole time the window is open, side page shown or
        /// not (measured). Where it is not bound there is no carrier and the child is its name and its
        /// level with nothing to review - the skill page is where the dossier lives.
        /// </summary>
        private static AgeTooltip OwnedSkillCarrier(
            AgeTransform dot,
            HeroSkillTreeSkillItemBase item
        )
        {
            try
            {
                HeroInspectionModalWindow window = Window();
                SkillTreeEditionPanel page =
                    window == null ? null : window.SkillTreeEditionPanel;
                if (page == null || page.GuiHero == null)
                {
                    return null;
                }

                HeroSkillDefinition definition = item.HeroSkillDefinition;
                AgeTooltip carrier;
                bool rebind = ScratchTooltips.Rebind(
                    "hero-overview-skill/" + dot.GetInstanceID(),
                    definition.Name.GetHashCode(),
                    out carrier
                );
                if (rebind && carrier != null)
                {
                    GuiHeroSkill wrapper =
                        new GuiHeroSkill(definition, item.SkillTreeName, page);
                    carrier.Class = wrapper.TooltipClass;
                    carrier.Content = wrapper.Name;
                    carrier.Target = wrapper;
                }

                ScratchTooltips.PlaceOver(carrier, dot);
                return carrier;
            }
            catch (Exception e)
            {
                Log.Warn("hero inspection: binding a hero skill's dossier threw: " + e);
                return null;
            }
        }

        /// <summary>What the game calls a skill, off the same wrapper the skill page names its dot from
        /// (<see cref="SkillName"/>) and with the same fallback - which for a BRANCH skill answers the
        /// same words either way (measured on all three of this hero's branches).</summary>
        private static string OwnedSkillName(AgeTooltip carrier, HeroSkillDefinition definition)
        {
            try
            {
                string named = AgeWidgets.TooltipTitle(carrier);
                return string.IsNullOrEmpty(named)
                    ? AgeText.Clean(Gui.Localize(Gui.GetTitle(definition.Name)))
                    : named;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>How far along a skill the hero HAS is, in the same words the skill page's dot uses
        /// for it. Only the owned level: a pending pick belongs to the page that can make one.</summary>
        private static string OwnedSkillLevel(
            SkillTreeBasePanel box,
            HeroSkillDefinition definition
        )
        {
            try
            {
                int levels = definition.SkillLevels.Length;
                int owned = box.GuiHero.GetHeroSkillLevel(definition) + 1;
                return ModStrings.Format(ModStrings.HeroSkillLevel, owned, levels);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The paragraph the page draws across its bottom edge: who the hero is, in the game's
        /// own words, permanently on screen inside a scroll view. Always-drawn text, so it is spoken in
        /// full as the line it is rather than left to a tooltip rule.</summary>
        private void BuildStory(GraphBuilder builder, HeroOverviewPanel panel)
        {
            HeroDetailedCard card = panel.HeroInspectionCard;
            AgeTransform story = card == null ? null : AgeWidgets.Transform(card.DescriptionLabel);
            if (
                story == null
                || !AgeWidgets.Visible(story)
                || string.IsNullOrEmpty(HeroCards.Description(card))
            )
            {
                return;
            }

            builder.BeginStop(StoryStop);
            _cells.Clear();
            Cells.AddReadout(_cells, story, Keys + "story");
            Cells.EmitLinear(builder, _cells);
        }
    }
}
