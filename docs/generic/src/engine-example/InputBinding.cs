namespace ES2Access.UI.Input
{
    /// <summary>
    /// One physical trigger for an <see cref="InputAction"/>. Abstract so an action can later be
    /// bound to something other than a key chord (a controller button, a mouse gesture) without the
    /// action or the manager learning anything new.
    ///
    /// Bindings are immutable: a rebind constructs a new one rather than mutating this.
    /// </summary>
    public abstract class InputBinding
    {
        /// <summary>How the binding reads in help and settings text ("Ctrl+Up").</summary>
        public abstract string DisplayName { get; }

        /// <summary>The binding became active this frame.</summary>
        public abstract bool JustPressed();

        /// <summary>The binding is active right now (drives auto-repeat).</summary>
        public abstract bool Held();
    }
}
