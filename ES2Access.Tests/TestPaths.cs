using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ES2Access.Tests
{
    /// <summary>
    /// Where the repository is, asked once. The tests run out of <c>bin/</c>, so anything checked in
    /// has to be found by walking up rather than by being copied next to the test assembly — and a
    /// walk per caller means a walk per SENTINEL, which is how the suite ended up looking for four
    /// different files and disagreeing about where the root was. One sentinel lives here
    /// (<c>ES2Access/ES2Access.csproj</c>, the thing that makes this tree the mod's tree) and every
    /// other path is derived from it.
    /// </summary>
    public static class TestPaths
    {
        private static string _root;

        public static string RepoRoot()
        {
            if (_root != null)
            {
                return _root;
            }

            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ES2Access", "ES2Access.csproj")))
                {
                    _root = directory.FullName;
                    return _root;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "no ES2Access\\ES2Access.csproj above " + AppContext.BaseDirectory
            );
        }

        /// <summary>The shipped translation tables.</summary>
        public static string Locale()
        {
            return Path.Combine(RepoRoot(), "ES2Access", "locale");
        }

        /// <summary>english.json: the template every other translation is written from.</summary>
        public static string EnglishTemplate()
        {
            return Path.Combine(Locale(), "english.json");
        }

        /// <summary>The shipped video description scripts.</summary>
        public static string Descriptions()
        {
            return Path.Combine(RepoRoot(), "ES2Access", "descriptions");
        }

        /// <summary>The generic guide's source mirror — this repository's primary deliverable.</summary>
        public static string GenericSrc()
        {
            return Path.Combine(RepoRoot(), "docs", "generic", "src");
        }

        /// <summary>Every key a translation table carries. The question four different sweeps ask of
        /// english.json, each of them one way to spell "a phrase the mod speaks and the template does
        /// not is a phrase no translator is ever offered".</summary>
        public static SortedSet<string> ShippedKeys(string path)
        {
            SortedSet<string> keys = new SortedSet<string>(StringComparer.Ordinal);
            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(path)))
            {
                foreach (JsonProperty entry in document.RootElement.EnumerateObject())
                {
                    keys.Add(entry.Name);
                }
            }

            return keys;
        }
    }
}
