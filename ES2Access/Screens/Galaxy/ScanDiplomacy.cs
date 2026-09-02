using System;
using System.Collections.Generic;
using Amplitude;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.ES2.UI;
using ES2Access.UI;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>
    /// The two furthest-out rungs of the scan ladder, where the lens draws no star at all.
    ///
    /// What it draws instead is DIPLOMACY: a painted galaxy, a circle at each empire's centre, a link
    /// from the watching empire's centre to every other's, a curve from each of the watched empire's
    /// own colonies back to that centre, and a label wherever there is something to say - a major's
    /// explored home, or a fight in orbit. So the tree there is a LIST OF EMPIRES (RULED 2026-09-01),
    /// with the battle labels beside it and the watched empire's curves hanging under its own row.
    ///
    /// It is the same shape the closer bands wear: an empire heading with its holdings under it. At
    /// 1-2 the holdings the lens draws are the watched empire's tethered colonies, and from 3 they are
    /// the stars the lens paints an owner ring on - and because both use the one key
    /// (<see cref="OwnerKey"/>), a system the player is standing on when the camera crosses into the
    /// diplomacy band seats on its OWNER's row rather than falling out of the tree.
    /// </summary>
    public sealed partial class GalaxyHudScreen
    {
        /// <summary>One empire the diplomacy band lists, and where the watching empire's intelligence
        /// puts it - which is what the list is ordered by and what the row says as a position.</summary>
        private struct EmpireRow
        {
            public Empire Empire;

            public GalaxyPosition Centre;

            public bool Placed;
        }

        private readonly List<EmpireRow> _empireList = new List<EmpireRow>();

        private readonly List<StarSystemNode> _spokes = new List<StarSystemNode>();

        private static readonly Comparison<EmpireRow> EmpireOrder = CompareEmpires;

        /// <summary>The empires read in the order the picture puts them in - by the centre the watching
        /// empire knows, in the same reading order the rest of this map is walked in. An empire with no
        /// known centre has no circle drawn for it, so it sorts after the ones that have.</summary>
        private static int CompareEmpires(EmpireRow left, EmpireRow right)
        {
            if (left.Placed != right.Placed)
            {
                return left.Placed ? -1 : 1;
            }

            return left.Placed ? ComparePositions(left.Centre, right.Centre) : 0;
        }

        /// <summary>
        /// WHOSE POINT OF VIEW THE LENS IS DRAWING FROM.
        ///
        /// Ordinarily the player's, and the game resets it to the player every time the scan view is
        /// opened (<c>DiplomacyScanViewWindow.OnBeginShow</c>). The swap toggle on a major's home label
        /// points it at that empire instead, and from then on every line, circle and relation icon on
        /// the band is drawn from THEIR records - so it is the empire this band's reading is composed
        /// against too.
        ///
        /// The player is the fall-back wherever the window has no answer, which is also every rung
        /// outside this band: the closer lenses draw no diplomacy at all, so their headings ask about
        /// the player and never about whoever the diplomacy band was last pointed at
        /// (<see cref="AddOwnerGroup"/>).
        /// </summary>
        private static Empire Watching(Empire player)
        {
            try
            {
                DiplomacyScanViewWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<DiplomacyScanViewWindow>(false)
                    : null;
                Empire watching = window == null ? null : window.WatchingEmpire;
                return watching == null ? player : watching;
            }
            catch (Exception)
            {
                return player;
            }
        }

        /// <summary>
        /// The empires the player has met, self included - always at least one, which is what makes
        /// this band's tree never empty and the reconciliation into it always land somewhere.
        ///
        /// Met is the game's own flag on the diplomatic relation
        /// (<c>DiplomaticAbilityDefinition.Names.IsKnown</c>, the same one
        /// <c>DepartmentOfForeignAffairs.HasMetAnyMajorEmpire</c> counts). MAJORS only, matching the
        /// lens, which walks the major empires and draws nothing for a minor or a pirate; and an
        /// eliminated empire holds no colony, so the lens has neither centre nor line to draw for it.
        /// </summary>
        private void GatherEmpires(Empire player, Empire watching)
        {
            _empireList.Clear();
            try
            {
                Game game = Gui.Game;
                Empire[] empires = game == null ? null : game.Empires;
                DepartmentOfForeignAffairs foreign =
                    player == null ? null : player.GetAgency<DepartmentOfForeignAffairs>();
                for (int i = 0; empires != null && i < empires.Length; i++)
                {
                    MajorEmpire major = empires[i] as MajorEmpire;
                    if (major == null || major.HasBeenEliminated)
                    {
                        continue;
                    }

                    if (!ReferenceEquals(major, player) && !Met(foreign, major))
                    {
                        continue;
                    }

                    EmpireRow row = new EmpireRow { Empire = major };
                    GalaxyPosition centre;
                    row.Placed = KnownCentre(major, watching, out centre);
                    row.Centre = centre;
                    _empireList.Add(row);
                }

                _empireList.Sort(EmpireOrder);
            }
            catch (Exception e)
            {
                Log.Warn("scan: gathering the met empires threw: " + e);
            }
        }

        private static bool Met(DepartmentOfForeignAffairs foreign, Empire other)
        {
            try
            {
                DiplomaticRelation relation =
                    foreign == null ? null : foreign.GetDiplomaticRelation(other);
                return relation != null
                    && relation.HasAbility(DiplomaticAbilityDefinition.Names.IsKnown);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// The diplomacy band's whole tree: the empires, then the fights the lens has planted a label
        /// over.
        ///
        /// The battles are a level of their own rather than children of an empire, because the lens
        /// plants that label at a NODE and not at an empire - two empires are fighting there, and
        /// filing the row under either would say the fight was one of theirs.
        /// </summary>
        private void BuildEmpireList(GraphBuilder builder, Empire player)
        {
            Empire watching = Watching(player);
            GatherEmpires(player, watching);
            IList<ScanViewDiplomacyLabel> labels = DiplomacyLabels();
            for (int i = 0; i < _empireList.Count; i++)
            {
                AddEmpireRow(builder, _empireList[i], player, watching, labels);
            }

            AddBattleRows(builder, player, labels);
        }

        /// <summary>
        /// One empire as the band draws it: who they are, how the watching empire stands with them,
        /// and where the watching empire's records put them.
        ///
        /// The position is spoken as a POSITION and never as a home (RULED 2026-09-01): the record is
        /// the empire's home system where the player has discovered it and their highest-influence
        /// known colony where they have not, and the lens draws the identical circle and line in both
        /// cases - so naming the case would hand a keyboard player a fact the picture withholds.
        ///
        /// Under the row: the swap toggle wherever the game draws one, and - for the empire the lens is
        /// WATCHING - the curves it tethers to that centre (<see cref="AddSpokes"/>). An empire with
        /// neither is a leaf, exactly as the picture has nothing under it.
        /// </summary>
        private void AddEmpireRow(
            GraphBuilder builder,
            EmpireRow row,
            Empire player,
            Empire watching,
            IList<ScanViewDiplomacyLabel> labels
        )
        {
            Empire it = row.Empire;
            Empire looking = player;
            Empire against = watching;
            ScanViewDiplomacyLabel swap = SwapLabelFor(row.Empire, labels);
            bool watched = ReferenceEquals(row.Empire, watching);
            GatherSpokes(watched ? row.Empire : null);
            string head = OwnerKey(ScanBucket.Empire, row.Empire);
            ControlId id = ControlId.Structural(head);
            bool inhabited = swap != null || _spokes.Count > 0;
            NodeVtable vtable = inhabited
                ? GraphNodes.Group(() => LeaderName(it, looking))
                : GraphBuilder.Label(() => LeaderName(it, looking));
            vtable.Announcements = new List<NodeAnnouncement>(vtable.Announcements);
            if (row.Placed)
            {
                vtable.Announcements.Add(GalaxyCoordinates.Part(row.Centre));
            }

            string home = HomeLabelled(swap);
            if (home != null)
            {
                vtable.Announcements.Add(
                    GraphNodes.LabelPart(() => ModStrings.Format(ModStrings.ScanEmpireHome, home))
                );
            }

            vtable.Announcements.Add(
                GraphNodes.ValuePart(() => RelationWord(it, against), false)
            );

            if (!inhabited)
            {
                // Synthetic: the row stands for the EMPIRE, which the lens paints as a circle and a
                // line rather than drawing any one widget for. A leaf, because the picture has nothing
                // under it: the curves belong to the watched empire alone and the toggle is drawn only
                // at an explored home.
                builder.AddItem(Nodes.Synthetic(id, vtable));
                return;
            }

            Seed(builder, id);
            bool open = builder.ExpandAll || builder.IsExpanded(id);
            builder.BeginGroup(Nodes.Synthetic(id, vtable), expanded: open);
            if (open)
            {
                AddSwapToggle(builder, swap, head);
                AddSpokes(builder, head, player);
            }

            builder.EndGroup();
        }

        /// <summary>An empire by the name the game's own system dossier heads itself with - the same
        /// call the owner headings and a system's owner word use, so one empire is one word wherever it
        /// is said.</summary>
        private static string LeaderName(Empire empire, Empire looking)
        {
            try
            {
                GuiEmpire wrapper =
                    empire == null
                        ? null
                        : Gui.GuiWrapperProviderService.GetGuiEmpire(empire);
                return wrapper == null ? null : AgeText.Clean(wrapper.GetLeaderName(looking));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>How the empire the lens is watching stands with this one, in the game's own word for
        /// that state - the tinted icon the label draws beside the name, read from the model the game
        /// tinted it from. Nothing at all for the watching empire itself, which is exactly where the
        /// game hides the icon (<c>ScanViewDiplomacyLabel.RefreshEmpireNameLine</c> :313-323).</summary>
        private static string RelationWord(Empire empire, Empire watching)
        {
            try
            {
                if (empire == null || watching == null || ReferenceEquals(empire, watching))
                {
                    return null;
                }

                GuiEmpire theirs = Gui.GuiWrapperProviderService.GetGuiEmpire(empire);
                GuiEmpire ours = Gui.GuiWrapperProviderService.GetGuiEmpire(watching);
                DiplomaticRelationState state =
                    theirs == null || ours == null ? null : ours.GetRelationStateWith(theirs);
                return state == null
                    ? null
                    : AgeText.Clean(Gui.GetLocalizedTitle(state.Name));
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the swap toggle ----

        /// <summary>
        /// The label whose swap toggle points the lens at this empire, and only while the game is
        /// really drawing that line.
        ///
        /// The game offers the swap in exactly one place: inside the empire-name line of a label
        /// standing at a MAJOR's home system the player has explored
        /// (<c>ScanViewDiplomacyLabel.RefreshEmpireNameLine</c> :304-312 - the line is hidden below
        /// exploration state 2, and a battle-only label draws no line at all). The mod offers it where
        /// the game does and nowhere else, which is parity rather than caution: the mechanism works for
        /// any met empire, and using it from a row the picture has no toggle on would be a power a
        /// sighted player does not have.
        /// </summary>
        private static ScanViewDiplomacyLabel SwapLabelFor(
            Empire empire,
            IList<ScanViewDiplomacyLabel> labels
        )
        {
            // The drawn policy is the game's own answer to whether the toggle is on the screen at all:
            // the name LINE is what it lives inside, and the label goes on drawing its battle line
            // with the name line hidden - so a label passing the empire test can still be one the
            // player has no toggle on.
            return empire == null
                ? null
                : LabelFor(
                    labels,
                    l =>
                        l.MainColonizedStarSystem != null
                        && ReferenceEquals(l.MainColonizedStarSystem.Empire, empire),
                    l =>
                        AgeWidgets.Visible(l.AgeTransform)
                        && AgeWidgets.Painted(l.EmpireNameLine)
                );
        }

        /// <summary>
        /// Point the lens at this empire instead - the game's own toggle, named with the game's own
        /// word for the gesture (<c>%DiplomacyScreenSwapModeTitle</c>, whose description is the very
        /// tooltip the game hangs on this toggle) because the widget itself draws no text: on the map
        /// it is the empire's name line that is clickable, and the name is already the row above.
        ///
        /// Asked live as well as at build time: the game switches the toggle off the moment the lens is
        /// already watching somebody else, and a row must not go on offering what the picture has
        /// stopped offering.
        /// </summary>
        private static void AddSwapToggle(
            GraphBuilder builder,
            ScanViewDiplomacyLabel label,
            string head
        )
        {
            AgeControlToggle toggle = label == null ? null : label.SwapToggle;
            AgeTransform widget = AgeWidgets.Transform(toggle);
            if (widget == null)
            {
                return;
            }

            AgeControlToggle it = toggle;
            NodeVtable vtable = GraphNodes.Checkbox(
                () => AgeText.Clean(Gui.Localize(SwapModeTitle)),
                () => it.State,
                () => AgeWidgets.Toggle(it),
                () => AgeWidgets.Operable(widget),
                AgeWidgets.Raw(widget)
            );
            AgeWidgets.Point(vtable, it);
            builder.AddItem(
                Nodes.Drawn(ControlId.For(toggle, head + "/swap"), vtable, toggle)
            );
        }

        private const string SwapModeTitle = "%DiplomacyScreenSwapModeTitle";

        /// <summary>
        /// THE STAR THE LENS WRITES THIS EMPIRE'S NAME OVER (owner ruling 2026-09-01, after playtest).
        ///
        /// The centre a row says is a POSITION and never a home, because the circle the lens draws
        /// there is the same circle whether the record is a capital or the highest-influence colony the
        /// watcher can see (RULED). But the lens ALSO writes the empire's name, in its own colours, AT
        /// its home system - and the mod said nothing about that at all, so the player's capital was
        /// the one thing on the band with no row of any kind while a tethered colony had one. Reported
        /// from the owner's own game: Dusay gone, Heka present.
        ///
        /// The gate is the drawing's own, not a second copy of it: this is the label whose empire-name
        /// LINE the game is painting for this empire (<see cref="SwapLabelFor"/>), and the game paints
        /// that line exactly at a MAJOR's home system the player has explored
        /// (<c>ScanViewDiplomacyLabel.RefreshEmpireNameLine</c> :303 - <c>ExplorationState >= 2</c> and
        /// <c>IsMajorHomeSystem</c>). So the player's own home always answers, a foreign home answers
        /// once it has been explored, and an unexplored foreign centre stays a position and nothing
        /// more. The name needs no knowledge gate of its own for the same reason: the line the mod is
        /// reading is drawn only where the player has explored the place.
        /// </summary>
        private static string HomeLabelled(ScanViewDiplomacyLabel label)
        {
            try
            {
                StarSystemNode node = label == null ? null : label.StarSystemNode;
                return node == null ? null : node.LocalizedName;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- the spokes ----

        /// <summary>
        /// The colonies the lens tethers to the watched empire's centre.
        ///
        /// The game draws one curve per colonized system of the WATCHED empire, gated by that empire's
        /// own knowledge and not by the player's (<c>GalaxyStarSystem.UpdateDiplomaticScanView</c>
        /// :931, :949-973): the colony has to be visible to them and the node revealed to them, and the
        /// curve at their own home system is never shown because it would have no length. Verified live
        /// and optically (2026-09-01): pointed at another empire the lens drew five curves, four of
        /// them at systems the player has never explored - it is an intelligence tool for locating a
        /// watched empire's holdings, and the rows mirror it exactly.
        ///
        /// Which is why the row's NAME is the player's own knowledge and not the watched empire's: the
        /// picture reveals a place, not a name, and a star the player has not explored is one the map
        /// already refuses to name anywhere else.
        /// </summary>
        private void GatherSpokes(Empire watched)
        {
            _spokes.Clear();
            if (watched == null)
            {
                return;
            }

            try
            {
                DepartmentOfTheInterior interior = watched.GetAgency<DepartmentOfTheInterior>();
                StarSystemNode home = interior == null ? null : interior.HomeSystemNode;
                System.Collections.IList colonies =
                    interior == null ? null : interior.ColonizedStarSystems as System.Collections.IList;
                for (int i = 0; colonies != null && i < colonies.Count; i++)
                {
                    ColonizedStarSystem colony = colonies[i] as ColonizedStarSystem;
                    StarSystemNode node = colony == null ? null : colony.Node as StarSystemNode;
                    if (colony == null || colony.Destroyed || node == null)
                    {
                        continue;
                    }

                    if (home != null && ReferenceEquals(node, home))
                    {
                        continue;
                    }

                    if (
                        (int)colony.Visibility[watched] < (int)EntityVisibility.Layer.Known
                        || (int)node.Exploration[watched.Index]
                            < (int)EntityExploration.State.Revealed
                    )
                    {
                        continue;
                    }

                    _spokes.Add(node);
                }

                _spokes.Sort(ReadingOrder);
            }
            catch (Exception e)
            {
                Log.Warn("scan: gathering an empire's tethered colonies threw: " + e);
            }
        }

        /// <summary>The tethered colonies as rows: the place, and where it is. No owner word - the
        /// heading this row hangs under IS whose it is, and the word would be composed from what the
        /// PLAYER can see of the colony, which at a system the lens is revealing over fog would
        /// contradict the heading rather than add to it.</summary>
        private void AddSpokes(GraphBuilder builder, string head, Empire player)
        {
            for (int i = 0; i < _spokes.Count; i++)
            {
                StarSystemNode node = _spokes[i];
                StarSystemNode it = node;
                bool named = Perceived(node, player);
                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(
                            () =>
                                named
                                    ? it.LocalizedName
                                    : ModStrings.Get(ModStrings.GalaxySystemUnexplored)
                        ),
                        GalaxyCoordinates.Part(node.GalaxyPosition),
                    },
                };
                // Synthetic: the curve is drawn by the renderer rather than by a widget, and the row
                // stands for the star at its far end.
                builder.AddItem(
                    Nodes.Synthetic(
                        ControlId.For(node, head + "/spoke/" + node.GUID),
                        vtable
                    )
                );
            }
        }

        // ---- the battle labels ----

        /// <summary>
        /// One row per fight the lens has planted a label over - which is not one per battle in the
        /// galaxy: the label draws its battle line only where the player has explored the node and the
        /// game's own encounter repository has a fight in progress there
        /// (<c>ScanViewDiplomacyLabel.RefreshBattles</c>).
        ///
        /// The system is named by the player's own knowledge, coordinates alone otherwise - the same
        /// rule the spokes follow, and for the same reason. Fixture-blocked: no save in this project
        /// has ever had a fight in orbit while the lens was up, so the reading is verified by code.
        /// </summary>
        private void AddBattleRows(
            GraphBuilder builder,
            Empire player,
            IList<ScanViewDiplomacyLabel> labels
        )
        {
            for (int i = 0; i < labels.Count; i++)
            {
                ScanViewDiplomacyLabel label = labels[i];
                GameNode node = label == null ? null : label.GameNode;
                // Flow control: whether this label contributes a battle row at all. The label is
                // drawn for its empire name as well, and most of them carry no fight.
                if (
                    node == null
                    || !AgeWidgets.Visible(label.AgeTransform)
                    || !AgeWidgets.Painted(label.BattleLine)
                )
                {
                    continue;
                }

                ScanViewDiplomacyLabel it = label;
                GameNode place = node;
                bool named = Perceived(node, player);
                NodeVtable vtable = new NodeVtable
                {
                    Announcements = new List<NodeAnnouncement>
                    {
                        GraphNodes.LabelPart(
                            () =>
                                named
                                    ? place.LocalizedName
                                    : ModStrings.Get(ModStrings.GalaxySystemUnexplored)
                        ),
                        GalaxyCoordinates.Part(node.GalaxyPosition),
                        GraphNodes.ValuePart(() => BattleWord(it), false),
                    },
                };
                AgeWidgets.PointAt(vtable, label.BattleLine);
                // Synthetic: the row stands for the FIGHT at that node, which the label draws as two
                // tinted emblems and no words at all.
                builder.AddItem(
                    Nodes.Synthetic(
                        ControlId.For(node, "galaxy:battle/" + node.GUID),
                        vtable
                    )
                );
            }
        }

        /// <summary>Who is fighting, from the same fleets the label asks about - a docked fleet still
        /// alive and in an encounter (<c>ScanViewDiplomacyLabel.CollectFightingEmpires</c>). The line
        /// itself is two emblems with no words, so the names come from the model the game tinted them
        /// from.</summary>
        private static string BattleWord(ScanViewDiplomacyLabel label)
        {
            try
            {
                MessageBuilder empires = new MessageBuilder();
                System.Collections.IList docked = label.GameNode.DockedEntities
                    as System.Collections.IList;
                List<int> said = new List<int>();
                for (int i = 0; docked != null && i < docked.Count; i++)
                {
                    Fleet fleet = docked[i] as Fleet;
                    if (fleet == null || fleet.IsDestroyed || !fleet.IsInEncounter)
                    {
                        continue;
                    }

                    Empire empire = fleet.DisplayedEmpire;
                    if (empire == null || said.Contains(empire.Index))
                    {
                        continue;
                    }

                    said.Add(empire.Index);
                    empires.ListItemForcedComma(LeaderName(empire, Gui.PlayerEmpire));
                }

                string names = empires.Build();
                return string.IsNullOrEmpty(names)
                    ? ModStrings.Get(ModStrings.ScanBattleHere)
                    : ModStrings.Format(ModStrings.ScanBattle, names);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---- where a diplomacy-band row stands ----

        /// <summary>The key words the band's own rows are named by. The empire heading shares
        /// <c>owner</c> with the closer lenses' groups, which is what makes a system seat on its owner's
        /// row when the camera crosses in; the other two are this band's alone.</summary>
        private const string EmpireSegment = "owner";

        private const string SpokeSegment = "spoke";

        private const string BattleSegment = "battle";

        /// <summary>
        /// WHERE A DIPLOMACY-BAND ROW STANDS (owner ruling 2026-09-01, after playtest).
        ///
        /// At every other rung the map draws stars, and a heading is a word written over some of them:
        /// it is not a place, the rows inside it are, and refusing to arm the inspect cell from one is
        /// what stops a constellation lending its centroid to a cursor (<c>PlacedRows</c>). At the two
        /// furthest-out rungs of the scan ladder the lens names no star AT ALL, so that reasoning
        /// inverts - the empire rows, their tethered colonies and the fights are the whole picture, each
        /// drawn at a position, and refusing all of them left the TERRITORY SURVEY - the one reading
        /// that band exists for - armable from a point bookmark and nowhere else. Measured: Ctrl+I on a
        /// spoke row was consumed, said nothing and armed nothing.
        ///
        /// So at this band a row answers with what the lens draws it AT: an empire with the centre the
        /// watching empire's intelligence has for it (an empire it cannot place has no circle drawn and
        /// still stands nowhere), a spoke and a battle with the star at the end of them. A row with no
        /// place of its own - the Bookmarks heading - keeps the silent refusal, and so does every row of
        /// every other band, because the question is asked only while the lens is drawing empires.
        ///
        /// The three questions besides arming (Enter's order, the leap trail, the restore) are the
        /// registry's as before: what changes here is where a row IS, and the table goes on saying what
        /// a row can DO.
        /// </summary>
        private bool DiplomacyRowPlace(ControlId id, out GalaxyPosition at)
        {
            at = default(GalaxyPosition);
            if (id == null || !Scanning || !ZoomBands.Shows(BandKind.Empires))
            {
                return false;
            }

            string segment = PlacedRows.SegmentOf(id.StructuralKey);
            if (segment == SpokeSegment || segment == BattleSegment)
            {
                IGameEntityWithGalaxyPosition drawn = id.Subject as IGameEntityWithGalaxyPosition;
                if (drawn == null)
                {
                    return false;
                }

                at = drawn.GalaxyPosition;
                return true;
            }

            if (segment != EmpireSegment)
            {
                return false;
            }

            string key = id.StructuralKey as string;
            for (int i = 0; key != null && i < _empireList.Count; i++)
            {
                EmpireRow row = _empireList[i];
                if (row.Placed && OwnerKey(ScanBucket.Empire, row.Empire) == key)
                {
                    at = row.Centre;
                    return true;
                }
            }

            return false;
        }
        /// <summary>Every label the diplomacy lens is holding, for the length of one frame - the same
        /// pooling reason the other two label walks are held for: the window instantiates one per game
        /// node and several callers want the list in a frame. A window that exists and is NOT SHOWN is
        /// no labels at all: its labels are still bound to whatever it last drew.</summary>
        private static readonly LabelSweep<ScanViewDiplomacyLabel> DiplomacyLenses =
            new LabelSweep<ScanViewDiplomacyLabel>("scan", DiplomacyLabelsWindow);

        private static IList<ScanViewDiplomacyLabel> DiplomacyLabels()
        {
            return DiplomacyLenses.Labels();
        }

        private static Component DiplomacyLabelsWindow()
        {
            DiplomacyScanViewWindow window = Gui.GuiServiceAvailable
                ? Gui.GuiService.GetWindow<DiplomacyScanViewWindow>(false)
                : null;
            return window == null || !window.Shown ? null : window;
        }
    }
}
