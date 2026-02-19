using Blazored.LocalStorage;
using Obrigenie.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace Obrigenie.Services
{
    public class CalendarService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        private const string CacheKey = "CachedCalendarData";

        public CalendarService(HttpClient httpClient, ILocalStorageService localStorage)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
        }

        public async Task<SchoolYearCalendar> GetCalendarData()
        {
            try
            {
                var apiData = await _httpClient.GetFromJsonAsync<List<ApiCalendrierDto>>("api/values");
                if (apiData != null && apiData.Count > 0)
                {
                    await _localStorage.SetItemAsStringAsync(CacheKey, JsonSerializer.Serialize(apiData));
                    return ConvertApiDataToModel(apiData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[INFO] API injoignable ({ex.Message}). Tentative de lecture du cache...");
            }

            try
            {
                var cachedJson = await _localStorage.GetItemAsStringAsync(CacheKey);
                if (!string.IsNullOrEmpty(cachedJson))
                {
                    var cachedData = JsonSerializer.Deserialize<List<ApiCalendrierDto>>(cachedJson);
                    if (cachedData != null && cachedData.Count > 0)
                    {
                        return ConvertApiDataToModel(cachedData);
                    }
                }
            }
            catch { }

            return GenerateOfflineCalendar();
        }

        private SchoolYearCalendar ConvertApiDataToModel(List<ApiCalendrierDto> apiData)
        {
            var allHolidays = new List<Holiday>();
            DateTime schoolYearStart = DateTime.MinValue;
            string currentSchoolYearStr = GetCurrentSchoolYearString();

            foreach (var item in apiData)
            {
                if (schoolYearStart == DateTime.MinValue &&
                    item.nomEvenement != null &&
                    item.nomEvenement.Contains("Rentree") &&
                    item.anneeScolaire == currentSchoolYearStr)
                {
                    schoolYearStart = item.dateDebut.ToDateTime(TimeOnly.MinValue);
                }

                allHolidays.Add(new Holiday
                {
                    Name = item.nomEvenement ?? "Conge",
                    StartDate = item.dateDebut.ToDateTime(TimeOnly.MinValue),
                    EndDate = item.dateFin.ToDateTime(TimeOnly.MinValue)
                });
            }

            if (schoolYearStart == DateTime.MinValue)
                schoolYearStart = GetDefaultRentreeDate(DateTime.Today.Year);

            EnsureSchoolStartExists(allHolidays, GetDefaultRentreeDate(schoolYearStart.Year));
            EnsureSchoolStartExists(allHolidays, GetDefaultRentreeDate(schoolYearStart.Year + 1));

            return new SchoolYearCalendar { SchoolYearStart = schoolYearStart, Holidays = allHolidays };
        }

        private SchoolYearCalendar GenerateOfflineCalendar()
        {
            var (startYear, endYear) = GetCurrentSchoolYear();
            var holidays = new List<Holiday>();
            holidays.AddRange(GetFallbackHolidays(startYear, endYear));
            holidays.AddRange(GetFallbackHolidays(startYear + 1, endYear + 1));

            DateTime rentreeCurrent = GetDefaultRentreeDate(startYear);
            EnsureSchoolStartExists(holidays, rentreeCurrent);
            DateTime rentreeNext = GetDefaultRentreeDate(startYear + 1);
            EnsureSchoolStartExists(holidays, rentreeNext);

            return new SchoolYearCalendar { SchoolYearStart = rentreeCurrent, Holidays = holidays };
        }

        private void EnsureSchoolStartExists(List<Holiday> holidays, DateTime rentreeDate)
        {
            if (!holidays.Any(h => h.Name.Contains("Rentree") && h.StartDate == rentreeDate))
            {
                holidays.Add(new Holiday { Name = "Rentree scolaire", StartDate = rentreeDate, EndDate = rentreeDate });
            }
        }

        private DateTime GetDefaultRentreeDate(int year) => new DateTime(year, 8, 26);

        private (int startYear, int endYear) GetCurrentSchoolYear()
        {
            var today = DateTime.Today;
            if (today.Month >= 8) return (today.Year, today.Year + 1);
            return (today.Year - 1, today.Year);
        }

        private string GetCurrentSchoolYearString()
        {
            var (start, end) = GetCurrentSchoolYear();
            return $"{start}-{end}";
        }

        private List<Holiday> GetFallbackHolidays(int startYear, int endYear)
        {
            return new List<Holiday>
            {
                new() { Name = "Conge d'automne (Toussaint)", StartDate = new DateTime(startYear, 10, 20), EndDate = new DateTime(startYear, 11, 3) },
                new() { Name = "Vacances d'hiver (Noel)", StartDate = new DateTime(startYear, 12, 23), EndDate = new DateTime(endYear, 1, 5) },
                new() { Name = "Conge de detente (Carnaval)", StartDate = new DateTime(endYear, 2, 23), EndDate = new DateTime(endYear, 3, 9) },
                new() { Name = "Vacances de printemps (Paques)", StartDate = new DateTime(endYear, 4, 6), EndDate = new DateTime(endYear, 4, 19) },
                new() { Name = "Vacances d'ete", StartDate = new DateTime(endYear, 7, 5), EndDate = new DateTime(endYear, 8, 25) },
            };
        }
    }

    public static class SchoolPeriodHelper
    {
        private static string GetShortName(string holidayName)
        {
            if (holidayName.Contains("Toussaint") || holidayName.Contains("automne")) return "Toussaint";
            if (holidayName.Contains("Noel") || holidayName.Contains("Noël") || holidayName.Contains("hiver")) return "Noël";
            if (holidayName.Contains("Carnaval") || holidayName.Contains("detente") || holidayName.Contains("détente")) return "Carnaval";
            if (holidayName.Contains("Paques") || holidayName.Contains("Pâques") || holidayName.Contains("printemps")) return "Pâques";
            if (holidayName.Contains("ete") || holidayName.Contains("été") || holidayName.Contains("Ete") || holidayName.Contains("Été")) return "Été";
            if (holidayName.Contains("Rentree") || holidayName.Contains("Rentrée")) return "Rentrée";
            return holidayName;
        }

        // Retourne (début, fin) de la période scolaire contenant la date
        public static (DateTime start, DateTime end, string title) GetPeriodBounds(DateTime date, List<Holiday> holidays)
        {
            var vacations = holidays
                .Where(h => !h.Name.Contains("Rentree") && !h.Name.Contains("Rentrée"))
                .OrderBy(h => h.StartDate)
                .ToList();

            // Si la date est dans un congé, on va à la période suivante
            var inHoliday = vacations.FirstOrDefault(h => date.Date >= h.StartDate.Date && date.Date <= h.EndDate.Date);
            if (inHoliday != null)
                date = inHoliday.EndDate.AddDays(1);

            var prev = vacations.Where(h => h.EndDate.Date < date.Date).OrderByDescending(h => h.EndDate).FirstOrDefault();
            var next = vacations.Where(h => h.StartDate.Date > date.Date).OrderBy(h => h.StartDate).FirstOrDefault();

            DateTime start = prev != null ? prev.EndDate.AddDays(1) : date.AddDays(-60);
            DateTime end   = next != null ? next.StartDate.AddDays(-1) : date.AddDays(60);

            string prevName = prev != null ? GetShortName(prev.Name) : "Rentrée";
            string nextName = next != null ? GetShortName(next.Name) : "Fin d'année";
            string title = $"{prevName} → {nextName}";

            return (start, end, title);
        }

        public static string? GetLabel(DateTime date, List<Holiday> holidays)
        {
            if (holidays == null || holidays.Count == 0) return null;

            // Vacances uniquement (pas les Rentrée qui sont des marqueurs ponctuels)
            var vacations = holidays
                .Where(h => !h.Name.Contains("Rentree") && !h.Name.Contains("Rentrée"))
                .OrderBy(h => h.StartDate)
                .ToList();

            // Congé en cours ?
            var current = vacations.FirstOrDefault(h => date.Date >= h.StartDate.Date && date.Date <= h.EndDate.Date);
            if (current != null)
                return $"Congé de {GetShortName(current.Name)}";

            // Congé précédent et suivant
            var prev = vacations.Where(h => h.EndDate.Date < date.Date).OrderByDescending(h => h.EndDate).FirstOrDefault();
            var next = vacations.Where(h => h.StartDate.Date > date.Date).OrderBy(h => h.StartDate).FirstOrDefault();

            if (prev != null && next != null)
                return $"{GetShortName(prev.Name)} → {GetShortName(next.Name)}";
            if (next != null)
                return $"Rentrée → {GetShortName(next.Name)}";
            if (prev != null)
                return $"Après {GetShortName(prev.Name)}";

            return null;
        }
    }

    public class ApiCalendrierDto
    {
        public string? nomEvenement { get; set; }
        public DateOnly dateDebut { get; set; }
        public DateOnly dateFin { get; set; }
        public string? typeEvenement { get; set; }
        public string? anneeScolaire { get; set; }
    }
}
