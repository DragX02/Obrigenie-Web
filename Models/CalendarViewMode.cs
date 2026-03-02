namespace Obrigenie.Models
{
    /// <summary>
    /// Defines all available calendar display modes.
    /// The user can switch between these modes using the view-mode toolbar on the main page.
    /// </summary>
    public enum CalendarViewMode
    {
        /// <summary>
        /// Shows only Monday through Friday (5 days) in a grid layout.
        /// </summary>
        Week,

        /// <summary>
        /// Shows Monday through Sunday (7 days) in a grid layout, including the weekend.
        /// </summary>
        WeekAndWeekend,

        /// <summary>
        /// Shows a full calendar month grid (up to 6 weeks, 42 cells).
        /// </summary>
        Month,

        /// <summary>
        /// Shows a single day with an hourly time grid from 06:00 to 22:00.
        /// </summary>
        SingleDay,

        /// <summary>
        /// Shows the entire school period (trimester) between two consecutive holiday breaks.
        /// Weeks are displayed as columns and weekdays as rows.
        /// </summary>
        SchoolPeriod
    }
}
