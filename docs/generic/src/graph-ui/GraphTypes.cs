using System;
using System.Collections.Generic;

namespace ES2Access.Core.UI.Graph
{
    /// <summary>The four navigable directions between graph nodes (explicit edges). Tab-stop cycling and
    /// region jumps are OPERATIONS over node metadata (<see cref="GraphNode.StopKey"/> /
    /// <see cref="GraphNode.RegionKey"/>), not edges — they carry per-stop remembered positions, which a
    /// static edge can't express.</summary>
    public enum GraphDir
    {
        Up,
        Right,
        Down,
        Left,
    }

    /// <summary>The well-known announcement-part kinds. A part's kind is its identity for control-type
    /// ordering, node-over-type overriding, and the user's per-kind announcement settings.</summary>
    public static class AnnouncementKinds
    {
        public const string Label = "label";
        public const string Role = "role";
        public const string Value = "value";
        public const string Selected = "selected";
        public const string Enabled = "enabled";
        public const string Tooltip = "tooltip";
        public const string Position = "position";
    }

    /// <summary>
    /// One part of a control's spoken focus readout ("Hold position" / "toggle" / "on"), resolved live at
    /// speak time. A LIVE part is additionally watched while its node is focused: when its resolved text
    /// changes (an async toggle settling, a value the game flips), the navigator speaks just that part
    /// immediately — state feedback without re-reading the whole control, and without per-element watcher
    /// machinery.
    /// </summary>
    public sealed class NodeAnnouncement
    {
        /// <summary>The part's text, resolved live. Null/empty at speak time = the part stays silent.</summary>
        public Func<string> Text;

        /// <summary>Watch this part while the node is focused and speak it when its value changes.</summary>
        public bool Live;

        /// <summary>The part's kind (<see cref="AnnouncementKinds"/>), or null for a custom one-off part.
        /// Kinds drive the control type's speak order, let a node's part override the type's common part
        /// of the same kind, and key the user's per-kind announcement settings.</summary>
        public string Kind;

        public NodeAnnouncement(Func<string> text, bool live = false, string kind = null)
        {
            Text = text;
            Live = live;
            Kind = kind;
        }

        public static NodeAnnouncement Static(string text)
        {
            return new NodeAnnouncement(() => text);
        }
    }

    /// <summary>
    /// A CONTROL TYPE — "button", "toggle", "slider" — as a registry VALUE rather than a C# class. Deriving
    /// type identity from proxy/wrapper classes forces attribute unions and class collapsing whenever two
    /// widgets should share one settings identity; a value lets a node factory just point at the type. A
    /// type owns the speak ORDER of its announcement kinds and the parts COMMON to every control of the
    /// type (the localized role word); nodes contribute their specific parts, overriding a common part of
    /// the same kind. The user's per-type announcement settings key off <see cref="Key"/>.
    /// </summary>
    public sealed class ControlType
    {
        /// <summary>Stable settings/registry key ("button", "toggle", "slider").</summary>
        public string Key;

        /// <summary>The announcement kinds in speak order; parts with unknown/absent kinds append after,
        /// in declaration order.</summary>
        public string[] Order;

        /// <summary>The parts every control of this type shares (the role word), resolved per compose.
        /// Null = none.</summary>
        public Func<IList<NodeAnnouncement>> Common;
    }

    /// <summary>
    /// One block of a control's readable content, declared ONCE and surfaced everywhere it belongs.
    ///
    /// A control's content reaches the player through two channels — the focus readout and the review
    /// buffer — and wiring them separately is how a row comes to announce a tooltip it cannot review, or
    /// to review one it never mentions. It happened three times on the new-game screens before this type
    /// existed. So a section says WHAT the lines are and HOW LOUD they should be, and the engine derives
    /// both surfaces from that one declaration: every section feeds the buffer, in declared order, and
    /// <see cref="Mode"/> alone decides what (if anything) the focus readout says about it.
    ///
    /// Sections are ordered as the screen draws them (a row's heading tooltip before its value's), which
    /// is the order the buffer reads them in.
    /// </summary>
    public sealed class NodeSection
    {
        /// <summary>The block's lines, resolved live at read time — a refusing button's reason has to be
        /// the one it would give now. Null or an empty list = the section contributes nothing.</summary>
        public Func<IList<string>> Lines;

        /// <summary>How the section reaches the focus readout. <see cref="TooltipMode.None"/> is a
        /// buffer-only section: content the control DRAWS (a planet card's output rows, a chart's
        /// series), never announced and never indicated, because the readout already named the control
        /// and the substance is there to be walked.</summary>
        public TooltipMode Mode;

        public NodeSection(Func<IList<string>> lines, TooltipMode mode)
        {
            Lines = lines;
            Mode = mode;
        }

        /// <summary>Content the control draws: reviewable, never spoken on focus.</summary>
        public static NodeSection Buffer(Func<IList<string>> lines)
        {
            return lines == null ? null : new NodeSection(lines, TooltipMode.None);
        }
    }

    /// <summary>
    /// The behaviors of a control, as data. <see cref="Announcements"/> is required (its parts compose the
    /// spoken focus readout; the first part is the control's label for search/dedupe purposes); the rest
    /// are optional — a null slot means the control doesn't have that behavior and the navigator speaks
    /// its "nothing there" feedback instead.
    /// </summary>
    public sealed class NodeVtable
    {
        /// <summary>Required, at least one part. The control's spoken focus readout. Parts marked
        /// <see cref="NodeAnnouncement.Live"/> re-speak on change while focused. When
        /// <see cref="ControlType"/> is set, the type's common parts merge in and the type's kind order
        /// applies; otherwise parts speak in declaration order.
        ///
        /// A node's announcement-part list must keep its SHAPE across rebuilds: the live-part watch
        /// re-baselines when the list changes shape and swallows exactly the change it should have
        /// spoken - represent absent state as an empty part, never a missing one.</summary>
        public IList<NodeAnnouncement> Announcements;

        /// <summary>The control's type (registry value) — supplies the role word, the speak order, and the
        /// per-type announcement settings identity. Null = an untyped one-off.</summary>
        public ControlType ControlType;

        /// <summary>Optional. Primary activation — the left-click equivalent (Enter).</summary>
        public Action OnActivate;

        /// <summary>Optional. Secondary activation — the right-click equivalent.</summary>
        public Action OnSecondary;

        /// <summary>Optional. The control's OTHER activation — what the game's own modified click does
        /// (queue this at the head of the queue rather than the end). Distinct from
        /// <see cref="OnSecondary"/>, which is the right-click.</summary>
        public Action OnAlternate;

        /// <summary>Optional. The command the game puts on a RIGHT-CLICK here - the one thing the
        /// control does when the player asks it to do its obvious thing without opening anything.
        /// Distinct from <see cref="OnActivate"/> (the left click) and from <see cref="OnAlternate"/>
        /// (the modified left click); a control without one answers the key with a spoken cue rather
        /// than with silence.</summary>
        public Action OnContextual;

        /// <summary>Optional. The command the game puts on a DOUBLE click here - the second click
        /// inside its own double-click window, which several of this game's controls answer with a
        /// command of their own (a fleet row shows that fleet on the map, a picked choice is
        /// confirmed, a module tile fits itself). Distinct from <see cref="OnActivate"/> (the single
        /// click, which such a control may answer with nothing at all), from
        /// <see cref="OnAlternate"/> (the click with a modifier held) and from
        /// <see cref="OnContextual"/> (the right click).</summary>
        public Action OnDoubleClick;

        /// <summary>Optional. Add this control's item to the game's own selection, or take it out
        /// again, leaving the rest of the selection alone - what the game's Ctrl+click does.</summary>
        public Action OnSelectToggle;

        /// <summary>Optional. Extend the game's own selection from wherever it last was to here -
        /// what the game's Shift+click does.</summary>
        public Action OnSelectRange;

        /// <summary>Optional. What this control offers to PICK UP and carry (a ship out of a fleet,
        /// a population unit off a planet). Returning null means it has nothing to give right now.
        /// The carried thing's name is captured at that moment and never re-derived - see
        /// <see cref="CarryItem"/>. A PURE QUERY: the readout asks it speculatively to know whether to
        /// say "draggable" (<c>CarryState.DraggablePart</c>), so it must decide, not act.</summary>
        public Func<CarryItem> OnPickUp;

        /// <summary>Optional. Which kind of cargo this control will TAKE (<see cref="CarryItem.Kind"/>).
        /// Null takes nothing; the kind is what keeps a ship from being dropped into a population
        /// list.</summary>
        public string DropKind;

        /// <summary>Optional. Take the carried thing, through the GAME's own can-do check - never a
        /// rule the mod invented. A refusal carries the game's own words and leaves the player still
        /// holding it.</summary>
        public Func<CarryItem, DropResult> OnDrop;

        /// <summary>Optional. Whether this control would take THIS cargo right now - the screen's own
        /// test for the ones among a family of targets that will refuse (a locked deck slot beside three
        /// live ones, a hull slot the module does not fit). Asked for the spoken drop-target INDICATION
        /// only, so the word and the outcome cannot disagree; the drop itself still goes through
        /// <see cref="OnDrop"/>, whose refusal carries the game's own reason for a player who presses
        /// anyway. Null = <see cref="DropKind"/> alone answers.</summary>
        public Func<CarryItem, bool> DropAccepts;

        /// <summary>Optional. Read / open the control's tooltip. The action owns the whole behavior
        /// (speak, or open the drill-in tooltip reader), so the core stays game-agnostic.</summary>
        public Action OnTooltip;

        /// <summary>Optional. Everything the control has to say beyond its readout, as ordered
        /// <see cref="NodeSection"/>s — its tooltips (the heading's explanation, then the value's
        /// dossier) and whatever else it draws. ONE declaration: the review buffer reads them all in
        /// order under the control's name and state, and the focus readout's tooltip part is derived
        /// from their modes (<see cref="TooltipParts.Part(IList{NodeSection})"/>). Null = the control
        /// has nothing beyond its readout, which is a complete buffer in itself.</summary>
        public IList<NodeSection> Sections;

        /// <summary>Optional. Horizontal value adjust (a slider): sign is -1 (decrease) / +1 (increase),
        /// large requests a coarse step. When set, left/right do NOT navigate.</summary>
        public Action<int, bool> OnAdjust;

        /// <summary>Optional. The control's state line, spoken IMMEDIATELY (interrupting) after an
        /// activation/adjust that changes state — the synchronous feedback path for rapid key repeats.
        /// Asynchronous/game-driven changes ride the Live announcement watch instead.</summary>
        public Func<string> StateText;

        /// <summary>Optional. The text type-ahead matches against; null = the first announcement part
        /// (the label). (A cell whose label is a bare number can search as its row's name, etc.)</summary>
        public Func<string> SearchText;

        /// <summary>If true, type-ahead never matches this control.</summary>
        public bool ExcludeFromSearch;

        /// <summary>Which column of a tabular row this control is - 0 (the default) for the row's
        /// primary cell and for everything that is not in a table. Stamped by
        /// <see cref="GraphSheet"/>, and read by type-ahead: a row contributes ONE result, its
        /// primary, because every cell of it searches as the row's name.</summary>
        public int Column;

        /// <summary>Optional (Expandable groups): override HOW expansion state changes. When null the
        /// engine mutates the persistent expansion set (<see cref="GraphState.Expanded"/>); an adapter
        /// wires these to a retained game-side container's Expand/Collapse instead.</summary>
        public Action OnExpand;

        public Action OnCollapse;

        /// <summary>Set when this group's own announcements already include its expanded/collapsed state,
        /// so the announcer doesn't append it again.</summary>
        public bool SpeaksOwnExpansion;

        /// <summary>Set when this node's announcements already include its list position, so the announcer
        /// doesn't append the auto-stamped one.</summary>
        public bool SpeaksOwnPosition;

        /// <summary>
        /// Optional. Make the GAME look the way it would if the pointer were resting on this control —
        /// its hover highlight, a menu opening under it, the game's own tooltip. Nothing here is spoken
        /// or navigable; it exists so that someone watching the screen sees where the keyboard is, which
        /// is what makes a screen-reader player's turn followable by the people sitting next to them.
        ///
        /// Called by the navigator at the one place focus is committed, whatever moved it, and only when
        /// focus actually changes control. An adapter should treat it as a REQUEST recorded now and
        /// applied once per frame rather than as game calls made inline: focus is re-committed after a
        /// rebuild, and animations restarted mid-flight flicker.
        /// </summary>
        public Action OnFocusVisual;

        /// <summary>Optional. The other half of <see cref="OnFocusVisual"/>: focus has left this control.
        /// Called before the new control's OnFocusVisual, and also when the screen closes or the mod
        /// stops, so nothing is left looking hovered.</summary>
        public Action OnBlurVisual;
    }

    /// <summary>A directed edge to another node, with an optional spoken transition line (a "lane
    /// change" — e.g. crossing into a new column band). Kept as plain data; contextual announcements are
    /// composed from node metadata by the announcer, not per-edge closures (GC discipline).</summary>
    public sealed class Transition
    {
        public ControlId Destination;
        public string Label; // spoken only while crossing this edge; null = silent edge

        public Transition(ControlId destination, string label = null)
        {
            Destination = destination;
            Label = label;
        }
    }

    /// <summary>A control: identity, behaviors, directional transitions, and structural metadata (its
    /// parent chain, tab-stop and region membership, expandability).</summary>
    public sealed class GraphNode
    {
        public ControlId Id;
        public NodeVtable Vtable;
        public readonly Dictionary<GraphDir, Transition> Transitions = new Dictionary<GraphDir, Transition>();

        /// <summary>The node's structural parent within THIS render, or null at screen level. The parent
        /// chain IS the presentation hierarchy: the announcer prefix-diffs old/new chains by identity, so
        /// entering a group reads its levels outermost-first and descending from a group onto its own
        /// child re-announces nothing (the group is on the chain and is the from-node). A parent may be
        /// non-focusable pure structure (a labeled panel — <see cref="Focusable"/> false, never in
        /// Nodes/Order) or a real control (a tree group header).</summary>
        public GraphNode Parent;

        /// <summary>False for a pure-structure parent node (a labeled panel): it exists only on
        /// <see cref="Parent"/> chains for announcements — never navigable, never in Nodes/Order.</summary>
        public bool Focusable = true;

        /// <summary>This node is a group that can expand/collapse (a tree section header). The engine's
        /// tree operations (expand/collapse/descend/ascend) key off this.</summary>
        public bool Expandable;

        /// <summary>An <see cref="Expandable"/> group's state AT THIS RENDER (stamped by the builder from
        /// the persistent expansion set, or the explicit value the declarer passed).</summary>
        public bool Expanded;

        /// <summary>The Tab-stop this node belongs to. Nodes sharing a StopKey form one stop; Tab cycles
        /// stops in first-appearance order, landing on the stop's remembered position.</summary>
        public object StopKey;

        /// <summary>The region (within a stop) this node belongs to, or null. Ctrl+Up/Down jumps between
        /// regions in first-appearance order.</summary>
        public object RegionKey;

        /// <summary>Auto-stamped sibling position (1-based) and count, from the builder: menu-mode nodes
        /// grouped by (parent, stop) — "3 of 10" among the siblings arrows actually reach. 0 = none
        /// (raw/grid nodes, or a lone sibling outside an expandable group, which reads no position).</summary>
        public int PositionIndex;

        public int PositionCount;

        /// <summary>On a parent (context/group) node: its direct children get NO auto position — for
        /// log-like streams where "37 of 200" is noise.</summary>
        public bool SuppressChildPositions;
    }

    /// <summary>
    /// One built snapshot of a graph: the nodes (keyed by structural identity), their order of
    /// declaration, and where focus starts when there is no prior position. Rebuilt per operation and
    /// thrown away — live state belongs in the node callbacks, not here.
    /// </summary>
    public sealed class GraphRender
    {
        public ControlId StartKey;
        public readonly Dictionary<ControlId, GraphNode> Nodes = new Dictionary<ControlId, GraphNode>();

        /// <summary>Declaration order — drives stop/region cycling and type-ahead scan order.</summary>
        public readonly List<GraphNode> Order = new List<GraphNode>();

        public GraphNode NodeAt(ControlId key)
        {
            if (key == null) return null;
            GraphNode n;
            return Nodes.TryGetValue(key, out n) ? n : null;
        }
    }

    /// <summary>
    /// The persistent cursor for a graph — the only thing that survives between renders. Holds where
    /// focus is, the last computed traversal order (for closest-survivor recovery), per-stop remembered
    /// positions (so Tab returns to where you were in a stop), and a one-shot move request.
    /// </summary>
    public sealed class GraphState
    {
        /// <summary>The focused control's id (carries its Reference for tier-1 recovery). Null until first render.</summary>
        public ControlId CurKey;

        /// <summary>The down-right total order from the previous render. Null on first render.</summary>
        public List<ControlId> KeyOrder;

        /// <summary>If set, focus jumps here on the next render when present (consumed either way).</summary>
        public ControlId NextSuggestedMove;

        /// <summary>Remembered position per Tab-stop: where Tab lands when cycling back into a stop.</summary>
        public readonly Dictionary<object, ControlId> StopMemory = new Dictionary<object, ControlId>();

        /// <summary>The expanded groups (by id). The builder consults this for groups declared without an
        /// explicit state; the engine's expand/collapse operations mutate it. Screens hold NO expansion
        /// state of their own.</summary>
        public readonly HashSet<ControlId> Expanded = new HashSet<ControlId>();
    }
}
