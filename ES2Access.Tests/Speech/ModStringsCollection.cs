using Xunit;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// ModStrings is process-wide static state (it is main-thread-only by design), so every test
    /// that installs a translation joins this collection: xUnit never runs two classes of the same
    /// collection in parallel.
    /// </summary>
    [CollectionDefinition(Name)]
    public sealed class ModStringsCollection
    {
        public const string Name = "ModStrings";
    }
}
