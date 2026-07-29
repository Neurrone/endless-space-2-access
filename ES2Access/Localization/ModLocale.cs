using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Amplitude.Unity.Framework;
using Amplitude.Unity.Localization;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;
using Newtonsoft.Json;

// The engine has its own Amplitude.Unity.Framework.Path.
using Path = System.IO.Path;

namespace ES2Access.Localization
{
    /// <summary>
    /// Engine side of the mod's own translations: it follows the game's language and feeds
    /// <see cref="ModStrings"/> the matching table from
    /// <c>BepInEx\plugins\ES2Access\locale\&lt;language&gt;.json</c>. Language names are the
    /// Steam-style ones the game itself uses ("english", "french", "schinese"), so a translator
    /// never has to map culture codes.
    ///
    /// The language is not known at plugin load: the game's localization manager registers its
    /// service only after its own load coroutine has read the language from Steam. So this polls
    /// once per frame from Plugin.Update until the service appears, then keeps watching the
    /// language cheaply so an in-game language change is picked up.
    ///
    /// A missing translation file is normal (English needs none); a malformed one is logged and
    /// the current strings are kept. Neither ever throws into the game's frame.
    /// </summary>
    public static class ModLocale
    {
        private const string LocaleFolder = "locale";

        private static string _language;

        /// <summary>True once the game's language has been read and its table installed.</summary>
        public static bool LanguageResolved { get; private set; }

        /// <summary>Poll the game for its language. Cheap; call every frame.</summary>
        public static void Tick()
        {
            ILocalizationService service = LocalizationService();
            if (service == null)
            {
                return;
            }

            string language = service.CurrentLanguage;
            if (string.IsNullOrEmpty(language))
            {
                return;
            }

            if (LanguageResolved && language == _language)
            {
                return;
            }

            _language = language;
            LanguageResolved = true;
            Install(language);
        }

        /// <summary>Forget the resolved language so a reloaded plugin resolves it again.</summary>
        public static void Reset()
        {
            _language = null;
            LanguageResolved = false;
        }

        private static ILocalizationService LocalizationService()
        {
            try
            {
                return Services.GetService<ILocalizationService>();
            }
            catch (Exception)
            {
                // The service registry is torn down with the game; a shutdown frame is not a
                // reason to log every frame.
                return null;
            }
        }

        private static void Install(string language)
        {
            string path = LocalePath(language);
            if (path == null)
            {
                return;
            }

            if (!File.Exists(path))
            {
                Log.Info(
                    "locale: no translation for language '"
                        + language
                        + "', using built-in English strings"
                );
                ModStrings.Install(null);
                return;
            }

            Dictionary<string, string> table;
            try
            {
                table = Read(path);
            }
            catch (Exception e)
            {
                Log.Warn("locale: could not read " + path + ": " + e.Message);
                return;
            }

            Log.Info("locale: loaded " + table.Count + " strings for language '" + language + "'");
            ModStrings.Install(table);
        }

        private static string LocalePath(string language)
        {
            string pluginDirectory = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location
            );
            if (string.IsNullOrEmpty(pluginDirectory))
            {
                Log.Warn("locale: cannot locate the plugin directory; translations disabled");
                return null;
            }

            return Path.Combine(
                Path.Combine(pluginDirectory, LocaleFolder),
                language + ".json"
            );
        }

        // Flat string -> string JSON, read with the streaming reader only: the game ships an old
        // Newtonsoft and the serializer/LINQ-to-JSON surfaces have moved around between versions,
        // while JsonTextReader has not. Non-string values are skipped rather than rejected, so one
        // odd entry does not cost a translator the whole file.
        private static Dictionary<string, string> Read(string path)
        {
            Dictionary<string, string> table = new Dictionary<string, string>();
            using (StreamReader text = new StreamReader(path))
            using (JsonTextReader json = new JsonTextReader(text))
            {
                string key = null;
                while (json.Read())
                {
                    if (json.TokenType == JsonToken.PropertyName && json.Depth == 1)
                    {
                        key = json.Value as string;
                    }
                    else if (json.TokenType == JsonToken.String && key != null)
                    {
                        table[key] = (string)json.Value;
                        key = null;
                    }
                    else
                    {
                        key = null;
                    }
                }
            }

            return table;
        }
    }
}
