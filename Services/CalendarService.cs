using Blazored.LocalStorage;
using Obrigenie.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace Obrigenie.Services
{
    /// <summary>
    /// Responsible for fetching, caching, and providing school-year calendar data
    /// (holiday periods and the back-to-school date) to the rest of the application.
    ///
    /// Data flow:
    ///   1. Attempt to fetch the current calendar from the API endpoint "api/values".
    ///   2. If the API is reachable, cache the raw JSON in browser local storage.
    ///   3. If the API is unreachable, attempt to serve the data from the local cache.
    ///   4. If neither source is available, generate a hard-coded offline fallback calendar.
    ///
    /// This service is registered as Scoped in Program.cs.
    /// </summary>
    public class CalendarService
    {
        /// <summary>
        /// The HTTP client pre-configured with the API base address and the auth header handler.
        /// Used to call "api/values" to retrieve school-year event data.
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// The Blazored local storage service used to cache the raw API response JSON
        /// so calendar data is available when the server cannot be reached.
        /// </summary>
        private readonly ILocalStorageService _localStorage;

        /// <summary>
        /// The key used to store and retrieve the serialised calendar JSON in local storage.
        /// </summary>
        private const string CacheKey = "CachedCalendarData";

        /// <summary>
        /// Initialises the service with its HTTP and local storage dependencies.
        /// </summary>
        /// <param name="httpClient">The HTTP client for API calls.</param>
        /// <param name="localStorage">The local storage service for caching.</param>
        public CalendarService(HttpClient httpClient, ILocalStorageService localStorage)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
        }

        /// <summary>
        /// Returns the school-year calendar, following the three-tier strategy:
        /// live API → local storage cache → hard-coded offline fallback.
        /// The result is always a fully populated <see cref="SchoolYearCalendar"/> instance.
        /// </summary>
        /// <returns>
        /// A <see cref="SchoolYearCalendar"/> containing the school-year start date
        /// and the full list of holiday periods.
        /// </returns>
        public async Task<SchoolYearCalendar> GetCalendarData()
        {
            try
            {
                // Attempt to fetch live data from the server API
                var apiData = await _httpClient.GetFromJsonAsync<List<ApiCalendrierDto>>("api/values");

                if (apiData != null && apiData.Count > 0)
                {
                    // Serialise the raw API response and persist it to local storage for offline use
                    await _localStorage.SetItemAsStringAsync(CacheKey, JsonSerializer.Serialize(apiData));

                    // Convert the raw DTO list to the application model and return it
                    return ConvertApiDataToModel(apiData);
                }
            }
            catch (Exception ex)
            {
                // The API may be unreachable (e.g., no network). Log and fall through to the cache.
                Console.WriteLine($"[INFO] API unreachable ({ex.Message}). Attempting to read from cache...");
            }

            try
            {
                // Attempt to read previously cached calendar data from local storage
                var cachedJson = await _localStorage.GetItemAsStringAsync(CacheKey);

                if (!string.IsNullOrEmpty(cachedJson))
                {
                    // Deserialise the cached JSON back to the DTO list
                    var cachedData = JsonSerializer.Deserialize<List<ApiCalendrierDto>>(cachedJson);

                    if (cachedData != null && cachedData.Count > 0)
                    {
                        // Convert cached data to the application model and return it
                        return ConvertApiDataToModel(cachedData);
                    }
                }
            }
            catch
            {
                // Silently ignore cache read or deserialisation errors; fall through to offline mode
            }

            // Last resort: generate a static offline calendar with hard-coded Belgian school dates
            return GenerateOfflineCalendar();
        }

        /// <summary>
        /// Converts a list of raw API calendar DTOs into the application's
        /// <see cref="SchoolYearCalendar"/> model.
        /// During conversion:
        ///   - The "Rentrée" event for the current school year is used to set SchoolYearStart.
        ///   - Rentrée markers are injected for the current and following years if missing.
        /// </summary>
        /// <param name="apiData">The raw list of event DTOs received from or cached from the API.</param>
        /// <returns>A fully populated <see cref="SchoolYearCalendar"/> instance.</returns>
        private SchoolYearCalendar ConvertApiDataToModel(List<ApiCalendrierDto> apiData)
        {
            var allHolidays = new List<Holiday>();
            DateTime schoolYearStart = DateTime.MinValue;

            // Determine the "YYYY-YYYY" string for the current school year (e.g., "2024-2025")
            string currentSchoolYearStr = GetCurrentSchoolYearString();

            foreach (var item in apiData)
            {
                // Find the Rentrée entry for the current school year to use as the year start anchor
                if (schoolYearStart == DateTime.MinValue &&
                    item.nomEvenement != null &&
                    item.nomEvenement.Contains("Rentree") &&
                    item.anneeScolaire == currentSchoolYearStr)
                {
                    // Convert DateOnly to DateTime (midnight) for consistent comparisons
                    schoolYearStart = item.dateDebut.ToDateTime(TimeOnly.MinValue);
                }

                // Add every event (holiday or Rentrée) to the holidays list
                allHolidays.Add(new Holiday
                {
                    Name      = item.nomEvenement ?? "Conge",
                    StartDate = item.dateDebut.ToDateTime(TimeOnly.MinValue),
                    EndDate   = item.dateFin.ToDateTime(TimeOnly.MinValue)
                });
            }

            // If no Rentrée was found in the API data, use the default date (Aug 26)
            if (schoolYearStart == DateTime.MinValue)
                schoolYearStart = GetDefaultRentreeDate(DateTime.Today.Year);

            // Ensure Rentrée markers exist for both the current and the following school year
            EnsureSchoolStartExists(allHolidays, GetDefaultRentreeDate(schoolYearStart.Year));
            EnsureSchoolStartExists(allHolidays, GetDefaultRentreeDate(schoolYearStart.Year + 1));

            return new SchoolYearCalendar { SchoolYearStart = schoolYearStart, Holidays = allHolidays };
        }

        /// <summary>
        /// Generates a minimal offline calendar using hard-coded Belgian school holiday dates
        /// when both the API and the local storage cache are unavailable.
        /// Covers the current and the following school year so that navigation still works.
        /// </summary>
        /// <returns>A <see cref="SchoolYearCalendar"/> built from static fallback holiday data.</returns>
        private SchoolYearCalendar GenerateOfflineCalendar()
        {
            // Determine the start and end years of the current school year
            var (startYear, endYear) = GetCurrentSchoolYear();

            var holidays = new List<Holiday>();

            // Add fallback holidays for the current school year (e.g., 2024-2025)
            holidays.AddRange(GetFallbackHolidays(startYear, endYear));

            // Add fallback holidays for the following school year (e.g., 2025-2026)
            // so that the calendar still works when browsing into the next year
            holidays.AddRange(GetFallbackHolidays(startYear + 1, endYear + 1));

            // Determine the back-to-school date and ensure Rentrée markers are present
            DateTime rentreeCurrent = GetDefaultRentreeDate(startYear);
            EnsureSchoolStartExists(holidays, rentreeCurrent);

            DateTime rentreeNext = GetDefaultRentreeDate(startYear + 1);
            EnsureSchoolStartExists(holidays, rentreeNext);

            return new SchoolYearCalendar { SchoolYearStart = rentreeCurrent, Holidays = holidays };
        }

        /// <summary>
        /// Adds a synthetic "Rentree scolaire" marker to the holiday list if no such entry
        /// already exists for the given date.
        /// This guarantees that SchoolPeriodHelper always finds a Rentrée anchor when computing
        /// period boundaries, even when the API data is incomplete.
        /// </summary>
        /// <param name="holidays">The holiday list to check and potentially modify.</param>
        /// <param name="rentreeDate">The expected Rentrée date to ensure exists.</param>
        private void EnsureSchoolStartExists(List<Holiday> holidays, DateTime rentreeDate)
        {
            // Only add a synthetic Rentrée entry if none already exists for this exact date
            if (!holidays.Any(h => h.Name.Contains("Rentree") && h.StartDate == rentreeDate))
            {
                holidays.Add(new Holiday
                {
                    Name      = "Rentree scolaire",
                    StartDate = rentreeDate,
                    EndDate   = rentreeDate  // Rentrée is a single-day marker
                });
            }
        }

        /// <summary>
        /// Returns the default back-to-school date for a given year.
        /// Defaults to August 26 of the provided year, which is typically
        /// the earliest possible Rentrée date in the Belgian school calendar.
        /// </summary>
        /// <param name="year">The year in which the school year begins (e.g., 2024).</param>
        /// <returns>August 26 of the given year.</returns>
        private DateTime GetDefaultRentreeDate(int year) => new DateTime(year, 8, 26);

        /// <summary>
        /// Determines the start and end years of the current school year based on today's date.
        /// In Belgium, the school year runs from September to June:
        ///   - If today is in August or later, the school year started this calendar year.
        ///   - Otherwise, the school year started the previous calendar year.
        /// </summary>
        /// <returns>
        /// A tuple (startYear, endYear) where startYear is the September start year
        /// and endYear is the June end year.
        /// </returns>
        private (int startYear, int endYear) GetCurrentSchoolYear()
        {
            var today = DateTime.Today;

            // School year starts in August (or September), so months 8-12 belong to the new year
            if (today.Month >= 8) return (today.Year, today.Year + 1);

            // Months 1-7 belong to a school year that started last calendar year
            return (today.Year - 1, today.Year);
        }

        /// <summary>
        /// Returns the school-year identifier string in "YYYY-YYYY" format
        /// (e.g., "2024-2025") for the current school year.
        /// This string matches the "anneeScolaire" field in the API response DTOs.
        /// </summary>
        /// <returns>The school year string for the current year.</returns>
        private string GetCurrentSchoolYearString()
        {
            var (start, end) = GetCurrentSchoolYear();
            return $"{start}-{end}";
        }

        /// <summary>
        /// Returns a hard-coded list of typical Belgian school holiday periods
        /// for a school year spanning the given start and end calendar years.
        /// Used only when neither the API nor the local cache is available.
        /// </summary>
        /// <param name="startYear">The calendar year in which the school year begins (e.g., 2024).</param>
        /// <param name="endYear">The calendar year in which the school year ends (e.g., 2025).</param>
        /// <returns>A list of five standard Belgian school holiday periods.</returns>
        private List<Holiday> GetFallbackHolidays(int startYear, int endYear)
        {
            return new List<Holiday>
            {
                // Autumn break (Toussaint) — late October to early November
                new() { Name = "Conge d'automne (Toussaint)",       StartDate = new DateTime(startYear, 10, 20), EndDate = new DateTime(startYear, 11, 3) },

                // Winter break (Christmas) — late December to early January
                new() { Name = "Vacances d'hiver (Noel)",           StartDate = new DateTime(startYear, 12, 23), EndDate = new DateTime(endYear, 1, 4) },

                // Carnival break (spring relaxation) — mid February to early March
                new() { Name = "Conge de detente (Carnaval)",       StartDate = new DateTime(endYear, 2, 16),   EndDate = new DateTime(endYear, 3, 1) },

                // Spring break (Easter) — early to mid April
                new() { Name = "Vacances de printemps (Paques)",    StartDate = new DateTime(endYear, 4, 6),    EndDate = new DateTime(endYear, 4, 19) },

                // Summer holidays — early July to late August
                new() { Name = "Vacances d'ete",                    StartDate = new DateTime(endYear, 7, 5),    EndDate = new DateTime(endYear, 8, 25) },
            };
        }
    }

    /// <summary>
    /// A static helper class that provides methods for computing school-period boundaries
    /// and generating human-readable period labels based on a list of holiday periods.
    /// Used by Index.razor to determine the date range for the "Trimestre" view and to
    /// display the navigation strip label (e.g., "Toussaint → Noël") above the calendar.
    /// </summary>
    public static class SchoolPeriodHelper
    {
        /// <summary>
        /// Maps a full holiday name to a short display label suitable for the period navigation strip.
        /// Falls back to the original name when no specific match is found.
        /// </summary>
        /// <param name="holidayName">The full holiday name stored in the Holiday.Name property.</param>
        /// <returns>A short display name (e.g., "Toussaint", "Noël", "Été").</returns>
        private static string GetShortName(string holidayName)
        {
            if (holidayName.Contains("Toussaint") || holidayName.Contains("automne"))                                     return "Toussaint";
            if (holidayName.Contains("Noel") || holidayName.Contains("Noël") || holidayName.Contains("hiver"))            return "Noël";
            if (holidayName.Contains("Carnaval") || holidayName.Contains("detente") || holidayName.Contains("détente"))   return "Carnaval";
            if (holidayName.Contains("Paques") || holidayName.Contains("Pâques") || holidayName.Contains("printemps"))    return "Pâques";
            if (holidayName.Contains("ete") || holidayName.Contains("été") || holidayName.Contains("Ete") || holidayName.Contains("Été")) return "Été";
            if (holidayName.Contains("Rentree") || holidayName.Contains("Rentrée"))                                       return "Rentrée";

            // No specific match found: return the original name as-is
            return holidayName;
        }

        /// <summary>
        /// Computes the start date, end date, and title string for the school period that
        /// contains the given date.
        ///
        /// A "school period" (trimester) is the segment of school days between two consecutive
        /// holiday breaks. For example, the period between Toussaint and Noël.
        ///
        /// If the given date falls inside a holiday, the function moves back to the day before
        /// that holiday begins, so it lands in the preceding school period instead.
        ///
        /// If the API provides a "Rentrée" entry immediately after the previous holiday,
        /// that exact date is used as the period start (instead of the day after the holiday ends).
        /// </summary>
        /// <param name="date">The reference date used to determine which period to display.</param>
        /// <param name="holidays">
        /// The complete list of holidays (including Rentrée markers) for all school years.
        /// </param>
        /// <returns>
        /// A tuple containing:
        ///   start  — the first day of the school period (inclusive),
        ///   end    — the last day of the school period (inclusive),
        ///   title  — a human-readable label such as "Toussaint → Noël".
        /// </returns>
        public static (DateTime start, DateTime end, string title) GetPeriodBounds(
            DateTime date,
            List<Holiday> holidays)
        {
            // Work only with real holiday breaks; Rentrée markers are handled separately below
            var vacations = holidays
                .Where(h => !h.Name.Contains("Rentree") && !h.Name.Contains("Rentrée"))
                .OrderBy(h => h.StartDate)
                .ToList();

            // If the reference date is inside a holiday, retreat to the day before it starts
            // so the algorithm lands in the preceding (more useful) school period
            var inHoliday = vacations.FirstOrDefault(
                h => date.Date >= h.StartDate.Date && date.Date <= h.EndDate.Date);
            if (inHoliday != null)
                date = inHoliday.StartDate.AddDays(-1);

            // Find the holiday that ended most recently before the (adjusted) date
            var prev = vacations
                .Where(h => h.EndDate.Date < date.Date)
                .OrderByDescending(h => h.EndDate)
                .FirstOrDefault();

            // Find the next upcoming holiday after the (adjusted) date
            var next = vacations
                .Where(h => h.StartDate.Date > date.Date)
                .OrderBy(h => h.StartDate)
                .FirstOrDefault();

            // Default period start: the day after the previous holiday ended (or 60 days ago)
            DateTime defaultStart = prev != null ? prev.EndDate.AddDays(1) : date.AddDays(-60);

            // If there is a Rentrée marker between defaultStart and the next holiday,
            // use its date as the precise period start
            var rentreeAfterPrev = holidays
                .Where(h => (h.Name.Contains("Rentree") || h.Name.Contains("Rentrée")) &&
                            h.StartDate.Date >= defaultStart.Date &&
                            (next == null || h.StartDate.Date < next.StartDate.Date))
                .OrderBy(h => h.StartDate)
                .FirstOrDefault();

            // Use the Rentrée date when available; otherwise use the default start
            DateTime start = rentreeAfterPrev != null ? rentreeAfterPrev.StartDate : defaultStart;

            // Period end: the day before the next holiday starts (or 60 days into the future)
            DateTime end = next != null ? next.StartDate.AddDays(-1) : date.AddDays(60);

            // Build a human-readable title for the period (e.g., "Toussaint → Noël")
            string prevName = prev != null ? GetShortName(prev.Name) : "Rentrée";
            string nextName = next != null ? GetShortName(next.Name) : "Fin d'année";
            string title    = $"{prevName} → {nextName}";

            return (start, end, title);
        }

        /// <summary>
        /// Returns a short human-readable label describing the school-period context of a given date.
        /// This label is displayed in the narrow banner below the view-mode toolbar in week/month views.
        ///
        /// Possible label formats:
        ///   - "Congé de Toussaint"         (date is inside a holiday)
        ///   - "Toussaint → Noël"           (date is between two known holidays)
        ///   - "Rentrée → Toussaint"        (date is before the first known holiday)
        ///   - "Après Été"                  (date is after the last known holiday)
        ///   - null                          (no holiday data is available)
        /// </summary>
        /// <param name="date">The date for which to generate the label.</param>
        /// <param name="holidays">The complete holiday list for all school years.</param>
        /// <returns>A label string, or null when no data is available.</returns>
        public static string? GetLabel(DateTime date, List<Holiday> holidays)
        {
            // Return null immediately if there is no holiday data to work with
            if (holidays == null || holidays.Count == 0) return null;

            // Work only with proper vacation periods, not Rentrée markers (which are single-day)
            var vacations = holidays
                .Where(h => !h.Name.Contains("Rentree") && !h.Name.Contains("Rentrée"))
                .OrderBy(h => h.StartDate)
                .ToList();

            // Check whether the given date falls inside an active holiday break
            var current = vacations.FirstOrDefault(
                h => date.Date >= h.StartDate.Date && date.Date <= h.EndDate.Date);
            if (current != null)
                return $"Congé de {GetShortName(current.Name)}";

            // Find the most recent past holiday and the next upcoming holiday
            var prev = vacations
                .Where(h => h.EndDate.Date < date.Date)
                .OrderByDescending(h => h.EndDate)
                .FirstOrDefault();

            var next = vacations
                .Where(h => h.StartDate.Date > date.Date)
                .OrderBy(h => h.StartDate)
                .FirstOrDefault();

            // Both a previous and a next holiday are known: show the transition arrow
            if (prev != null && next != null)
                return $"{GetShortName(prev.Name)} → {GetShortName(next.Name)}";

            // Only a future holiday is known (date is before any vacation has occurred)
            if (next != null)
                return $"Rentrée → {GetShortName(next.Name)}";

            // Only a past holiday is known (date is after the last known vacation)
            if (prev != null)
                return $"Après {GetShortName(prev.Name)}";

            // No surrounding holidays found at all
            return null;
        }
    }

    /// <summary>
    /// Raw Data Transfer Object that maps the JSON structure returned by the "api/values" endpoint.
    /// Each instance represents one school-year event (holiday, Rentrée, etc.) as stored
    /// in the backend database.
    /// </summary>
    public class ApiCalendrierDto
    {
        /// <summary>
        /// The name of the school calendar event (e.g., "Conge d'automne (Toussaint)",
        /// "Rentree scolaire"). Used to classify events and generate short display labels.
        /// </summary>
        public string? nomEvenement { get; set; }

        /// <summary>
        /// The first day of the event period, stored as a DateOnly value (no time component).
        /// </summary>
        public DateOnly dateDebut { get; set; }

        /// <summary>
        /// The last day of the event period, stored as a DateOnly value (no time component).
        /// </summary>
        public DateOnly dateFin { get; set; }

        /// <summary>
        /// The type or category of the event as returned by the API
        /// (e.g., "CONGE", "RENTREE"). May be null.
        /// </summary>
        public string? typeEvenement { get; set; }

        /// <summary>
        /// The school year to which this event belongs, in "YYYY-YYYY" format
        /// (e.g., "2024-2025"). Used to identify the Rentrée for the current school year.
        /// </summary>
        public string? anneeScolaire { get; set; }
    }
}
