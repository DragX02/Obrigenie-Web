namespace Obrigenie.Models
{
    /// <summary>
    /// Represents a time-stamped note written by the user for a specific day and hour range.
    /// Notes are displayed in the single-day time grid and as small indicators in week/month views.
    /// They are persisted on the server and loaded via ApiService.
    /// </summary>
    public class Note
    {
        /// <summary>
        /// The server-assigned unique identifier for this note.
        /// A value of 0 indicates a new note that has not yet been saved.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The calendar date this note belongs to.
        /// Stored as UTC midnight to avoid timezone-shift issues during JSON serialisation.
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// The starting hour of the note (24-hour format, 0–23).
        /// Determines which time-grid row the note is placed in when viewed in single-day mode.
        /// </summary>
        public int Hour { get; set; }

        /// <summary>
        /// The ending hour of the note (exclusive upper bound, 24-hour format, 1–24).
        /// A value of 0 means the end hour has not been set; the UI then treats it as Hour + 1.
        /// </summary>
        public int EndHour { get; set; }

        /// <summary>
        /// The text content of the note. Maximum 2000 characters (enforced by the UI textarea).
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// The UTC timestamp when this note was first created on the server.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// The UTC timestamp of the most recent modification to this note on the server.
        /// </summary>
        public DateTime ModifiedAt { get; set; }
    }
}
