using System;
using System.Collections.Generic;
using System.IO;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using Newtonsoft.Json;

// The engine has its own Amplitude.Unity.Framework.Path.
using Path = System.IO.Path;

namespace ES2Access.Localization
{
    /// <summary>
    /// Engine side of the mod's audio descriptions: it feeds <see cref="VideoDescriptions"/> the
    /// table from <c>BepInEx\plugins\ES2Access\descriptions\&lt;language&gt;.json</c>, built from
    /// the authored files by <c>build-descriptions.ps1</c>.
    ///
    /// It follows the game's language the same way translations do, and for the same reason - a
    /// description is a spoken phrase the mod wrote, not game text - but it differs from
    /// <see cref="ModLocale"/> in one way: English is a file here rather than a fallback in the
    /// sources. There are no built-in descriptions to fall back to, so English is loaded like any
    /// other language, and a language with no descriptions of its own borrows English rather than
    /// going silent. That mirrors what a missing translation does with the mod's own strings.
    ///
    /// The cues are trusted to be in the order they are spoken. The generator refuses to write a
    /// track whose cues run backwards, so nothing sorts them again per playback.
    /// </summary>
    public static class ModDescriptions
    {
        private const string DescriptionsFolder = "descriptions";
        private const string FallbackLanguage = "english";

        /// <summary>Follow <paramref name="language"/>, falling back to English. Called from
        /// <see cref="ModLocale"/> whenever the game's language resolves or changes, so there is
        /// one language watcher rather than two.</summary>
        public static void Install(string pluginDirectory, string language)
        {
            string path = TablePath(pluginDirectory, language);
            if (path == null)
            {
                return;
            }

            if (!File.Exists(path))
            {
                string fallback = TablePath(pluginDirectory, FallbackLanguage);
                if (fallback == null || !File.Exists(fallback))
                {
                    Log.Info("descriptions: none for language '" + language + "'; cutscenes go undescribed");
                    VideoDescriptions.Reset();
                    return;
                }

                Log.Info(
                    "descriptions: none for language '" + language + "', using the English ones"
                );
                path = fallback;
            }

            Dictionary<string, DescriptionCue[]> table;
            try
            {
                table = Read(path);
            }
            catch (Exception e)
            {
                // Kept rather than cleared: a table that failed to re-read on a language change is
                // no reason to lose the one already describing videos.
                Log.Warn("descriptions: could not read " + path + ": " + e.Message);
                return;
            }

            Log.Info(
                "descriptions: loaded " + table.Count + " videos for language '" + language + "'"
            );
            VideoDescriptions.Install(table);
        }

        public static void Reset()
        {
            VideoDescriptions.Reset();
        }

        private static string TablePath(string pluginDirectory, string language)
        {
            if (string.IsNullOrEmpty(pluginDirectory))
            {
                Log.Warn("descriptions: the plugin directory is not known; cutscenes go undescribed");
                return null;
            }

            return Path.Combine(
                Path.Combine(pluginDirectory, DescriptionsFolder),
                language + ".json"
            );
        }

        // Read with the streaming reader only, for the reason ModLocale.Read gives: the game ships
        // an old Newtonsoft whose serializer and LINQ-to-JSON surfaces have moved around between
        // versions, while JsonTextReader has not. Nesting is counted here rather than taken from
        // the reader's own Depth, which is one more thing that could have moved.
        private static Dictionary<string, DescriptionCue[]> Read(string path)
        {
            Dictionary<string, DescriptionCue[]> table = new Dictionary<string, DescriptionCue[]>();
            using (StreamReader text = new StreamReader(path))
            using (JsonTextReader json = new JsonTextReader(text))
            {
                int depth = 0;
                string movie = null;
                string field = null;
                List<DescriptionCue> cues = null;
                float at = 0f;
                string line = null;

                while (json.Read())
                {
                    switch (json.TokenType)
                    {
                        case JsonToken.StartArray:
                            depth++;
                            if (depth == 2)
                            {
                                cues = new List<DescriptionCue>();
                            }

                            break;

                        case JsonToken.EndArray:
                            if (depth == 2 && movie != null && cues != null)
                            {
                                table[movie] = cues.ToArray();
                            }

                            depth--;
                            cues = null;
                            movie = null;
                            break;

                        case JsonToken.StartObject:
                            depth++;
                            if (depth == 3)
                            {
                                at = 0f;
                                line = null;
                            }

                            break;

                        case JsonToken.EndObject:
                            if (depth == 3 && cues != null && !string.IsNullOrEmpty(line))
                            {
                                cues.Add(new DescriptionCue(at, line));
                            }

                            depth--;
                            break;

                        case JsonToken.PropertyName:
                            if (depth == 1)
                            {
                                movie = json.Value as string;
                            }
                            else
                            {
                                field = json.Value as string;
                            }

                            break;

                        case JsonToken.Integer:
                        case JsonToken.Float:
                            if (field == "at")
                            {
                                at = Convert.ToSingle(json.Value);
                            }

                            break;

                        case JsonToken.String:
                            if (field == "text")
                            {
                                line = json.Value as string;
                            }

                            break;
                    }
                }
            }

            return table;
        }
    }
}
