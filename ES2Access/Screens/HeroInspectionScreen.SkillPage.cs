using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The skill-tree page: the trees, their stages and skills, what a skill still needs,
    /// and the statistics box beside them.</summary>
    public sealed partial class HeroInspectionScreen
    {
        // ---- the skill-tree page ----

        /// <summary>The right-hand page, in the three columns the game draws it in: what the hero has to
        /// spend and what the pending picks would do down the left edge, the wheel in the middle, and the
        /// figures about the wheel down the right edge.</summary>
        private void BuildSkillPage(GraphBuilder builder, HeroInspectionModalWindow window)
        {
            SkillTreeEditionPanel panel = window.SkillTreeEditionPanel;
            if (panel == null || !AgeWidgets.Visible(panel.AgeTransform))
            {
                return;
            }

            BuildTreeInfo(builder, panel);
            // The wheel wears the page's own drawn heading, which is why the page declares no heading
            // stop of its own - see BuildHeading.
            BuildTrees(builder, panel, AgeWidgets.TextOf(Heading(window)));
            BuildTreeStats(builder, panel);
        }

        /// <summary>
        /// The left-hand column: the points left to spend, where the hero is posted, and the block of
        /// bonuses the pending picks would produce.
        ///
        /// The bonuses are the whole feedback a preview has - spending a point rewrites them and posts
        /// no order - so they are read as the game writes them: one line per effect, under the caption
        /// that says which situation the effects apply in
        /// (<c>SkillTreeEditionPanel.RefreshEffects</c> :398-404 binds one item per
        /// <c>DescriptorEffectSet</c> of the PREVIEWED hero).
        /// </summary>
        private void BuildTreeInfo(GraphBuilder builder, SkillTreeEditionPanel panel)
        {
            builder.BeginStop(TreeInfoStop);
            _cells.Clear();
            try
            {
                AddRow(panel.SkillPointsGroup, "tree/points");
                AddRow(Assignment(panel), "tree/assignment");
                AddCaption(panel.SkillEffectsTable, 3, "tree/effects-caption");
                IList<AgeTransform> items = panel.SkillEffectsTable == null
                    ? null
                    : panel.SkillEffectsTable.Children;
                for (int i = 0; items != null && i < items.Count; i++)
                {
                    AddEffectSet(items[i], i);
                }
            }
            catch (Exception e)
            {
                Log.Warn("hero inspection: reading the skill page's left column threw: " + e);
            }

            // Declaration order, not the drawn rows: the column is one stack, and the bonuses are the
            // one band on this window whose contents the game rebuilds from a pooled table while the
            // player watches. A pooled item the game has not re-laid-out yet overlaps its neighbour, and
            // banding by drawn position then reads two blocks of effects a line at a time, alternating
            // (measured, after a Reset).
            for (int i = 0; i < _cells.Count; i++)
            {
                builder.AddItem(Nodes.Drawn(_cells[i].Id, _cells[i].Vtable, _cells[i].Widget));
            }
        }

        /// <summary>One block of the bonuses list: the situation it applies in, then one line per effect
        /// the game wrote into it.</summary>
        private void AddEffectSet(AgeTransform widget, int index)
        {
            PanelFeatureEffectsSetsItem item =
                widget == null ? null : widget.GetComponent<PanelFeatureEffectsSetsItem>();
            if (item == null || !AgeWidgets.Painted(widget))
            {
                return;
            }

            Cells.AddReadout(
                _cells,
                AgeWidgets.Transform(item.TitleLabel),
                Keys + "tree/effect/" + index
            );
            AgeTransform table =
                item.EffectMapper == null ? null : item.EffectMapper.EffectLinesTable;
            IList<AgeTransform> lines = table == null ? null : table.Children;
            for (int i = 0; lines != null && i < lines.Count; i++)
            {
                // Flow control: a line the effect table is not drawing is not one of this skill's effects.
                if (AgeWidgets.Painted(lines[i]))
                {
                    Cells.AddReadout(
                        _cells,
                        lines[i],
                        Keys + "tree/effect/" + index + "/line/" + i
                    );
                }
            }
        }

        /// <summary>
        /// The wheel: three branches, four rings each, one or two skills to a ring.
        ///
        /// A wheel is not a list and this one is not really a wheel either - it is three pies
        /// (<c>SkillTreeBasePanel.BindSkillTree</c> :182-193 gives each branch a sector of the circle),
        /// each of four rings out from the middle (<c>HeroSkillTreeItem.BindSkillTreeStage</c> :148-157),
        /// with the ring's skills spread along its arc. So it is declared as the tree it is: branch,
        /// ring, skill, in the order the game laid them out - which is the order of the definitions
        /// themselves, inner ring first.
        ///
        /// A ring is LOCKED until the hero has spent enough points to reach it
        /// (<c>SkillTreeEditionPanel.CountPointsAndEnableStages</c> :238-300 disables the ring item, and
        /// the pending picks count towards it), and a locked ring's skills refuse for free: the game
        /// leaves each dot's own Enable flag ON and switches the RING off, so the answer comes from the
        /// ancestor walk rather than from the dot (measured - all 21 dots read Enable true while three
        /// rings of each branch are disabled).
        ///
        /// <paramref name="label"/> is the page's own drawn heading, which is what the wheel is called
        /// on this page and therefore what names the stop - so the heading is said where the thing it
        /// names is, and the page needs no heading stop of its own.
        /// </summary>
        private void BuildTrees(GraphBuilder builder, SkillTreeEditionPanel panel, string label)
        {
            AgeTransform table = panel.SkillTreesTable;
            IList<AgeTransform> trees = table == null ? null : table.Children;
            if (trees == null)
            {
                return;
            }

            builder.BeginStop(TreeStop);
            bool named = !string.IsNullOrEmpty(label);
            if (named)
            {
                builder.PushContext(label);
            }

            try
            {
                for (int i = 0; i < trees.Count; i++)
                {
                    HeroSkillTreeItem tree =
                        trees[i] == null ? null : trees[i].GetComponent<HeroSkillTreeItem>();
                    // Flow control: a tree the page is not drawing is not one this hero has, and is not walked.
                    if (
                        tree == null
                        || tree.SkillTreeDefinition == null
                        || !AgeWidgets.Painted(tree.AgeTransform)
                    )
                    {
                        continue;
                    }

                    ControlId id = ControlId.For(
                        tree.SkillTreeDefinition,
                        Keys + "tree/branch/" + i
                    );
                    // Synthetic: a branch stands for the skill-tree DEFINITION the hero was built from,
                    // and the enumeration above is what says the panel is drawing it.
                    builder.BeginGroup(Nodes.Synthetic(id, BranchVtable(panel, tree, i)));
                    BuildStages(builder, panel, tree, i);
                    builder.EndGroup();
                }
            }
            catch (Exception e)
            {
                Log.Warn("hero inspection: reading the wheel threw: " + e);
            }

            if (named)
            {
                builder.PopContext();
            }
        }

        /// <summary>One branch of the wheel: what the game calls it, how much of it is done, and the
        /// sentence the game hangs on the icon at its centre.</summary>
        private static NodeVtable BranchVtable(
            SkillTreeEditionPanel panel,
            HeroSkillTreeItem tree,
            int index
        )
        {
            AgeTransform icon = AgeWidgets.Transform(tree.IconImage);
            AgeTooltip tooltip = AgeWidgets.Raw(icon);
            string name = TreeName(tree);
            NodeVtable vtable = GraphNodes.Group(() => name, null, tooltip);
            SkillTreeEditionPanel owner = panel;
            int at = index;
            vtable.Announcements.Add(GraphNodes.ValuePart(() => Completion(owner, at), false));
            AgeWidgets.PointAt(vtable, icon);
            return vtable;
        }

        /// <summary>How far along a branch is, in the words the page itself draws for it down the
        /// right-hand column ("0/12"): the completion lines are bound one per branch in branch order
        /// (<c>SkillTreeEditionPanel.RefreshSkillTrees</c> :302-307).</summary>
        private static string Completion(SkillTreeEditionPanel panel, int index)
        {
            try
            {
                IList<AgeTransform> lines = panel.TreeCompletionLinesTable == null
                    ? null
                    : panel.TreeCompletionLinesTable.Children;
                if (lines == null || index < 0 || index >= lines.Count)
                {
                    return null;
                }

                SkillTreeCompletionLine line = lines[index].GetComponent<SkillTreeCompletionLine>();
                return line == null ? null : AgeText.Label(line.PointsLabel);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The rings of one branch, inner first - the order the game binds them in and the
        /// order they unlock in.</summary>
        private void BuildStages(
            GraphBuilder builder,
            SkillTreeEditionPanel panel,
            HeroSkillTreeItem tree,
            int branch
        )
        {
            IList<AgeTransform> stages = tree.SkillTreeStagesTable == null
                ? null
                : tree.SkillTreeStagesTable.Children;
            for (int i = 0; stages != null && i < stages.Count; i++)
            {
                HeroSkillTreeStageItem stage =
                    stages[i] == null ? null : stages[i].GetComponent<HeroSkillTreeStageItem>();
                // Flow control: a stage the tree is not drawing is not one of its own, and is not walked.
                if (
                    stage == null
                    || stage.SkillTreeStage == null
                    || !AgeWidgets.Painted(stage.AgeTransform)
                )
                {
                    continue;
                }

                HeroSkillTreeStageItem it = stage;
                int ring = i;
                int rings = stages.Count;
                NodeVtable vtable = GraphNodes.Group(
                    () => StageName(ring, rings),
                    () => AgeWidgets.Operable(it.AgeTransform)
                );
                vtable.Announcements.Add(GraphNodes.ValuePart(() => StageRequirement(it), false));
                // Synthetic: a stage stands for the tree's own stage definition, enumerated above.
                builder.BeginGroup(Nodes.Synthetic(
                    ControlId.For(
                        stage.SkillTreeStage,
                        Keys + "tree/branch/" + branch + "/ring/" + i
                    ),
                    vtable
                ));
                BuildSkills(builder, panel, stage, branch, i);
                builder.EndGroup();
            }
        }

        /// <summary>
        /// Which ring this is, counted out from the middle.
        ///
        /// The game has no name for a ring. The only words it writes anywhere near one are the three
        /// legends down the right-hand column, each a threshold with a leader line pointing at the ring
        /// it unlocks ("Used Skill Points 4":
        /// <c>SkillTreeEditionPanel.RefreshLevelLabels</c> :363-382 writes
        /// <c>Gui.Localize("%SkillTreeStageLevelTitle") + RequiredLevel</c>). Naming the ring from that
        /// was reading the leader line as a caption: every ring of every branch announced itself as
        /// "Used skill points 0" and nothing said what the figure was about. So the ring is named for
        /// where it is - which is the one thing a sighted player can see about it without following a
        /// line - and the threshold is said as the sentence it means, in
        /// <see cref="StageRequirement"/>.
        /// </summary>
        private static string StageName(int ring, int rings)
        {
            return ModStrings.Format(ModStrings.HeroSkillRing, ring + 1, rings);
        }

        /// <summary>
        /// How many points have to have been spent anywhere in the wheel before this ring opens - the
        /// figure the page draws in its right-hand legend, said as what it means.
        ///
        /// Taken from the ring's own definition rather than from the legend labels, because the page
        /// draws one set of them for all three branches and one fewer than there are rings: the outermost
        /// ring's threshold is drawn nowhere at all. The innermost ring asks for nothing, and says so by
        /// saying nothing.
        /// </summary>
        private static string StageRequirement(HeroSkillTreeStageItem stage)
        {
            try
            {
                int required = stage.SkillTreeStage.RequiredLevel;
                return required <= 0
                    ? null
                    : ModStrings.Plural(
                        ModStrings.HeroSkillRingPoint,
                        ModStrings.HeroSkillRingPoints,
                        required
                    );
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The dots along one ring's arc, in the order the ring's definition lists them.
        /// </summary>
        private void BuildSkills(
            GraphBuilder builder,
            SkillTreeEditionPanel panel,
            HeroSkillTreeStageItem stage,
            int branch,
            int ring
        )
        {
            IList<AgeTransform> skills = stage.SkillTreeSkillsTable == null
                ? null
                : stage.SkillTreeSkillsTable.Children;
            for (int i = 0; skills != null && i < skills.Count; i++)
            {
                HeroSkillTreeSkillItem skill =
                    skills[i] == null
                        ? null
                        : skills[i].GetComponent<HeroSkillTreeSkillItem>();
                // Flow control: a skill the stage is not drawing is not one of its own, and is not walked.
                if (
                    skill == null
                    || skill.HeroSkillDefinition == null
                    || !AgeWidgets.Painted(skill.AgeTransform)
                )
                {
                    continue;
                }

                // Synthetic: a skill stands for its DEFINITION, and the loop above - which skips the
                // ones the panel is not drawing - is the honesty about it.
                builder.AddItem(Nodes.Synthetic(
                    ControlId.For(
                        skill.HeroSkillDefinition,
                        Keys + "tree/branch/" + branch + "/ring/" + ring + "/skill/" + i
                    ),
                    SkillVtable(panel, skill)
                ));
            }
        }

        /// <summary>
        /// One skill, which is one press of "spend a point on this".
        ///
        /// It is a BUTTON and not the tick the game drew, because pressing it repeatedly buys the next
        /// level each time up to the skill's last: the dot's tick only means "a level of this is
        /// pending" (<c>HeroSkillTreeSkillItem.Refresh</c> :139 writes
        /// <c>Toggle.State = GetPendingLevels &gt; 0</c>), and a checkbox that said "ticked" would be
        /// saying the wrong thing about a skill on its second of three levels. So what it says is the
        /// level it stands at out of the levels it has, and then that a further level is pending - which
        /// is exactly what the game draws as a ring of coloured arcs round the dot (:143-158).
        ///
        /// Enter is the dot's own click - state first, then the handler, which is what the mouse does
        /// (the game's own handler flips the tick back and lets its refresh rewrite it). It commits
        /// nothing: <c>OnSkillCb</c> :451-470 appends to a pending list and unlocks the skill on a copy
        /// of the hero. A skill at its last level, and one whose ring is locked, are already REFUSING
        /// without anything here: the game switches the dot off at :140-142 for the first and the ring
        /// off for the second.
        ///
        /// <b>The one refusal the mod composes itself.</b> A skill that names other skills as
        /// prerequisites is one the game will not let stand: <c>Refresh</c> :101-130 writes
        /// <c>%NeedTheseSkills</c> and the missing skills' titles onto the dot's tooltip and switches the
        /// dot off - and then line 142 switches it back ON and line 159 overwrites the tooltip with the
        /// skill's own name, so by the time anybody can read either one, both are gone. The game lets the
        /// click through and undoes it on its next refresh. So the test is made here, off the same
        /// <c>RequiredSkills</c> the game reads, and the sentence is put back together out of the game's
        /// own words - approved as a deliberate deviation. No skill in the base game's own hero trees
        /// declares a required skill, so this path belongs to the bonus trees a Nakalim hero carries.
        /// </summary>
        private static NodeVtable SkillVtable(
            SkillTreeEditionPanel panel,
            HeroSkillTreeSkillItem skill
        )
        {
            SkillTreeEditionPanel owner = panel;
            HeroSkillTreeSkillItem it = skill;
            AgeTooltip tooltip = skill.Tooltip ?? AgeWidgets.Raw(skill.AgeTransform);
            Func<string> missing = () => Missing(owner, it);
            Func<bool> enabled = () =>
                AgeWidgets.Operable(it.AgeTransform) && string.IsNullOrEmpty(missing());
            Func<string> level = () => SkillLevel(owner, it);
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => SkillName(it)),
                    GraphNodes.DisabledPart(enabled),
                    GraphNodes.ValuePart(level),
                    new NodeAnnouncement(missing, live: false),
                },
                Sections = GraphNodes.Sections(null, tooltip),
                // What the press produced, read back at once - and nothing at all from a dot that is
                // refusing, which is what every other refusing control in the mod does: the reason was
                // said when focus arrived. A dot the game leaves ENABLED while the hero has no point to
                // spend answers with the level it still stands at, which is the only sign the press
                // went nowhere (the game answers it with a sound).
                StateText = () => enabled() ? level() : null,
                OnActivate = () =>
                {
                    if (enabled())
                    {
                        AgeWidgets.Toggle(it.Toggle);
                    }
                },
            };
            AgeWidgets.Point(vtable, it.Toggle, tooltip, it.AgeTransform);
            return vtable;
        }

        /// <summary>
        /// What the game calls a skill. Off the wrapper the dot builds for it, which is what the tooltip
        /// window heads its dossier with.
        ///
        /// The wrapper is right for a DOT and would be wrong for a starting skill, where it answers the
        /// generic "Starting Skill" instead (<c>GuiHeroSkill.Title</c> :22-32) - but a dot is never a
        /// starting skill (the panel builds those wrappers with the flag set, and only for the box in the
        /// right-hand column: <see cref="Named"/>). Going to the skill's DEFINITION instead would be
        /// worse in both places: <c>GuiWrapper.Title</c> reads the skill's gui element and answers empty
        /// when there is none, while <c>Gui.GetTitle</c> on the definition's name answers the engine's
        /// "(missing GuiElement)" debug string for one of this hero's skills and, for the other, a key
        /// that resolves to the HERO's name (measured on Dmitri Lenko: "HeroSkill01Terrans04 (missing
        /// GuiElement)" and "Dmitri Lenko").
        /// </summary>
        private static string SkillName(HeroSkillTreeSkillItem skill)
        {
            try
            {
                return skill.GuiHeroSkill != null
                    ? AgeText.Clean(skill.GuiHeroSkill.Title)
                    : AgeText.Clean(Gui.Localize(Gui.GetTitle(skill.HeroSkillDefinition.Name)));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Where the skill stands: the level the hero has of it out of the levels it has at
        /// all, and then the level a pending pick would take it to. Both are the numbers the game paints
        /// the ring of arcs round the dot with - one arc per level, coloured for owned, for pending, and
        /// for neither (<c>HeroSkillTreeSkillItem.Refresh</c> :143-158).</summary>
        private static string SkillLevel(
            SkillTreeEditionPanel panel,
            HeroSkillTreeSkillItem skill
        )
        {
            try
            {
                HeroSkillDefinition definition = skill.HeroSkillDefinition;
                int levels = definition.SkillLevels.Length;
                int owned = panel.GuiHero.GetHeroSkillLevel(definition) + 1;
                int pending = panel.GetTotalSkillLevel(definition) + 1;
                MessageBuilder message = new MessageBuilder();
                message.ListItem(ModStrings.Format(ModStrings.HeroSkillLevel, owned, levels));
                if (pending > owned)
                {
                    message.ListItem(ModStrings.Format(ModStrings.HeroSkillPending, pending));
                }

                return message.Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The skills this one needs first and the hero has not got, in the game's own words -
        /// the sentence the game writes and then overwrites, put back together. Null where there is
        /// nothing missing, which is every skill in the base game's own hero trees.</summary>
        private static string Missing(
            SkillTreeEditionPanel panel,
            HeroSkillTreeSkillItem skill
        )
        {
            try
            {
                HeroSkillDefinition[] required = skill.HeroSkillTreeSkill.RequiredSkills;
                if (required == null || required.Length == 0)
                {
                    return null;
                }

                MessageBuilder message = new MessageBuilder();
                int missing = 0;
                for (int i = 0; i < required.Length; i++)
                {
                    if (panel.GetTotalSkillLevel(required[i]) >= 0)
                    {
                        continue;
                    }

                    if (missing++ == 0)
                    {
                        message.Fragment(AgeText.Clean(Gui.Localize(NeedTheseSkills)));
                    }

                    message.ListItem(
                        AgeText.Clean(Gui.Localize(Gui.GetTitle(required[i].Name)))
                    );
                }

                return missing == 0 ? null : message.Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private const string NeedTheseSkills = "%NeedTheseSkills";

        /// <summary>
        /// The right-hand column, box by box in the order the game stacks them: how much of each branch
        /// is done, the points each ring needs, the skills the hero started with, the masteries they
        /// have built up - and, for a hero who can carry them, the relic skills.
        ///
        /// Read box by box because three of the four say their figures with an icon and no words: a
        /// starting skill and a mastery line are named only on the wrapper behind their tooltips, and a
        /// completion line is a figure and a name drawn side by side.
        ///
        /// Each box is also a REGION, so the jump key steps down the column a box at a time rather than
        /// making the player walk a dozen rows to reach the masteries. They carry no label of the mod's:
        /// the game draws a heading over every box, and that heading is the region's first row.
        /// </summary>
        private void BuildTreeStats(GraphBuilder builder, SkillTreeEditionPanel panel)
        {
            AgeTransform banner = Banner(panel);
            IList<AgeTransform> boxes = banner == null ? null : banner.Children;
            if (boxes == null)
            {
                return;
            }

            builder.BeginStop(TreeStatsStop);
            try
            {
                for (int i = 0; i < boxes.Count; i++)
                {
                    AgeTransform box = boxes[i];
                    // Flow control: a box the page is not drawing has nothing this tree put in it, and is not walked.
                    if (!AgeWidgets.Painted(box))
                    {
                        continue;
                    }

                    if (HoldsLegends(box, panel))
                    {
                        continue;
                    }

                    // A region per box, keyed and unlabelled: the box already draws its own heading as
                    // the first row of the region, so a label of the mod's would say it twice.
                    _cells.Clear();
                    if (AgeWidgets.Under(panel.TreeCompletionLinesTable, box))
                    {
                        builder.SetRegion(Keys + "tree-stats/completion");
                        AddBox(box, panel.TreeCompletionLinesTable, "tree/completion");
                    }
                    else if (AgeWidgets.Under(panel.StartingSkillItemsTable, box))
                    {
                        builder.SetRegion(Keys + "tree-stats/starting");
                        AddNamedBox(
                            box,
                            panel.StartingSkillItemsTable,
                            "tree/starting",
                            StartingSkillName
                        );
                    }
                    else if (
                        panel.HeroMasteryPanel != null
                        && AgeWidgets.Under(panel.HeroMasteryPanel.MasteryLinesContainer, box)
                    )
                    {
                        builder.SetRegion(Keys + "tree-stats/mastery");
                        AddNamedBox(
                            box,
                            panel.HeroMasteryPanel.MasteryLinesContainer,
                            "tree/mastery",
                            Named
                        );
                    }
                    else if (AgeWidgets.Under(panel.RelicSkillItemsTable, box))
                    {
                        builder.SetRegion(Keys + "tree-stats/relics");
                        AddRelics(box, panel);
                    }
                    else
                    {
                        builder.SetRegion(Keys + "tree-stats/box/" + i);
                        AddBox(box, null, "tree/box/" + i);
                    }

                    Cells.EmitLinear(builder, _cells);
                }
            }
            catch (Exception e)
            {
                Log.Warn("hero inspection: reading the skill page's right column threw: " + e);
            }
        }

        /// <summary>A box of the right-hand column: whatever heading it draws, and then one line per row
        /// of the table inside it. Both are read as ROWS rather than walked into: a heading is a group
        /// holding a label and an icon, and its explaining sentence is on the label rather than on the
        /// group, which is what <see cref="AddRow"/> is for.</summary>
        private void AddBox(AgeTransform box, AgeTransform table, string key)
        {
            AddHeads(box, table, key);
            IList<AgeTransform> rows = table == null ? null : table.Children;
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                AddRow(rows[i], key + "/row/" + i);
            }
        }

        /// <summary>Whatever a box draws above its table - a heading, or in one case the three ring
        /// legends that are the whole of the box.</summary>
        private void AddHeads(AgeTransform box, AgeTransform table, string key)
        {
            IList<AgeTransform> children = box.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child != null && !ReferenceEquals(child, table))
                {
                    AddRow(child, key + "/head/" + i);
                }
            }
        }

        /// <summary>The same for a box whose rows draw an icon and a figure and say what they are
        /// nowhere in the row: a mastery line (the level reached out of the highest this hero can reach)
        /// and a starting skill (which draws nothing but its own symbol). Both keep what they are on the
        /// wrapper behind the row's tooltip, and <paramref name="name"/> is how the box says which of
        /// the two questions to ask it.</summary>
        private void AddNamedBox(
            AgeTransform box,
            AgeTransform table,
            string key,
            Func<AgeTooltip, string> name
        )
        {
            AddHeads(box, table, key);
            IList<AgeTransform> rows = table == null ? null : table.Children;
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                AgeTransform row = rows[i];
                // Flow control: a row the table is not drawing has no words of this box's, and is not walked.
                if (!AgeWidgets.Painted(row))
                {
                    continue;
                }

                AgeTooltip tooltip = AgeWidgets.Raw(row);
                string said = name(tooltip);
                AgeTransform at = row;
                NodeVtable vtable = GraphNodes.Readout(
                    () => said,
                    () => AgeWidgets.TextOf(at),
                    null,
                    tooltip
                );
                AgeWidgets.PointAt(vtable, row);
                Cells.Add(_cells, row, ControlId.For(row, Keys + key + "/row/" + i), vtable);
            }
        }

        /// <summary>What a row drawn as a bare symbol is called: the name the game keeps on the wrapper
        /// behind its tooltip. A mastery line's is the mastery.</summary>
        private static string Named(AgeTooltip tooltip)
        {
            return AgeWidgets.TooltipTitle(tooltip);
        }

        /// <summary>
        /// Which masteries a starting skill counts towards, which is what tells one of them from the
        /// next.
        ///
        /// The tooltip's own title will not do it: the game answers "Starting Skill" for every one of
        /// them (<c>GuiHeroSkill.Title</c> :22-32 returns that instead of the skill's own for a starting
        /// skill), so a hero with two has two rows saying the same words. Nor will the skill's own name -
        /// <c>Gui.GetTitle</c> on Dmitri Lenko's two answers the engine's "HeroSkill01Terrans04 (missing
        /// GuiElement)" for one and the HERO's name for the other.
        ///
        /// What the skill really is, in words the game keeps for exactly this, is the mastery its first
        /// level counts towards (<c>HeroSkillDefinition.HeroSkillLevelDefinition.MasteryLevels</c>) -
        /// "Command", "Labor" - which is also what the Masteries box below reads out, so the two rows
        /// say the same words about the same thing.
        /// </summary>
        private static string StartingSkillName(AgeTooltip tooltip)
        {
            try
            {
                GuiHeroSkill skill = tooltip == null ? null : tooltip.Target as GuiHeroSkill;
                HeroSkillDefinition definition =
                    skill == null ? null : skill.HeroSkillDefinition;
                HeroSkillDefinition.HeroSkillLevelDefinition[] levels =
                    definition == null ? null : definition.SkillLevels;
                HeroSkillDefinition.MasteryLevel[] masteries =
                    levels == null || levels.Length == 0 ? null : levels[0].MasteryLevels;
                List<string> names = new List<string>(
                    masteries == null ? 0 : masteries.Length
                );
                for (int i = 0; masteries != null && i < masteries.Length; i++)
                {
                    names.Add(MasteryName(masteries[i].MasteryName));
                }

                // Through the one home for "several things said as one line" rather than a
                // MessageBuilder loop of this file's own: the separator between them is a translated
                // template, and a skill counting towards two masteries must take the same one every
                // other list in the mod takes.
                return SpokenList.Items(names);
            }
            catch (Exception e)
            {
                Log.Warn("hero inspection: naming a starting skill threw: " + e);
                return null;
            }
        }

        /// <summary>What the game calls a mastery, under the naming convention its own mastery rows are
        /// titled by. Silence rather than the key, which is never worth saying out loud.</summary>
        private static string MasteryName(Amplitude.StaticString mastery)
        {
            return AgeText.Title("%" + mastery + "Title");
        }

        /// <summary>
        /// The relic skills a Nakalim or Templar hero can learn, which the game draws as a flat strip
        /// rather than as a wheel and gates on relics rather than on skill points
        /// (<c>SkillTreeEditionPanel.OnRelicSkillCb</c> :513-544). Pressing one is its own toggle, and
        /// unlike a skill it really is a toggle: the same press learns and unlearns.
        /// </summary>
        private void AddRelics(AgeTransform box, SkillTreeEditionPanel panel)
        {
            IList<AgeTransform> children = box.Children;
            for (int i = 0; children != null && i < children.Count; i++)
            {
                AgeTransform child = children[i];
                if (child != null && !ReferenceEquals(child, panel.RelicSkillItemsTable))
                {
                    Cells.AddReadout(_cells, child, Keys + "tree/relic/head/" + i);
                }
            }

            IList<AgeTransform> rows = panel.RelicSkillItemsTable == null
                ? null
                : panel.RelicSkillItemsTable.Children;
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                RelicSkillTreeItem relic =
                    rows[i] == null ? null : rows[i].GetComponent<RelicSkillTreeItem>();
                // Flow control: a relic row the panel is not drawing is not one this hero holds, and is not walked.
                if (
                    relic == null
                    || relic.HeroSkillDefinition == null
                    || !AgeWidgets.Painted(relic.AgeTransform)
                )
                {
                    continue;
                }

                RelicSkillTreeItem it = relic;
                AgeTooltip tooltip = relic.Tooltip ?? AgeWidgets.Raw(relic.AgeTransform);
                NodeVtable vtable = GraphNodes.Checkbox(
                    () => RelicName(it),
                    () => it.Toggle != null && it.Toggle.State,
                    () => AgeWidgets.Toggle(it.Toggle),
                    () => AgeWidgets.Operable(it.AgeTransform),
                    tooltip
                );
                AgeWidgets.Point(vtable, it.Toggle, tooltip, it.AgeTransform);
                Cells.Add(
                    _cells,
                    relic.AgeTransform,
                    ControlId.For(relic.AgeTransform, Keys + "tree/relic/row/" + i),
                    vtable
                );
            }
        }

        private static string RelicName(RelicSkillTreeItem relic)
        {
            try
            {
                return relic.GuiHeroSkill != null
                    ? AgeText.Clean(relic.GuiHeroSkill.Title)
                    : AgeText.Clean(Gui.Localize(Gui.GetTitle(relic.HeroSkillDefinition.Name)));
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
