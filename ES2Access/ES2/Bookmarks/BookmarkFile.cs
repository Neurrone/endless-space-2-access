using System;
using ES2Access.Core.Settings;
using ES2Access.Core.Util;

namespace ES2Access.ES2.Bookmarks
{
    /// <summary>
    /// WHICH FILE a campaign's bookmarks belong in, and how a file says whose it is.
    ///
    /// The name carries both facts a reader needs: the campaign's GUID, which identifies it, and
    /// the faction in front of it, which is there so a folder of these files can be read by a
    /// person. The same two facts are written INSIDE the file as well
    /// (<see cref="Identify"/>), because a file that has changed hands arrives as text - pasted
    /// into a chat window, with whatever name the receiver's machine would have given it - and text
    /// that cannot say which campaign it belongs to cannot be filed.
    ///
    /// The rule lives here, away from the running game, so that the store that writes the file and
    /// the import that reads somebody else's both work the name out the same way
    /// (owner ruling 2026-09-17).
    /// </summary>
    public static class BookmarkFile
    {
        /// <summary>The campaign's GUID, in the "N" form the file's own name carries.</summary>
        public const string CampaignKey = "campaign";

        /// <summary>The faction part of the file's name - empty for a campaign whose faction gives
        /// the name nothing that may go in one.</summary>
        public const string FactionKey = "faction";

        /// <summary>What a bookmarks file is called at the end.</summary>
        public const string Extension = ".cfg";

        /// <summary>How much of the faction's internal name the file's own name may carry. Long
        /// enough for any name a person would recognise it by, short enough that the whole path
        /// stays comfortable beside a plugin directory that is already deep.</summary>
        public const int FactionNameLimit = 48;

        /// <summary>How long a campaign's GUID is, written without its dashes.</summary>
        private const int CampaignLength = 32;

        /// <summary>
        /// The file name this campaign's bookmarks go under, or null where the campaign is not one
        /// - which is how text that is not a bookmarks file is found out.
        ///
        /// The faction is put through <see cref="FileNameText.Safe"/> here rather than trusted,
        /// because on the import's side it is whatever somebody else's file said, and a name is the
        /// one part of a path this mod composes from text it did not write.
        /// </summary>
        public static string NameOf(string campaign, string faction)
        {
            string identity = CampaignOrNull(campaign);
            if (identity == null)
            {
                return null;
            }

            string part = FileNameText.Safe(faction ?? string.Empty, FactionNameLimit);
            return (part.Length == 0 ? identity : part + "-" + identity) + Extension;
        }

        /// <summary>The campaign a file says it belongs to, or null where it says nothing a campaign
        /// could be named by. Never throws: the text may be anything at all.</summary>
        public static string CampaignOf(SettingsFile file)
        {
            return file == null ? null : CampaignOrNull(file.Get(CampaignKey));
        }

        /// <summary>The faction part a file records, or the empty string - which is also what a file
        /// written before this key existed answers.</summary>
        public static string FactionOf(SettingsFile file)
        {
            string faction = file == null ? null : file.Get(FactionKey);
            return faction ?? string.Empty;
        }

        /// <summary>Write into the file the two things its NAME says, so that the text alone is
        /// enough to file it. Stamped on every write, like the header comment beside it.</summary>
        public static void Identify(SettingsFile file, string campaign, string faction)
        {
            if (file == null)
            {
                return;
            }

            string identity = CampaignOrNull(campaign);
            if (identity == null)
            {
                return;
            }

            file.Set(CampaignKey, identity);
            file.Set(FactionKey, faction ?? string.Empty);
        }

        /// <summary>A campaign GUID as it is written and compared - thirty-two hexadecimal digits in
        /// lower case, which is what <c>Guid.ToString("N")</c> answers - or null for anything else.
        /// </summary>
        public static string CampaignOrNull(string text)
        {
            if (text == null)
            {
                return null;
            }

            string trimmed = text.Trim();
            if (trimmed.Length != CampaignLength)
            {
                return null;
            }

            char[] lowered = new char[CampaignLength];
            for (int i = 0; i < CampaignLength; i++)
            {
                char c = trimmed[i];
                if (c >= '0' && c <= '9')
                {
                    lowered[i] = c;
                }
                else if (c >= 'a' && c <= 'f')
                {
                    lowered[i] = c;
                }
                else if (c >= 'A' && c <= 'F')
                {
                    lowered[i] = char.ToLowerInvariant(c);
                }
                else
                {
                    return null;
                }
            }

            return new string(lowered);
        }
    }
}
