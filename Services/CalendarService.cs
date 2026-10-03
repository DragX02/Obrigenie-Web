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
                Console.WriteLine($"[INFO] API unreachable ({ex.Message}). Attempting to read from cache...");
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
            catch
            {
            }

            return GenerateOfflineCalendar();
        }

        public static bool EstRentree(string? nom)
            => TexteUtil.SansAccents(nom).Contains("Rentree", StringComparison.OrdinalIgnoreCase);

        public static SchoolYearCalendar AppliquerCorrections(SchoolYearCalendar calendrier,
                                                             IReadOnlyList<UserConge> corrections)
        {
            var parCalendrier = corrections
                .Where(c => c.IdCalendrierFk is > 0)
                .GroupBy(c => c.IdCalendrierFk!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            var resultat = new List<Holiday>();

            var debutAnnee = calendrier.SchoolYearStart;

            foreach (var conge in calendrier.Holidays)
            {
                if (conge.Id <= 0 || !parCalendrier.TryGetValue(conge.Id, out var correction))
                {
                    resultat.Add(conge);
                    continue;
                }

                bool estRentree = EstRentree(conge.Name);

                if (correction.Masque)
                {
                    if (!estRentree) continue;
                    resultat.Add(conge);
                    continue;
                }

                resultat.Add(new Holiday
                {
                    Id        = conge.Id,
                    Name      = correction.Nom,
                    StartDate = correction.DateDebut.Date,
                    EndDate   = correction.DateFin.Date,
                });

                if (!estRentree) continue;

                if (conge.StartDate.Date == debutAnnee.Date) debutAnnee = correction.DateDebut.Date;
            }

            var idsCorriges = new HashSet<int>(parCalendrier.Keys);

            foreach (var ajout in corrections.Where(c => c.IdCalendrierFk is null or 0 && !c.Masque))
            {
                resultat.Add(new Holiday
                {
                    Name      = ajout.Nom,
                    StartDate = ajout.DateDebut.Date,
                    EndDate   = ajout.DateFin.Date,
                });
            }

            return new SchoolYearCalendar
            {
                SchoolYearStart = debutAnnee,
                Holidays        = Dedupliquer(resultat, idsCorriges),
            };
        }

        private static List<Holiday> Dedupliquer(List<Holiday> conges, HashSet<int> idsCorriges)
        {
            var resultat = new List<Holiday>();

            foreach (var conge in conges.OrderBy(h => h.StartDate))
            {
                var doublon = resultat.FirstOrDefault(garde => MemeConge(garde, conge));

                if (doublon == null)
                {
                    resultat.Add(conge);
                    continue;
                }

                if (Priorite(conge, idsCorriges) > Priorite(doublon, idsCorriges))
                    resultat[resultat.IndexOf(doublon)] = conge;
            }

            return resultat.OrderBy(h => h.StartDate).ToList();
        }

        private static int Priorite(Holiday conge, HashSet<int> idsCorriges)
            => idsCorriges.Contains(conge.Id) ? 2 : conge.Id > 0 ? 1 : 0;

        private static bool MemeConge(Holiday a, Holiday b)
        {
            if (AnneeScolaire(a.StartDate) != AnneeScolaire(b.StartDate)) return false;

            if (EstRentree(a.Name) && EstRentree(b.Name)) return true;

            if (HolidayColors.Cle(a.Name) != HolidayColors.Cle(b.Name)) return false;

            return a.StartDate.Date <= b.EndDate.Date && b.StartDate.Date <= a.EndDate.Date;
        }

        public static int AnneeScolaire(DateTime date) => date.Month >= 8 ? date.Year : date.Year - 1;

        public static string LibelleAnneeScolaire(DateTime date)
        {
            int debut = AnneeScolaire(date);
            return $"{debut}-{debut + 1}";
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
                    Id        = item.idCalendrier,
                    Name      = item.nomEvenement ?? "Conge",
                    StartDate = item.dateDebut.ToDateTime(TimeOnly.MinValue),
                    EndDate   = item.dateFin.ToDateTime(TimeOnly.MinValue)
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
            if (!holidays.Any(h => EstRentree(h.Name) && AnneeScolaire(h.StartDate) == AnneeScolaire(rentreeDate)))
            {
                holidays.Add(new Holiday
                {
                    Name      = "Rentree scolaire",
                    StartDate = rentreeDate,
                    EndDate   = rentreeDate
                });
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
                new() { Name = "Conge d'automne (Toussaint)",       StartDate = new DateTime(startYear, 10, 20), EndDate = new DateTime(startYear, 11, 3) },

                new() { Name = "Vacances d'hiver (Noel)",           StartDate = new DateTime(startYear, 12, 23), EndDate = new DateTime(endYear, 1, 4) },

                new() { Name = "Conge de detente (Carnaval)",       StartDate = new DateTime(endYear, 2, 16),   EndDate = new DateTime(endYear, 3, 1) },

                new() { Name = "Vacances de printemps (Paques)",    StartDate = new DateTime(endYear, 4, 6),    EndDate = new DateTime(endYear, 4, 19) },

                new() { Name = "Vacances d'ete",                    StartDate = new DateTime(endYear, 7, 5),    EndDate = new DateTime(endYear, 8, 25) },
            };
        }
    }

    public static class SchoolPeriodHelper
    {
        private static string GetShortName(string holidayName)
        {
            if (holidayName.Contains("Toussaint") || holidayName.Contains("automne"))                                     return "Toussaint";
            if (holidayName.Contains("Noel") || holidayName.Contains("Noël") || holidayName.Contains("hiver"))            return "Noël";
            if (holidayName.Contains("Carnaval") || holidayName.Contains("detente") || holidayName.Contains("détente"))   return "Carnaval";
            if (holidayName.Contains("Paques") || holidayName.Contains("Pâques") || holidayName.Contains("printemps"))    return "Pâques";
            if (holidayName.Contains("ete") || holidayName.Contains("été") || holidayName.Contains("Ete") || holidayName.Contains("Été")) return "Été";
            if (holidayName.Contains("Rentree") || holidayName.Contains("Rentrée"))                                       return "Rentrée";

            return holidayName;
        }

        public static (DateTime start, DateTime end, string title) GetPeriodBounds(
            DateTime date,
            List<Holiday> holidays)
        {
            var vacations = holidays
                .Where(h => !h.Name.Contains("Rentree") && !h.Name.Contains("Rentrée"))
                .OrderBy(h => h.StartDate)
                .ToList();

            var inHoliday = vacations.FirstOrDefault(
                h => date.Date >= h.StartDate.Date && date.Date <= h.EndDate.Date);
            if (inHoliday != null)
                date = inHoliday.StartDate.AddDays(-1);

            var prev = vacations
                .Where(h => h.EndDate.Date < date.Date)
                .OrderByDescending(h => h.EndDate)
                .FirstOrDefault();

            var next = vacations
                .Where(h => h.StartDate.Date > date.Date)
                .OrderBy(h => h.StartDate)
                .FirstOrDefault();

            DateTime defaultStart = prev != null ? prev.EndDate.AddDays(1) : date.AddDays(-60);

            var rentreeAfterPrev = holidays
                .Where(h => (h.Name.Contains("Rentree") || h.Name.Contains("Rentrée")) &&
                            h.StartDate.Date >= defaultStart.Date &&
                            (next == null || h.StartDate.Date < next.StartDate.Date))
                .OrderBy(h => h.StartDate)
                .FirstOrDefault();

            DateTime start = rentreeAfterPrev != null ? rentreeAfterPrev.StartDate : defaultStart;

            DateTime end = next != null ? next.StartDate.AddDays(-1) : date.AddDays(60);

            string prevName = prev != null ? GetShortName(prev.Name) : "Rentrée";
            string nextName = next != null ? GetShortName(next.Name) : "Fin d'année";
            string title    = $"{prevName} → {nextName}";

            return (start, end, title);
        }

        public static string? GetLabel(DateTime date, List<Holiday> holidays)
        {
            if (holidays == null || holidays.Count == 0) return null;

            var vacations = holidays
                .Where(h => !h.Name.Contains("Rentree") && !h.Name.Contains("Rentrée"))
                .OrderBy(h => h.StartDate)
                .ToList();

            var current = vacations.FirstOrDefault(
                h => date.Date >= h.StartDate.Date && date.Date <= h.EndDate.Date);
            if (current != null)
                return $"Congé de {GetShortName(current.Name)}";

            var prev = vacations
                .Where(h => h.EndDate.Date < date.Date)
                .OrderByDescending(h => h.EndDate)
                .FirstOrDefault();

            var next = vacations
                .Where(h => h.StartDate.Date > date.Date)
                .OrderBy(h => h.StartDate)
                .FirstOrDefault();

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
        public int idCalendrier { get; set; }

        public string? nomEvenement { get; set; }

        public DateOnly dateDebut { get; set; }

        public DateOnly dateFin { get; set; }

        public string? typeEvenement { get; set; }

        public string? anneeScolaire { get; set; }
    }
}
