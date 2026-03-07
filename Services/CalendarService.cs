using Blazored.LocalStorage;
using Obrigenie.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace Obrigenie.Services
{
    // Responsable de la récupération, de la mise en cache et de la fourniture
    // des données du calendrier scolaire (périodes de congé et date de rentrée)
    // au reste de l'application.
    //
    // Flux de données :
    //   1. Tenter de récupérer le calendrier courant depuis le point d'API "api/values".
    //   2. Si l'API est accessible, mettre en cache le JSON brut dans le stockage local du navigateur.
    //   3. Si l'API est inaccessible, tenter de servir les données depuis le cache local.
    //   4. Si aucune source n'est disponible, générer un calendrier hors-ligne codé en dur.
    //
    // Ce service est enregistré en tant que Scoped dans Program.cs.
    public class CalendarService
    {
        // Le client HTTP préconfiguré avec l'adresse de base de l'API et le gestionnaire d'en-tête d'authentification.
        // Utilisé pour appeler "api/values" et récupérer les données d'événements de l'année scolaire.
        private readonly HttpClient _httpClient;

        // Le service de stockage local Blazored utilisé pour mettre en cache le JSON brut de la réponse API
        // afin que les données du calendrier soient disponibles lorsque le serveur est inaccessible.
        private readonly ILocalStorageService _localStorage;

        // La clé utilisée pour stocker et récupérer le JSON du calendrier sérialisé dans le stockage local.
        private const string CacheKey = "CachedCalendarData";

        // Initialise le service avec ses dépendances HTTP et de stockage local.
        public CalendarService(HttpClient httpClient, ILocalStorageService localStorage)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
        }

        // Retourne le calendrier scolaire en suivant la stratégie à trois niveaux :
        // API en direct → cache du stockage local → solution de repli hors-ligne codée en dur.
        // Le résultat est toujours une instance SchoolYearCalendar entièrement renseignée.
        public async Task<SchoolYearCalendar> GetCalendarData()
        {
            try
            {
                // Tentative de récupération des données en direct depuis l'API du serveur
                var apiData = await _httpClient.GetFromJsonAsync<List<ApiCalendrierDto>>("api/values");

                if (apiData != null && apiData.Count > 0)
                {
                    // Sérialiser la réponse brute de l'API et la conserver dans le stockage local pour une utilisation hors-ligne
                    await _localStorage.SetItemAsStringAsync(CacheKey, JsonSerializer.Serialize(apiData));

                    // Convertir la liste de DTO brute en modèle applicatif et la retourner
                    return ConvertApiDataToModel(apiData);
                }
            }
            catch (Exception ex)
            {
                // L'API peut être inaccessible (ex. : pas de réseau). Journaliser et passer au cache.
                Console.WriteLine($"[INFO] API unreachable ({ex.Message}). Attempting to read from cache...");
            }

            try
            {
                // Tentative de lecture des données du calendrier précédemment mises en cache dans le stockage local
                var cachedJson = await _localStorage.GetItemAsStringAsync(CacheKey);

                if (!string.IsNullOrEmpty(cachedJson))
                {
                    // Désérialiser le JSON mis en cache vers la liste de DTO
                    var cachedData = JsonSerializer.Deserialize<List<ApiCalendrierDto>>(cachedJson);

                    if (cachedData != null && cachedData.Count > 0)
                    {
                        // Convertir les données en cache en modèle applicatif et les retourner
                        return ConvertApiDataToModel(cachedData);
                    }
                }
            }
            catch
            {
                // Ignorer silencieusement les erreurs de lecture du cache ou de désérialisation ; passer en mode hors-ligne
            }

            // Dernier recours : générer un calendrier statique hors-ligne avec des dates scolaires belges codées en dur
            return GenerateOfflineCalendar();
        }

        // Convertit une liste de DTO bruts du calendrier API en modèle SchoolYearCalendar de l'application.
        // Lors de la conversion :
        //   - L'événement "Rentrée" de l'année scolaire courante est utilisé pour définir SchoolYearStart.
        //   - Des marqueurs de Rentrée sont injectés pour l'année courante et la suivante s'ils sont absents.
        private SchoolYearCalendar ConvertApiDataToModel(List<ApiCalendrierDto> apiData)
        {
            var allHolidays = new List<Holiday>();
            DateTime schoolYearStart = DateTime.MinValue;

            // Déterminer la chaîne "AAAA-AAAA" pour l'année scolaire courante (ex. : "2024-2025")
            string currentSchoolYearStr = GetCurrentSchoolYearString();

            foreach (var item in apiData)
            {
                // Trouver l'entrée Rentrée de l'année scolaire courante pour l'utiliser comme ancre de début d'année
                if (schoolYearStart == DateTime.MinValue &&
                    item.nomEvenement != null &&
                    item.nomEvenement.Contains("Rentree") &&
                    item.anneeScolaire == currentSchoolYearStr)
                {
                    // Convertir DateOnly en DateTime (minuit) pour des comparaisons cohérentes
                    schoolYearStart = item.dateDebut.ToDateTime(TimeOnly.MinValue);
                }

                // Ajouter chaque événement (congé ou Rentrée) à la liste des vacances
                allHolidays.Add(new Holiday
                {
                    Name      = item.nomEvenement ?? "Conge",
                    StartDate = item.dateDebut.ToDateTime(TimeOnly.MinValue),
                    EndDate   = item.dateFin.ToDateTime(TimeOnly.MinValue)
                });
            }

            // Si aucune Rentrée n'a été trouvée dans les données API, utiliser la date par défaut (26 août)
            if (schoolYearStart == DateTime.MinValue)
                schoolYearStart = GetDefaultRentreeDate(DateTime.Today.Year);

            // S'assurer que des marqueurs de Rentrée existent pour l'année scolaire courante et la suivante
            EnsureSchoolStartExists(allHolidays, GetDefaultRentreeDate(schoolYearStart.Year));
            EnsureSchoolStartExists(allHolidays, GetDefaultRentreeDate(schoolYearStart.Year + 1));

            return new SchoolYearCalendar { SchoolYearStart = schoolYearStart, Holidays = allHolidays };
        }

        // Génère un calendrier hors-ligne minimal en utilisant des dates de vacances scolaires belges codées en dur
        // lorsque l'API et le cache du stockage local sont tous deux indisponibles.
        // Couvre l'année scolaire courante et la suivante pour que la navigation fonctionne toujours.
        private SchoolYearCalendar GenerateOfflineCalendar()
        {
            // Déterminer les années de début et de fin de l'année scolaire courante
            var (startYear, endYear) = GetCurrentSchoolYear();

            var holidays = new List<Holiday>();

            // Ajouter les congés de repli pour l'année scolaire courante (ex. : 2024-2025)
            holidays.AddRange(GetFallbackHolidays(startYear, endYear));

            // Ajouter les congés de repli pour l'année scolaire suivante (ex. : 2025-2026)
            // afin que le calendrier fonctionne encore lors de la navigation vers l'année suivante
            holidays.AddRange(GetFallbackHolidays(startYear + 1, endYear + 1));

            // Déterminer la date de rentrée et s'assurer que les marqueurs de Rentrée sont présents
            DateTime rentreeCurrent = GetDefaultRentreeDate(startYear);
            EnsureSchoolStartExists(holidays, rentreeCurrent);

            DateTime rentreeNext = GetDefaultRentreeDate(startYear + 1);
            EnsureSchoolStartExists(holidays, rentreeNext);

            return new SchoolYearCalendar { SchoolYearStart = rentreeCurrent, Holidays = holidays };
        }

        // Ajoute un marqueur synthétique "Rentree scolaire" à la liste des congés si aucune entrée de ce type
        // n'existe déjà pour la date donnée.
        // Cela garantit que SchoolPeriodHelper trouve toujours une ancre de Rentrée lors du calcul
        // des limites de période, même lorsque les données de l'API sont incomplètes.
        private void EnsureSchoolStartExists(List<Holiday> holidays, DateTime rentreeDate)
        {
            // Ajouter une entrée Rentrée synthétique uniquement si aucune n'existe déjà pour cette date exacte
            if (!holidays.Any(h => h.Name.Contains("Rentree") && h.StartDate == rentreeDate))
            {
                holidays.Add(new Holiday
                {
                    Name      = "Rentree scolaire",
                    StartDate = rentreeDate,
                    EndDate   = rentreeDate  // La Rentrée est un marqueur d'un seul jour
                });
            }
        }

        // Retourne la date de rentrée par défaut pour une année donnée.
        // Par défaut le 26 août de l'année fournie, qui est généralement
        // la date de Rentrée la plus précoce possible dans le calendrier scolaire belge.
        private DateTime GetDefaultRentreeDate(int year) => new DateTime(year, 8, 26);

        // Détermine les années de début et de fin de l'année scolaire courante en fonction de la date du jour.
        // En Belgique, l'année scolaire va de septembre à juin :
        //   - Si aujourd'hui est en août ou après, l'année scolaire a commencé cette année civile.
        //   - Sinon, l'année scolaire a commencé l'année civile précédente.
        private (int startYear, int endYear) GetCurrentSchoolYear()
        {
            var today = DateTime.Today;

            // L'année scolaire commence en août (ou septembre), donc les mois 8-12 appartiennent à la nouvelle année
            if (today.Month >= 8) return (today.Year, today.Year + 1);

            // Les mois 1-7 appartiennent à une année scolaire qui a commencé l'année civile précédente
            return (today.Year - 1, today.Year);
        }

        // Retourne la chaîne d'identifiant de l'année scolaire au format "AAAA-AAAA"
        // (ex. : "2024-2025") pour l'année scolaire courante.
        // Cette chaîne correspond au champ "anneeScolaire" dans les DTO de réponse de l'API.
        private string GetCurrentSchoolYearString()
        {
            var (start, end) = GetCurrentSchoolYear();
            return $"{start}-{end}";
        }

        // Retourne une liste codée en dur des périodes de vacances scolaires belges typiques
        // pour une année scolaire couvrant les années civiles de début et de fin données.
        // Utilisé uniquement lorsque ni l'API ni le cache local ne sont disponibles.
        private List<Holiday> GetFallbackHolidays(int startYear, int endYear)
        {
            return new List<Holiday>
            {
                // Congé d'automne (Toussaint) — fin octobre à début novembre
                new() { Name = "Conge d'automne (Toussaint)",       StartDate = new DateTime(startYear, 10, 20), EndDate = new DateTime(startYear, 11, 3) },

                // Vacances d'hiver (Noël) — fin décembre à début janvier
                new() { Name = "Vacances d'hiver (Noel)",           StartDate = new DateTime(startYear, 12, 23), EndDate = new DateTime(endYear, 1, 4) },

                // Congé de détente (Carnaval) — mi-février à début mars
                new() { Name = "Conge de detente (Carnaval)",       StartDate = new DateTime(endYear, 2, 16),   EndDate = new DateTime(endYear, 3, 1) },

                // Vacances de printemps (Pâques) — début à mi-avril
                new() { Name = "Vacances de printemps (Paques)",    StartDate = new DateTime(endYear, 4, 6),    EndDate = new DateTime(endYear, 4, 19) },

                // Vacances d'été — début juillet à fin août
                new() { Name = "Vacances d'ete",                    StartDate = new DateTime(endYear, 7, 5),    EndDate = new DateTime(endYear, 8, 25) },
            };
        }
    }

    // Classe d'aide statique qui fournit des méthodes pour calculer les limites des périodes scolaires
    // et générer des étiquettes de période lisibles à partir d'une liste de périodes de vacances.
    // Utilisée par Index.razor pour déterminer la plage de dates de la vue "Trimestre" et pour
    // afficher l'étiquette de la barre de navigation (ex. : "Toussaint → Noël") au-dessus du calendrier.
    public static class SchoolPeriodHelper
    {
        // Associe un nom complet de congé à une étiquette d'affichage courte adaptée à la barre de navigation des périodes.
        // Retourne le nom original lorsqu'aucune correspondance spécifique n'est trouvée.
        private static string GetShortName(string holidayName)
        {
            if (holidayName.Contains("Toussaint") || holidayName.Contains("automne"))                                     return "Toussaint";
            if (holidayName.Contains("Noel") || holidayName.Contains("Noël") || holidayName.Contains("hiver"))            return "Noël";
            if (holidayName.Contains("Carnaval") || holidayName.Contains("detente") || holidayName.Contains("détente"))   return "Carnaval";
            if (holidayName.Contains("Paques") || holidayName.Contains("Pâques") || holidayName.Contains("printemps"))    return "Pâques";
            if (holidayName.Contains("ete") || holidayName.Contains("été") || holidayName.Contains("Ete") || holidayName.Contains("Été")) return "Été";
            if (holidayName.Contains("Rentree") || holidayName.Contains("Rentrée"))                                       return "Rentrée";

            // Aucune correspondance spécifique trouvée : retourner le nom original tel quel
            return holidayName;
        }

        // Calcule la date de début, la date de fin et la chaîne de titre de la période scolaire qui
        // contient la date donnée.
        //
        // Une "période scolaire" (trimestre) est le segment de jours d'école entre deux congés consécutifs.
        // Par exemple, la période entre Toussaint et Noël.
        //
        // Si la date donnée tombe pendant un congé, la fonction recule jusqu'au jour précédant
        // le début de ce congé, pour atterrir dans la période scolaire précédente.
        //
        // Si l'API fournit une entrée "Rentrée" immédiatement après le congé précédent,
        // cette date exacte est utilisée comme début de période (au lieu du lendemain de la fin du congé).
        public static (DateTime start, DateTime end, string title) GetPeriodBounds(
            DateTime date,
            List<Holiday> holidays)
        {
            // Ne travailler qu'avec de vraies périodes de congé ; les marqueurs de Rentrée sont traités séparément ci-dessous
            var vacations = holidays
                .Where(h => !h.Name.Contains("Rentree") && !h.Name.Contains("Rentrée"))
                .OrderBy(h => h.StartDate)
                .ToList();

            // Si la date de référence est pendant un congé, reculer jusqu'au jour précédent son début
            // afin que l'algorithme atterrisse dans la période scolaire précédente (plus utile)
            var inHoliday = vacations.FirstOrDefault(
                h => date.Date >= h.StartDate.Date && date.Date <= h.EndDate.Date);
            if (inHoliday != null)
                date = inHoliday.StartDate.AddDays(-1);

            // Trouver le congé qui s'est terminé le plus récemment avant la date (ajustée)
            var prev = vacations
                .Where(h => h.EndDate.Date < date.Date)
                .OrderByDescending(h => h.EndDate)
                .FirstOrDefault();

            // Trouver le prochain congé à venir après la date (ajustée)
            var next = vacations
                .Where(h => h.StartDate.Date > date.Date)
                .OrderBy(h => h.StartDate)
                .FirstOrDefault();

            // Début de période par défaut : le lendemain de la fin du congé précédent (ou 60 jours en arrière)
            DateTime defaultStart = prev != null ? prev.EndDate.AddDays(1) : date.AddDays(-60);

            // S'il existe un marqueur de Rentrée entre defaultStart et le prochain congé,
            // utiliser sa date comme début précis de la période
            var rentreeAfterPrev = holidays
                .Where(h => (h.Name.Contains("Rentree") || h.Name.Contains("Rentrée")) &&
                            h.StartDate.Date >= defaultStart.Date &&
                            (next == null || h.StartDate.Date < next.StartDate.Date))
                .OrderBy(h => h.StartDate)
                .FirstOrDefault();

            // Utiliser la date de Rentrée si disponible ; sinon utiliser le début par défaut
            DateTime start = rentreeAfterPrev != null ? rentreeAfterPrev.StartDate : defaultStart;

            // Fin de période : le jour précédant le début du prochain congé (ou 60 jours dans le futur)
            DateTime end = next != null ? next.StartDate.AddDays(-1) : date.AddDays(60);

            // Construire un titre lisible pour la période (ex. : "Toussaint → Noël")
            string prevName = prev != null ? GetShortName(prev.Name) : "Rentrée";
            string nextName = next != null ? GetShortName(next.Name) : "Fin d'année";
            string title    = $"{prevName} → {nextName}";

            return (start, end, title);
        }

        // Retourne une étiquette courte et lisible décrivant le contexte de la période scolaire d'une date donnée.
        // Cette étiquette est affichée dans la bannière étroite sous la barre d'outils de mode d'affichage
        // dans les vues semaine/mois.
        //
        // Formats d'étiquette possibles :
        //   - "Congé de Toussaint"         (la date est pendant un congé)
        //   - "Toussaint → Noël"           (la date est entre deux congés connus)
        //   - "Rentrée → Toussaint"        (la date est avant le premier congé connu)
        //   - "Après Été"                  (la date est après le dernier congé connu)
        //   - null                          (aucune donnée de congé disponible)
        public static string? GetLabel(DateTime date, List<Holiday> holidays)
        {
            // Retourner null immédiatement s'il n'y a pas de données de congé avec lesquelles travailler
            if (holidays == null || holidays.Count == 0) return null;

            // Ne travailler qu'avec de vraies périodes de vacances, pas les marqueurs de Rentrée (qui sont sur un seul jour)
            var vacations = holidays
                .Where(h => !h.Name.Contains("Rentree") && !h.Name.Contains("Rentrée"))
                .OrderBy(h => h.StartDate)
                .ToList();

            // Vérifier si la date donnée tombe pendant un congé actif
            var current = vacations.FirstOrDefault(
                h => date.Date >= h.StartDate.Date && date.Date <= h.EndDate.Date);
            if (current != null)
                return $"Congé de {GetShortName(current.Name)}";

            // Trouver le congé passé le plus récent et le prochain congé à venir
            var prev = vacations
                .Where(h => h.EndDate.Date < date.Date)
                .OrderByDescending(h => h.EndDate)
                .FirstOrDefault();

            var next = vacations
                .Where(h => h.StartDate.Date > date.Date)
                .OrderBy(h => h.StartDate)
                .FirstOrDefault();

            // Un congé précédent et un congé suivant sont tous deux connus : afficher la flèche de transition
            if (prev != null && next != null)
                return $"{GetShortName(prev.Name)} → {GetShortName(next.Name)}";

            // Seul un congé futur est connu (la date est avant qu'une vacance se soit produite)
            if (next != null)
                return $"Rentrée → {GetShortName(next.Name)}";

            // Seul un congé passé est connu (la date est après le dernier congé connu)
            if (prev != null)
                return $"Après {GetShortName(prev.Name)}";

            // Aucun congé environnant trouvé du tout
            return null;
        }
    }

    // Objet de transfert de données brutes qui correspond à la structure JSON retournée par le point d'API "api/values".
    // Chaque instance représente un événement de l'année scolaire (congé, Rentrée, etc.) tel que stocké
    // dans la base de données du serveur.
    public class ApiCalendrierDto
    {
        // Le nom de l'événement du calendrier scolaire (ex. : "Conge d'automne (Toussaint)",
        // "Rentree scolaire"). Utilisé pour classer les événements et générer des étiquettes d'affichage courtes.
        public string? nomEvenement { get; set; }

        // Le premier jour de la période de l'événement, stocké en tant que valeur DateOnly (sans composante horaire).
        public DateOnly dateDebut { get; set; }

        // Le dernier jour de la période de l'événement, stocké en tant que valeur DateOnly (sans composante horaire).
        public DateOnly dateFin { get; set; }

        // Le type ou la catégorie de l'événement tel que retourné par l'API
        // (ex. : "CONGE", "RENTREE"). Peut être null.
        public string? typeEvenement { get; set; }

        // L'année scolaire à laquelle appartient cet événement, au format "AAAA-AAAA"
        // (ex. : "2024-2025"). Utilisé pour identifier la Rentrée de l'année scolaire courante.
        public string? anneeScolaire { get; set; }
    }
}
