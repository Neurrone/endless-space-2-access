using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ES2Access.Tests.Speech
{
    /// <summary>
    /// Where the shipped translation files are and how to read them, shared by every test that
    /// lints one.
    ///
    /// The tests run from bin/, so the folders are found by walking up to the repository rather
    /// than by anything being copied next to the test assembly - the files under test are the
    /// SOURCES, which is what a translator edits and what a release is built from.
    ///
    /// sources/ is a subfolder on purpose. Both the build's copy step and build_release.ps1 take
    /// locale\*.json and descriptions\*.json without recursing, so the snapshots the staleness
    /// check reads never reach a player's install and cost the download nothing.
    /// </summary>
    public static class TranslationFiles
    {
        public const string SourcesFolder = "sources";
        public const string English = "english";

        /// <summary>The languages Endless Space 2 ships in, under the game's own names, which are
        /// also the names of the files.</summary>
        public static readonly string[] Languages =
        {
            "english",
            "brazilian",
            "french",
            "german",
            "koreana",
            "polish",
            "russian",
            "schinese",
            "spanish",
            "tchinese",
        };

        public static string RepoRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "ES2Access", "locale")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "no ES2Access\\locale folder above " + AppContext.BaseDirectory
            );
        }

        public static string LocaleDirectory()
        {
            return Path.Combine(RepoRoot(), "ES2Access", "locale");
        }

        public static string DescriptionsDirectory()
        {
            return Path.Combine(RepoRoot(), "ES2Access", "descriptions");
        }

        /// <summary>The shipped translation files, newest state on disk - never the snapshots, which
        /// live one folder down and are not translations.</summary>
        public static IEnumerable<string> LocaleFileNames()
        {
            foreach (string file in Directory.GetFiles(LocaleDirectory(), "*.json"))
            {
                yield return Path.GetFileName(file);
            }
        }

        public static IEnumerable<string> DescriptionFileNames()
        {
            foreach (string file in Directory.GetFiles(DescriptionsDirectory(), "*.json"))
            {
                yield return Path.GetFileName(file);
            }
        }

        public static string LanguageOf(string fileName)
        {
            return Path.GetFileNameWithoutExtension(fileName);
        }

        public static string SnapshotPath(string directory, string fileName)
        {
            return Path.Combine(Path.Combine(directory, SourcesFolder), fileName);
        }

        /// <summary>The script a language writes in, where that is something a machine can check.
        /// Every Latin-script language answers <see cref="NativeScript.None"/>.</summary>
        public static NativeScript ScriptFor(string language)
        {
            switch (language)
            {
                case "russian":
                    return NativeScript.Cyrillic;
                case "koreana":
                    return NativeScript.Hangul;
                case "schinese":
                case "tchinese":
                    return NativeScript.Han;
                default:
                    return NativeScript.None;
            }
        }

        /// <summary>Whether the language has a paucal form, and so owes the locale file a
        /// <c>.few</c> key for every plural pair.</summary>
        public static bool HasPaucal(string language)
        {
            return PluralFormsOf(language) == 3;
        }

        private static int PluralFormsOf(string language)
        {
            return language == "polish" || language == "russian" ? 3 : 2;
        }

        /// <summary>A flat key-to-text table: a translation, or a translation's English snapshot.</summary>
        public static Dictionary<string, string> ReadTable(string path)
        {
            Dictionary<string, string> table = new Dictionary<string, string>();
            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(path)))
            {
                foreach (JsonProperty entry in document.RootElement.EnumerateObject())
                {
                    table[entry.Name] = entry.Value.ValueKind == JsonValueKind.String
                        ? entry.Value.GetString()
                        : entry.Value.ToString();
                }
            }

            return table;
        }

        /// <summary>A cutscene description table, video to cues.</summary>
        public static Dictionary<string, IList<CueRow>> ReadDescriptions(string path)
        {
            Dictionary<string, IList<CueRow>> table = new Dictionary<string, IList<CueRow>>();
            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(path)))
            {
                foreach (JsonProperty movie in document.RootElement.EnumerateObject())
                {
                    List<CueRow> cues = new List<CueRow>();
                    foreach (JsonElement cue in movie.Value.EnumerateArray())
                    {
                        cues.Add(
                            new CueRow
                            {
                                At = Seconds(cue, "at"),
                                End = Seconds(cue, "end"),
                                Text = Text(cue, "text"),
                            }
                        );
                    }

                    table[movie.Name] = cues;
                }
            }

            return table;
        }

        /// <summary>A description snapshot: video to the English cue texts it was written from.</summary>
        public static Dictionary<string, IList<string>> ReadDescriptionSnapshot(string path)
        {
            Dictionary<string, IList<string>> table = new Dictionary<string, IList<string>>();
            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(path)))
            {
                foreach (JsonProperty movie in document.RootElement.EnumerateObject())
                {
                    List<string> texts = new List<string>();
                    foreach (JsonElement text in movie.Value.EnumerateArray())
                    {
                        texts.Add(text.GetString());
                    }

                    table[movie.Name] = texts;
                }
            }

            return table;
        }

        /// <summary>The offenders as one failure message, truncated so a wholly untranslated file
        /// reports a diagnosis rather than a wall.</summary>
        public static string Report(string fileName, IList<string> problems)
        {
            StringBuilder message = new StringBuilder();
            message.Append(fileName).Append(": ").Append(problems.Count).Append(" problem(s)");
            int shown = Math.Min(problems.Count, 40);
            for (int i = 0; i < shown; i++)
            {
                message.Append(Environment.NewLine).Append("  ").Append(problems[i]);
            }

            if (shown < problems.Count)
            {
                message
                    .Append(Environment.NewLine)
                    .Append("  ... and ")
                    .Append(problems.Count - shown)
                    .Append(" more");
            }

            return message.ToString();
        }

        private static double Seconds(JsonElement cue, string field)
        {
            JsonElement value;
            return cue.TryGetProperty(field, out value) && value.ValueKind == JsonValueKind.Number
                ? value.GetDouble()
                : double.NaN;
        }

        private static string Text(JsonElement cue, string field)
        {
            JsonElement value;
            return cue.TryGetProperty(field, out value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
    }
}
