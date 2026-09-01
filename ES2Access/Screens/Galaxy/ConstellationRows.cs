using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Amplitude;
using ES2Access.Core.Bookmarks;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;
using ES2Access.UI.Bookmarks;
using ES2Access.UI.Input;
using UnityEngine;

namespace ES2Access.Screens
{
    /// <summary>A stretch of sky as a row, and the unnamed stars gathered under one of its own.</summary>
    public sealed partial class GalaxyHudScreen
    {
        /// <summary>
        /// One constellation as a group node: the name the map writes across it, the game's own
        /// dossier on it as the node's tooltip, and the systems in it as its children.
        ///
        /// The label the name is read off is one the window keeps per constellation and shows for any
        /// the empire has explored. At the zoom the game is played at its alpha is nought - the picture
        /// fades constellation names out as the camera comes in - and that is deliberately not asked
        /// about: the label exists, the game keeps it bound, and its tooltip reads. What decides
        /// whether this group is named at all is the same gate the label itself uses
        /// (<see cref="Explored"/>), never how faded it happens to be at this moment.
        ///
        /// No coordinate pair (owner ruling 2026-08-20). A constellation is a REGION, and the centroid
        /// the game stores for it is where its name is written rather than a place anything stands - a
        /// pair here would be a place the player could steer to and find nothing.
        ///
        /// Closing the group takes the camera back out, exactly as closing a system does and for the
        /// same reason: it is the one gesture that means "I am done reading in there". Only while the
        /// camera is still inside THIS constellation - a player who has since read their way somewhere
        /// else has a camera that is not this group's to move. Opening moves no camera: there is
        /// nothing at a constellation's centre to fly to, and the group's own children are what opening
        /// it is for.
        /// </summary>
        private void AddConstellation(
            GraphBuilder builder,
            SkyGroup group,
            Empire empire,
            StarSystemLabel[] labels,
            ConstellationLabel[] regions
        )
        {
            Constellation it = group.Constellation;
            ConstellationLabel drawn = LabelFor(it, regions);
            AgeTooltip tooltip = drawn == null ? null : drawn.ConstellationTooltip;
            NodeVtable vtable = GraphNodes.Group(() => it.LocalizedName, tooltip: tooltip);
            AgeTooltip tip = tooltip;
            ConstellationLabel showing = drawn;
            vtable.OnFocusVisual = () =>
            {
                // The label the name and the dossier both live on is one the map CULLS at every
                // camera position the game is played at, and a hidden label draws no tooltip - so it
                // is held drawn for as long as the cursor stands here, and given back to the game the
                // moment it leaves (<see cref="ConstellationLabelHold"/>).
                ConstellationLabelHold.Hold(showing);
                if (tip != null)
                {
                    PointerFocus.MoveTo(null, tip, tip.AgeTransform);
                }
            };
            vtable.OnBlurVisual = ReleaseConstellation;

            ControlId id = ConstellationId(it);
            HashSet<ControlId> expansion = builder.Expansion;
            ControlId closing = id;
            Constellation leaving = it;
            vtable.OnCollapse = () =>
            {
                if (expansion != null)
                {
                    expansion.Remove(closing);
                }

                ZoomOutOf(leaving);
            };

            Seed(builder, id);
            // From the two furthest-out steps the map names the stretches of sky and draws no system
            // at all, so the group has nothing to hold and stands CLOSED - the row is still the map's
            // own organizing concept, and closed is what a group with nothing in it should sound like
            // rather than an opened branch that answers with silence.
            bool open = Open(builder, id);
            // Synthetic: a constellation is a place the mod assembled from the galaxy's own model - nothing on the map is drawn as one.
            builder.BeginGroup(Nodes.Synthetic(id, vtable), expanded: open);
            if (open)
            {
                List<StarSystemNode> members = _members[group.Members];
                for (int i = 0; i < members.Count; i++)
                {
                    // A bookmarked point of sky in this stretch is one of its entries and reads in
                    // the same order they do (<see cref="EmitBookmarksBefore"/>).
                    EmitBookmarksBefore(builder, it, members[i].GalaxyPosition);
                    AddPlace(builder, members[i], empire, labels);
                }

                EmitBookmarksAfter(builder, it);
            }

            builder.EndGroup();
        }

        /// <summary>
        /// Everything standing where the map has drawn no constellation name, in one group.
        ///
        /// One group and not one per constellation: the game DOES know which unexplored constellation
        /// each of these stands in, and saying so - even as five nameless buckets - would tell the
        /// player how the unseen half of the galaxy is divided up, which the picture does not. The
        /// caption is the mod's own for the same reason: there is no game text for a region the game is
        /// not naming.
        ///
        /// Last in the stop. It is the one entry with no position of its own - its members are
        /// scattered over the whole map - so there is no honest place for it in a walk sorted by
        /// position, and the end is where a group that is really "everything else" belongs.
        ///
        /// No tooltip, and expanding or closing it moves no camera: it stands for no place, so there is
        /// nowhere for a camera to go.
        /// </summary>
        private void AddUnexplored(GraphBuilder builder, Empire empire, StarSystemLabel[] labels)
        {
            if (_unexplored.Count == 0)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Group(
                () => ModStrings.Get(ModStrings.GalaxyConstellationUnexplored)
            );
            ControlId id = ControlId.Structural(UnexploredKey);
            Seed(builder, id);
            bool open = Open(builder, id);
            // Synthetic: the same, for the places the empire has not explored.
            builder.BeginGroup(Nodes.Synthetic(id, vtable), expanded: open);
            if (open)
            {
                for (int i = 0; i < _unexplored.Count; i++)
                {
                    AddPlace(builder, _unexplored[i], empire, labels);
                }
            }

            builder.EndGroup();
        }

        /// <summary>
        /// Open a group the first time this session ever declares it, and never again.
        ///
        /// A tree of constellations that arrived closed would put a level between the player and every
        /// system they used to walk straight into, which is a change to how the map READS rather than a
        /// change to what it holds. Open is therefore the inert default: the walk the player had is the
        /// walk they still have, and closing a constellation they are done with is something they can
        /// now choose. Once they have chosen, the choice is theirs - the seed never fires twice for the
        /// same group, so a group the player closed stays closed.
        ///
        /// Keyed on the structural key rather than the id so the record survives the id being rebuilt
        /// each frame, and cleared with the trail when the galaxy changes.
        /// </summary>
        private void Seed(GraphBuilder builder, ControlId id)
        {
            // Never off a search build: that build has everything open by construction
            // (<see cref="GraphBuilder.ExpandAll"/>) and it must not be what decides the tree the
            // player then walks - spending the once-ever seed there would leave a group they have
            // never seen already open.
            HashSet<ControlId> expansion = builder.ExpandAll ? null : builder.Expansion;
            if (expansion == null || !_seeded.Add(id.StructuralKey))
            {
                return;
            }

            expansion.Add(id);
        }

        /// <summary>Whether a stretch of sky opens onto anything on this build: the player's own
        /// expansion, and only from the band at which the map draws the systems inside it. A search
        /// build looks through everything, band or no band, because a search is the player asking what
        /// exists rather than what is on the screen.</summary>
        private bool Open(GraphBuilder builder, ControlId id)
        {
            return builder.ExpandAll || (_showsSystems && builder.IsExpanded(id));
        }

        /// <summary>The groups this session has already offered a starting state to.</summary>
        private readonly HashSet<object> _seeded = new HashSet<object>();

        /// <summary>Put the camera back out at the default view when a constellation's branch is
        /// closed - but only while it is a system of THIS constellation the camera is in on, which is
        /// the same test closing a system makes (<see cref="Collapse"/>) one level up. The way out is
        /// the system's own, so the camera lands exactly where collapsing that system would have put
        /// it, and a camera already out moves not at all.</summary>
        private void ZoomOutOf(Constellation constellation)
        {
            StarSystemNode inside = GalaxyViewLevels.FocusedSystem;
            if (inside != null && ReferenceEquals(inside.Constellation, constellation))
            {
                ZoomOut(inside);
                LeftPlace(inside);
            }
        }

        /// <summary>The map's own label for a constellation - matched by the constellation it was bound
        /// to, with the entity's identity as the fallback the system labels use for the same reason.
        /// </summary>
        private static ConstellationLabel LabelFor(
            Constellation constellation,
            ConstellationLabel[] labels
        )
        {
            try
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    if (ReferenceEquals(labels[i].Constellation, constellation))
                    {
                        return labels[i];
                    }
                }

                for (int i = 0; i < labels.Length; i++)
                {
                    Constellation candidate = labels[i].Constellation;
                    if (candidate != null && candidate.GUID == constellation.GUID)
                    {
                        return labels[i];
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: matching a constellation to its map label threw: " + e);
            }

            return null;
        }

        private static readonly ConstellationLabel[] NoConstellationLabels =
            new ConstellationLabel[0];

        /// <summary>Every constellation label the window is holding, fetched fresh for the same reason
        /// the system labels are: the window instantiates one per constellation as the game meets
        /// them.</summary>
        private static ConstellationLabel[] ConstellationLabels()
        {
            try
            {
                ConstellationLabelsWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<ConstellationLabelsWindow>(false)
                    : null;
                return window == null
                    ? NoConstellationLabels
                    : window.GetComponentsInChildren<ConstellationLabel>(true);
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: finding the constellation labels threw: " + e);
                return NoConstellationLabels;
            }
        }
    }
}
