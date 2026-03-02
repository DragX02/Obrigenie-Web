namespace Obrigenie.Models
{
    /// <summary>
    /// Represents the full school-year calendar data used throughout the application.
    /// This object is the top-level result returned by CalendarService and holds:
    ///   - the back-to-school date (used for school-week numbering)
    ///   - the complete list of holiday periods for the current and following school year
    /// It is cached in the Index page component and passed to SchoolPeriodHelper for
    /// period boundary calculations and period label generation.
    /// </summary>
    public class SchoolYearCalendar
    {
        /// <summary>
        /// The date on which the current school year begins (the "Rentrée" date).
        /// Used as the anchor point (week S1) for the school-week numbering algorithm.
        /// When the API returns no Rentrée entry for the current year, a default date
        /// of August 26 of the current school start year is used.
        /// </summary>
        public DateTime SchoolYearStart { get; set; }

        /// <summary>
        /// The complete, ordered list of holiday periods (and Rentrée markers) for all
        /// school years covered by the API data.
        /// Includes both the current school year and the next, so that navigation across
        /// year boundaries works without requiring a new API call.
        /// </summary>
        public List<Holiday> Holidays { get; set; } = new();
    }
}
