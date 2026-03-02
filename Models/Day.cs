namespace Obrigenie.Models
{
    /// <summary>
    /// Represents a single calendar day with all data needed to render it in any view mode.
    /// Instances are created by Index.razor's helper methods and populated with courses and notes
    /// fetched from the API.
    /// </summary>
    public class Day
    {
        /// <summary>
        /// The exact date this object represents.
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// The localised name of the day of the week (e.g., "lundi", "mardi") formatted in French.
        /// Used as a header label in the single-day view.
        /// </summary>
        public string DayOfWeek { get; set; } = string.Empty;

        /// <summary>
        /// The day-of-month number as a string (e.g., "1", "15", "31").
        /// Displayed in the top-left corner of each day cell in grid views.
        /// </summary>
        public string DayOfMonth { get; set; } = string.Empty;

        /// <summary>
        /// True when this day falls within a school holiday period (excluding "Rentrée" markers).
        /// Holiday days are rendered with a distinct CSS class to visually differentiate them.
        /// </summary>
        public bool IsHoliday { get; set; }

        /// <summary>
        /// The full name of the holiday that covers this day (e.g., "Conge d'automne (Toussaint)").
        /// Empty string when the day is not a holiday.
        /// Used as the source for the ShortHolidayName computed property.
        /// </summary>
        public string HolidayName { get; set; } = string.Empty;

        /// <summary>
        /// True when this day coincides with the school "Rentrée" (back-to-school) marker.
        /// Such days receive a special CSS class to distinguish them from regular holiday days,
        /// since the Rentrée entry overlaps with the end of summer holidays in the data.
        /// </summary>
        public bool IsSchoolStart { get; set; }

        /// <summary>
        /// The list of courses scheduled for this day, loaded from the API.
        /// Populated asynchronously after the day model is created.
        /// </summary>
        public List<Course> Courses { get; set; } = new();

        /// <summary>
        /// The list of notes written by the user for this day, loaded from the API.
        /// Populated asynchronously either per-day or in a batch range request.
        /// </summary>
        public List<Note> Notes { get; set; } = new();

        /// <summary>
        /// Returns a short, human-readable version of the holiday name for display inside
        /// the compact day cells of the week and month grid views.
        /// Performs a series of string replacements to shorten long official holiday names
        /// (e.g., "Vacances de printemps (Paques)" → "Paques").
        /// Returns an empty string when the day has no holiday name.
        /// </summary>
        public string ShortHolidayName
        {
            get
            {
                // Return empty string immediately when there is no holiday name to shorten
                if (string.IsNullOrEmpty(HolidayName)) return string.Empty;

                // Apply a chain of replacements from longest/most-specific to shortest/generic
                return HolidayName
                    .Replace("Vacances d'hiver (Noel)",          "Noel")
                    .Replace("Vacances d'hiver",                  "Hiver")
                    .Replace("Conge d'automne (Toussaint)",       "Toussaint")
                    .Replace("Vacances d'automne (Toussaint)",    "Toussaint")
                    .Replace("Conge de detente (Carnaval)",       "Carnaval")
                    .Replace("Vacances de detente (Carnaval)",    "Carnaval")
                    .Replace("Vacances de printemps (Paques)",    "Paques")
                    .Replace("Vacances de printemps",             "Printemps")
                    .Replace("Les vacances d'ete debutent le",    "Ete")
                    .Replace("Fin de l'annee scolaire",           "Ete")
                    .Replace("Rentree scolaire",                  "Rentree")
                    .Replace("Jour de l'Armistice",               "Armistice")
                    .Replace("Fete de la Communaute francaise",   "Fete CF")
                    .Replace("Fete des morts",                    "Toussaint")
                    .Replace("Lundi de Paques",                   "Paques")
                    .Replace("Jeudi de l'Ascension",              "Ascension")
                    .Replace("Lundi de Pentecote",                "Pentecote")
                    .Replace("Mardi gras",                        "Carnaval");
            }
        }
    }
}
