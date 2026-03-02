namespace Obrigenie.Models
{
    /// <summary>
    /// Represents a recurring course (class/lesson) that appears on the school calendar.
    /// A course has a fixed weekly schedule encoded as a bitmask, a time range, and a display color.
    /// Courses are loaded from the API for a specific date and displayed in both the day and week views.
    /// </summary>
    public class Course
    {
        /// <summary>
        /// The server-assigned unique identifier for this course.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The display name of the course (e.g., "Mathématiques", "Français").
        /// Shown in course blocks on the calendar.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The background color used to render this course block on the calendar.
        /// Stored as a CSS color string (e.g., "#FF5733" or "rgba(255,87,51,1)").
        /// Defaults to red (#FF0000) when no color is assigned.
        /// </summary>
        public string Color { get; set; } = "#FF0000";

        /// <summary>
        /// The first calendar date on which this course occurs.
        /// Used to restrict the course to the correct school year.
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// The last calendar date on which this course occurs.
        /// The course will not appear on dates beyond this value.
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// The daily start time of the course (e.g., TimeSpan(8, 30, 0) for 08:30).
        /// Used to position the course block in the hourly time grid of the single-day view.
        /// </summary>
        public TimeSpan StartTime { get; set; }

        /// <summary>
        /// The daily end time of the course (e.g., TimeSpan(10, 0, 0) for 10:00).
        /// Used to calculate the vertical span of the course block in the time grid.
        /// </summary>
        public TimeSpan EndTime { get; set; }

        /// <summary>
        /// A bitmask that encodes which days of the week this course occurs on.
        /// Bit values: Monday=1, Tuesday=2, Wednesday=4, Thursday=8,
        ///             Friday=16, Saturday=32, Sunday=64.
        /// Multiple days are combined with bitwise OR (e.g., Mon+Wed+Fri = 1+4+16 = 21).
        /// </summary>
        public int DaysOfWeek { get; set; }

        /// <summary>
        /// Determines whether this course occurs on the given day of the week
        /// by checking the corresponding bit in the DaysOfWeek bitmask.
        /// </summary>
        /// <param name="dayOfWeek">The day of the week to test.</param>
        /// <returns>True if the course is scheduled for that day; otherwise false.</returns>
        public bool OccursOnDay(DayOfWeek dayOfWeek)
        {
            // Each day maps to a specific bit position in the DaysOfWeek integer.
            // A bitwise AND with the mask for that day returns non-zero when the bit is set.
            return dayOfWeek switch
            {
                DayOfWeek.Monday    => (DaysOfWeek & 1) != 0,   // bit 0 → Monday
                DayOfWeek.Tuesday   => (DaysOfWeek & 2) != 0,   // bit 1 → Tuesday
                DayOfWeek.Wednesday => (DaysOfWeek & 4) != 0,   // bit 2 → Wednesday
                DayOfWeek.Thursday  => (DaysOfWeek & 8) != 0,   // bit 3 → Thursday
                DayOfWeek.Friday    => (DaysOfWeek & 16) != 0,  // bit 4 → Friday
                DayOfWeek.Saturday  => (DaysOfWeek & 32) != 0,  // bit 5 → Saturday
                DayOfWeek.Sunday    => (DaysOfWeek & 64) != 0,  // bit 6 → Sunday
                _ => false                                        // unknown day → never occurs
            };
        }
    }
}
