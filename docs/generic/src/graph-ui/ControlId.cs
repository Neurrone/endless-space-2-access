using System;

namespace ES2Access.Core.UI.Graph
{
    /// <summary>
    /// The identity of a control (graph node) — a two-tier identity so focus can be followed across
    /// rebuilds even when the world shifts under us. Ported from Tanglebeep (with permission), which
    /// upgraded Factorio Access's plain string node key.
    ///
    /// <para><b>Reference</b> (optional) is the game/domain object a node was derived from (a view model,
    /// a widget, an item), compared by reference identity. <b>StructuralKey</b> (always present) is a
    /// value-equatable key — a string, or a composite such as a (region, row, col) key.</para>
    ///
    /// <para>Two controls are "the same" when their references are identical (tier 1 — a perfect match
    /// that follows an object that MOVED, its structural key changing) OR their structural keys are equal
    /// (tier 2 — follows a logical control whose backing object was rebuilt: new instance, same identity).</para>
    ///
    /// <para>Equality/hashing is defined on <see cref="StructuralKey"/> alone, so it is a stable
    /// dictionary key (the graph stores nodes and traversal order by it). The reference tier is metadata,
    /// applied explicitly during focus reconciliation via <see cref="ReferenceMatches"/>.</para>
    /// </summary>
    public sealed class ControlId : IEquatable<ControlId>
    {
        /// <summary>The originating game/domain object, or null. Matched by reference identity.</summary>
        public object Reference { get; private set; }

        /// <summary>The value-equatable structural identity. Never null.</summary>
        public object StructuralKey { get; private set; }

        private ControlId(object reference, object structuralKey)
        {
            if (structuralKey == null) throw new ArgumentNullException("structuralKey");
            Reference = reference;
            StructuralKey = structuralKey;
        }

        /// <summary>A control identified only by a structural key (no backing object).</summary>
        public static ControlId Structural(object structuralKey)
        {
            return new ControlId(null, structuralKey);
        }

        /// <summary>A control with both tiers: a backing object and a structural key.</summary>
        public static ControlId Referenced(object reference, object structuralKey)
        {
            return new ControlId(reference, structuralKey);
        }

        /// <summary>A control identified by a backing object only — the object doubles as the structural
        /// key (equality collapses to identity). For wrapping a raw widget with no better key.</summary>
        public static ControlId ForObject(object reference)
        {
            if (reference == null) throw new ArgumentNullException("reference");
            return new ControlId(reference, reference);
        }

        /// <summary>Tier-1 test: is <paramref name="obj"/> this control's backing object?</summary>
        public bool ReferenceMatches(object obj)
        {
            return Reference != null && ReferenceEquals(Reference, obj);
        }

        public bool Equals(ControlId other)
        {
            return other != null && Equals(StructuralKey, other.StructuralKey);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ControlId);
        }

        public override int GetHashCode()
        {
            return StructuralKey.GetHashCode();
        }

        public override string ToString()
        {
            return Reference == null
                ? "ControlId(" + StructuralKey + ")"
                : "ControlId(" + StructuralKey + ", ref=" + Reference + ")";
        }
    }
}
