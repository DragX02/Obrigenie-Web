namespace Obrigenie.Models
{
    /// <summary>
    /// Represents a school holiday period or a notable school calendar event
    /// (such as the back-to-school date "Rentrée").
    /// Holidays are loaded from the API and cached in local storage by CalendarService.
    /// They drive the coloring, labeling, and school-period boundaries in the calendar view.
    /// </summary>
    public class Holiday
    {
        /// <summary>
        /// The database identifier for this holiday record.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The display name of the holiday or event.
        /// Examples: "Conge d'automne (Toussaint)", "Vacances d'ete", "Rentree scolaire".
        /// The name is also used as a keyword to distinguish holiday types from "Rentrée" markers.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The first day of the holiday period (inclusive).
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// The last day of the holiday period (inclusive).
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Returns true if the given date falls within this holiday period (inclusive on both ends).
        /// Uses date-only comparison (ignores time component) to avoid timezone issues.
        /// </summary>
        /// <param name="date">The date to check.</param>
        /// <returns>True when <paramref name="date"/> is between StartDate and EndDate inclusive.</returns>
        public bool IsDateInHoliday(DateTime date)
        {
            // Compare only the date portion so that time-of-day values do not affect the result
            return date.Date >= StartDate.Date && date.Date <= EndDate.Date;
        }
    }
}
