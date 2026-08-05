using System.Collections.Generic;

namespace ES2Access.Core.Speech
{
    /// <summary>
    /// A panel read out as one sentence: the fields it is showing, in the order it draws them,
    /// separated the way a list is.
    ///
    /// The fields are collected by the caller and passed in already in drawn order, blanks and all -
    /// which is what lets the caller ask each field the same question ("what does this widget say")
    /// without deciding, field by field, whether the answer is worth appending. A field the panel is
    /// not showing comes back empty and is dropped here.
    ///
    /// Nothing at all comes back as NULL rather than as an empty string, and the distinction is load
    /// bearing for a passive announcer: nothing to say means the panel has not been filled in yet, so
    /// the announcer must leave its watermark uncommitted and ask again next frame rather than
    /// recording a planet it never described.
    /// </summary>
    public static class FieldReadout
    {
        public static string Compose(IList<string> fields)
        {
            if (fields == null)
            {
                return null;
            }

            MessageBuilder message = new MessageBuilder();
            for (int i = 0; i < fields.Count; i++)
            {
                // Trimmed, not merely tested for empty: a label the game has blanked keeps the
                // spacing of the text it used to hold, and a list item of one space reads as a
                // stumble between two real ones.
                string field = fields[i] == null ? null : fields[i].Trim();
                if (!string.IsNullOrEmpty(field))
                {
                    message.ListItem(field);
                }
            }

            return message.Build();
        }
    }
}
