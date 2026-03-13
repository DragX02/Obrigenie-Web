using Obrigenie.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Obrigenie.Services
{
    // Service HTTP central pour le client Blazor WebAssembly Obrigenie.
    // Encapsule toute la communication avec l'API REST du backend et expose des méthodes
    // fortement typées pour l'authentification, les cours, les notes, la gestion des licences,
    // le déclenchement du scraper du calendrier scolaire et les données de référence en cascade
    // (cours / niveaux / domaines).
    //
    // Chaque méthode gère ses propres exceptions et retourne une valeur par défaut sûre (null,
    // false ou liste vide) plutôt que de propager les exceptions au composant appelant, rendant
    // l'interface résistante aux pannes réseau transitoires.
    //
    // Le HttpClient sous-jacent est injecté via la fabrique nommée "API" enregistrée dans
    // Program.cs. AuthHeaderHandler attache automatiquement le jeton JWT Bearer à chaque requête,
    // donc ce service n'a pas besoin de gérer les en-têtes d'authentification manuellement.
    public class ApiService
    {
        // Client HTTP préconfiguré avec l'adresse de base de l'API et le gestionnaire d'auth JWT.
        private readonly HttpClient _httpClient;

        // Fournit un accès direct au jeton JWT stocké pour les endpoints qui nécessitent
        // l'en-tête Bearer ajouté manuellement (en complément d'AuthHeaderHandler).
        private readonly AuthService _auth;

        // Initialise le service avec le client HTTP et le service d'auth fournis par l'injection de dépendances.
        // httpClient : le client HTTP utilisé pour tous les appels API.
        // auth : le service d'auth utilisé pour lire le jeton JWT depuis localStorage.
        public ApiService(HttpClient httpClient, AuthService auth)
        {
            _httpClient = httpClient;
            _auth = auth;
        }

        // ──────────────────────────────────────────────────────────────────
        // AUTHENTIFICATION
        // ──────────────────────────────────────────────────────────────────

        // Échange le cookie HttpOnly temporaire "auth_pending" (défini par le serveur après une
        // redirection OAuth réussie) contre le jeton JWT et les données utilisateur de l'application.
        // Le jeton n'est jamais transmis via l'URL ; cet endpoint lit le cookie côté serveur
        // et retourne le payload d'auth directement au client.
        // Appelé par AuthCallback.razor immédiatement après la redirection du fournisseur OAuth.
        // Endpoint : GET api/auth/exchange
        // Retourne un AuthResponse avec le JWT et les détails utilisateur en cas de succès ; null en cas d'échec.
        public async Task<AuthResponse?> ExchangeOAuthTokenAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/auth/exchange");

                // Retourne l'AuthResponse désérialisé en cas de succès ; null pour tout statut non-succès
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<AuthResponse>();

                return null;
            }
            catch
            {
                // Les erreurs réseau ou de sérialisation retournent null ; l'appelant gère la redirection
                return null;
            }
        }

        // Envoie les identifiants email/mot de passe à l'endpoint de connexion et retourne la réponse
        // d'authentification du serveur si les identifiants sont valides.
        // Endpoint : POST api/auth/login
        // loginDto : DTO contenant l'email et le mot de passe de l'utilisateur.
        // Retourne un AuthResponse avec le JWT et les détails utilisateur en cas de succès ;
        // null si les identifiants sont invalides ou en cas d'erreur réseau.
        public async Task<AuthResponse?> LoginAsync(LoginDto loginDto)
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginDto);

            // Désérialise et retourne le payload du jeton sur HTTP 200 ; null pour tout autre statut
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<AuthResponse>();

            return null;
        }

        // Envoie les données d'inscription au serveur pour créer un nouveau compte utilisateur.
        // Le serveur valide la robustesse du mot de passe et l'unicité de l'email.
        // Endpoint : POST api/auth/register
        // registerDto : DTO avec tous les champs d'inscription incluant la confirmation du mot de passe.
        // Retourne un AuthResponse (et JWT) pour le nouveau compte en cas de succès ;
        // null en cas d'erreur de validation ou de panne réseau.
        public async Task<AuthResponse?> RegisterAsync(RegisterDto registerDto)
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/register", registerDto);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<AuthResponse>();

            return null;
        }

        // ──────────────────────────────────────────────────────────────────
        // COURS
        // ──────────────────────────────────────────────────────────────────

        // Récupère la liste des cours planifiés pour une date calendaire spécifique.
        // Le serveur filtre les cours récurrents par leur masque de bits DaysOfWeek et la plage de dates.
        // Endpoint : GET api/courses/date/{yyyy-MM-dd}
        // date : la date pour laquelle charger les cours.
        // Retourne une liste de cours pour cette date, ou une liste vide en cas d'erreur.
        public async Task<List<Course>> GetCoursesForDateAsync(DateTime date)
        {
            try
            {
                // Formate la date en ISO 8601 (yyyy-MM-dd) tel qu'attendu par la route API
                return await _httpClient.GetFromJsonAsync<List<Course>>(
                    $"api/courses/date/{date:yyyy-MM-dd}") ?? new();
            }
            catch
            {
                // Retourne une liste vide pour que le calendrier s'affiche sans planter en cas d'erreur API
                return new();
            }
        }

        // Récupère toutes les notes utilisateur dont la date se situe dans la plage de dates inclusive donnée.
        // Utilisé par les vues semaine et mois pour charger les notes de tous les jours affichés en une seule
        // requête, ce qui est plus efficace qu'une requête par jour.
        // Endpoint : GET api/notes/range?start={yyyy-MM-dd}&end={yyyy-MM-dd}
        // start : la première date de la plage (inclusive).
        // end : la dernière date de la plage (inclusive).
        // Retourne toutes les notes dont la date est dans [start, end], ou une liste vide en cas d'erreur.
        public async Task<List<Note>> GetNotesForRangeAsync(DateTime start, DateTime end)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<Note>>(
                    $"api/notes/range?start={start:yyyy-MM-dd}&end={end:yyyy-MM-dd}") ?? new();
            }
            catch
            {
                return new();
            }
        }

        // Sauvegarde un nouveau cours ou met à jour un cours existant en le postant à l'endpoint des cours.
        // Le serveur détermine création ou mise à jour selon la valeur de Course.Id.
        // Endpoint : POST api/courses
        // course : le modèle de cours à créer ou mettre à jour.
        public async Task SaveCourseAsync(Course course)
        {
            await _httpClient.PostAsJsonAsync("api/courses", course);
        }

        // Supprime définitivement un cours par son identifiant assigné par le serveur.
        // Endpoint : DELETE api/courses/{id}
        // id : l'identifiant unique du cours à supprimer.
        public async Task DeleteCourseAsync(int id)
        {
            await _httpClient.DeleteAsync($"api/courses/{id}");
        }

        // ──────────────────────────────────────────────────────────────────
        // NOTES
        // ──────────────────────────────────────────────────────────────────

        // Récupère toutes les notes pour une seule date spécifique.
        // Utilisé par la vue journalière lorsqu'on n'a besoin que des notes d'un seul jour.
        // Endpoint : GET api/notes/date/{yyyy-MM-dd}
        // date : la date pour laquelle charger les notes.
        // Retourne toutes les notes pour cette date, ou une liste vide en cas d'erreur.
        public async Task<List<Note>> GetNotesForDateAsync(DateTime date)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<Note>>(
                    $"api/notes/date/{date:yyyy-MM-dd}") ?? new();
            }
            catch
            {
                return new();
            }
        }

        // Crée une nouvelle note ou met à jour une existante (déterminé par Note.Id valant 0 ou non).
        // Retourne un indicateur de succès et un message d'erreur optionnel pour que l'interface
        // puisse afficher un retour inline.
        // Endpoint : POST api/notes
        // note : la note à sauvegarder. Id == 0 signifie une nouvelle note.
        // Retourne (true, null) en cas de succès ;
        // (false, messageErreur) si le serveur retourne un statut non-succès ou en cas d'erreur réseau.
        public async Task<(bool Success, string? Error)> SaveNoteAsync(Note note)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/notes", note);

                if (response.IsSuccessStatusCode) return (true, null);

                // Lit le corps brut de la réponse pour inclure la description d'erreur du serveur
                var body = await response.Content.ReadAsStringAsync();
                return (false, $"Error {(int)response.StatusCode}: {body}");
            }
            catch (Exception ex)
            {
                // Panne réseau ou de sérialisation : remonte le message de l'exception
                return (false, ex.Message);
            }
        }

        // Supprime définitivement une note par son identifiant assigné par le serveur.
        // Endpoint : DELETE api/notes/{id}
        // id : l'identifiant unique de la note à supprimer.
        public async Task DeleteNoteAsync(int id)
        {
            await _httpClient.DeleteAsync($"api/notes/{id}");
        }

        // ──────────────────────────────────────────────────────────────────
        // LICENCE — VALIDATION ET VÉRIFICATION
        // ──────────────────────────────────────────────────────────────────

        // Soumet un code d'accès de licence saisi par l'utilisateur sur AccessCodePage pour validation initiale.
        // Si valide, le code est stocké en localStorage et utilisé pour les appels CheckLicenseAsync suivants.
        // Endpoint : POST api/access/validate  (body: { code })
        // code : la chaîne de code d'accès saisie par l'utilisateur.
        // Retourne true si le serveur accepte le code comme valide ; false sinon.
        public async Task<bool> ValidateAccessCodeAsync(string code)
        {
            try
            {
                // Envoie le code comme objet JSON avec une seule propriété "code"
                var response = await _httpClient.PostAsJsonAsync("api/access/validate", new { code });
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // Valide qu'un code de licence précédemment accepté est toujours actif sur le serveur.
        // Appelé à chaque démarrage de l'application (dans MainLayout) pour appliquer la révocation
        // de licence en temps réel : si un admin révoque une licence, l'utilisateur est redirigé
        // vers la page de code d'accès au prochain chargement.
        // Endpoint : GET api/access/check?code={code}
        // code : le code de licence stocké en localStorage.
        // Retourne true si la licence est toujours active ; false si révoquée ou introuvable.
        public async Task<bool> CheckLicenseAsync(string code)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"api/access/check?code={Uri.EscapeDataString(code)}");

                if (!response.IsSuccessStatusCode) return false;

                // Le serveur retourne { "valid": true/false } comme corps de réponse
                var result = await response.Content.ReadFromJsonAsync<LicenseCheckResult>();
                return result?.Valid == true;
            }
            catch
            {
                return false;
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // ADMIN — GESTION DES LICENCES
        // ──────────────────────────────────────────────────────────────────

        // Récupère la liste complète de tous les enregistrements de licences pour affichage dans la page admin.
        // Accessible uniquement aux utilisateurs ayant le rôle ADMIN (appliqué côté serveur).
        // Endpoint : GET api/admin/licenses
        // Retourne une liste de tous les enregistrements LicenseDto, ou une liste vide en cas d'erreur.
        public async Task<List<LicenseDto>> GetLicensesAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<LicenseDto>>("api/admin/licenses") ?? new();
            }
            catch
            {
                return new();
            }
        }

        // Crée une nouvelle licence avec un libellé optionnel, une date d'expiration et un code personnalisé.
        // Si aucun code personnalisé n'est fourni, le serveur en génère un automatiquement.
        // Endpoint : POST api/admin/licenses  (body: { code, label, expiresAt })
        // label : un libellé lisible optionnel (ex. : "PROF-DUPONT").
        // expiresAt : une date d'expiration optionnelle après laquelle la licence devient inactive.
        // code : une chaîne de code personnalisée optionnelle ; null laisse le serveur générer automatiquement.
        // Retourne (LicenseDto, null) en cas de succès ;
        // (null, messageErreur) si la création échoue, avec le message d'erreur du serveur si disponible.
        public async Task<(LicenseDto? License, string? Error)> CreateLicenseAsync(
            string? label, DateTime? expiresAt, string? code = null)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    "api/admin/licenses", new { code, label, expiresAt });

                if (response.IsSuccessStatusCode)
                    return (await response.Content.ReadFromJsonAsync<LicenseDto>(), null);

                // Tente d'extraire le message d'erreur structuré depuis le corps JSON de la réponse
                try
                {
                    var err = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                    var msg = err.GetProperty("message").GetString();
                    return (null, msg ?? $"Error {(int)response.StatusCode}");
                }
                catch
                {
                    // Si le corps ne peut pas être parsé en JSON, repli sur le code de statut
                    return (null, $"Error {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        // Révoque une licence active, empêchant tout utilisateur assigné à ce code d'accéder à l'application.
        // Endpoint : PUT api/admin/licenses/{id}/revoke  (sans body)
        // id : l'identifiant unique de la licence à révoquer.
        // Retourne true si le serveur a accepté la révocation ; false en cas d'échec.
        public async Task<bool> RevokeLicenseAsync(int id)
        {
            try
            {
                // PUT avec un body null — la route elle-même identifie l'action et la cible
                var response = await _httpClient.PutAsync($"api/admin/licenses/{id}/revoke", null);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // Réactive une licence précédemment révoquée.
        // Endpoint : PUT api/admin/licenses/{id}/reactivate  (sans body)
        // id : l'identifiant unique de la licence à réactiver.
        // Retourne true si la réactivation a été acceptée ; false en cas d'échec.
        public async Task<bool> ReactivateLicenseAsync(int id)
        {
            try
            {
                var response = await _httpClient.PutAsync($"api/admin/licenses/{id}/reactivate", null);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // Supprime définitivement un enregistrement de licence de la base de données.
        // Endpoint : DELETE api/admin/licenses/{id}
        // id : l'identifiant unique de la licence à supprimer.
        // Retourne true si la suppression a été acceptée ; false en cas d'échec.
        public async Task<bool> DeleteLicenseAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/admin/licenses/{id}");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // ADMIN — SCRAPER DU CALENDRIER SCOLAIRE
        // ──────────────────────────────────────────────────────────────────

        // Déclenche le scraper côté serveur qui récupère les dernières dates de vacances scolaires
        // depuis le site officiel enseignement.be et met à jour la base de données.
        // Cette opération peut prendre plusieurs secondes ; l'interface désactive le bouton pendant son exécution.
        // Endpoint : GET api/update-scolaire
        // Retourne (true, messageSucces) lorsque le scraper se termine sans erreur ;
        // (false, descriptionErreur) lorsque le scraper échoue ou en cas d'erreur réseau.
        public async Task<(bool Success, string Message)> TriggerScraperAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/update-scolaire");

                if (response.IsSuccessStatusCode)
                    return (true, await response.Content.ReadAsStringAsync());

                return (false, $"Error {(int)response.StatusCode}");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // VÉRIFICATION DE SANTÉ
        // ──────────────────────────────────────────────────────────────────

        // Effectue un ping de vérification de santé léger vers le serveur pour déterminer si le
        // backend est accessible. Le résultat pilote le badge en ligne/hors ligne dans MainLayout.
        // Endpoint : GET api/health
        // Retourne true si le serveur répond avec un statut 2xx ; false sinon.
        public async Task<bool> CheckHealthAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // RÉFÉRENTIEL — LECTEUR DE PDF
        // ──────────────────────────────────────────────────────────────────

        // URL de base de l'API (ex. : "http://localhost:5276/") utilisée par la page
        // ReferentielPage pour construire les URLs des fichiers PDF à afficher.
        public string BaseUrl => _httpClient.BaseAddress?.ToString() ?? "";

        // Récupère la liste des noms de fichiers PDF disponibles dans le dossier Referentiel du serveur.
        // Endpoint : GET api/referentiel
        // Retourne la liste triée des noms de fichiers, ou une liste vide en cas d'erreur.
        public async Task<List<string>> GetReferentielListAsync()
        {
            try
            {
                var request = await BuildAuthRequest(HttpMethod.Get, "api/referentiel");
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return new();
                return await response.Content.ReadFromJsonAsync<List<string>>() ?? new();
            }
            catch
            {
                return new();
            }
        }

        // Télécharge le contenu binaire d'un fichier PDF depuis le serveur.
        // Le tableau d'octets est ensuite passé au JS via IJSRuntime pour créer une URL blob
        // et l'afficher dans un iframe sans exposer le token JWT dans l'URL.
        // Endpoint : GET api/referentiel/{nomFichier}
        // nomFichier : le nom du fichier PDF à télécharger (ex. : "referentiel-maths.pdf").
        // Retourne le contenu du fichier en octets, ou null en cas d'erreur.
        public async Task<byte[]?> GetReferentielPdfAsync(string nomFichier)
        {
            try
            {
                var request = await BuildAuthRequest(HttpMethod.Get, $"api/referentiel/{Uri.EscapeDataString(nomFichier)}");
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadAsByteArrayAsync();
            }
            catch
            {
                return null;
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // DONNÉES ADMIN — GESTION DES TABLES PÉDAGOGIQUES
        // ──────────────────────────────────────────────────────────────────

        // Envoie un GET authentifié et désérialise la liste retournée.
        // Retourne une liste vide en cas d'erreur réseau ou de statut non-succès.
        private async Task<List<T>> AdminGetListAsync<T>(string url)
        {
            try
            {
                var req = await BuildAuthRequest(HttpMethod.Get, url);
                var res = await _httpClient.SendAsync(req);
                if (!res.IsSuccessStatusCode) return new();
                return await res.Content.ReadFromJsonAsync<List<T>>() ?? new();
            }
            catch { return new(); }
        }

        // Envoie un POST authentifié avec un body JSON sérialisé.
        // Retourne (true, null) en cas de succès, (false, messageErreur) sinon.
        private async Task<(bool Ok, string? Err)> AdminPostAsync<T>(string url, T body)
        {
            try
            {
                var req = await BuildAuthRequest(HttpMethod.Post, url);
                req.Content = JsonContent.Create(body);
                var res = await _httpClient.SendAsync(req);
                if (res.IsSuccessStatusCode) return (true, null);
                try
                {
                    var json = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                    return (false, json.GetProperty("message").GetString() ?? $"Erreur {(int)res.StatusCode}");
                }
                catch { return (false, $"Erreur {(int)res.StatusCode}"); }
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        // Envoie un DELETE authentifié sur l'URL donnée.
        // Retourne (true, null) en cas de succès, (false, messageErreur) sinon.
        private async Task<(bool Ok, string? Err)> AdminDeleteAsync(string url)
        {
            try
            {
                var req = await BuildAuthRequest(HttpMethod.Delete, url);
                var res = await _httpClient.SendAsync(req);
                if (res.IsSuccessStatusCode) return (true, null);
                try
                {
                    var json = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                    return (false, json.GetProperty("message").GetString() ?? $"Erreur {(int)res.StatusCode}");
                }
                catch { return (false, $"Erreur {(int)res.StatusCode}"); }
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        // Catégories
        public Task<List<CategorieAdminDto>>      GetAdminCategoriesAsync()                   => AdminGetListAsync<CategorieAdminDto>("api/admin-data/categories");
        public Task<(bool Ok, string? Err)>       CreateAdminCategorieAsync(object dto)        => AdminPostAsync("api/admin-data/categories", dto);
        public Task<(bool Ok, string? Err)>       DeleteAdminCategorieAsync(int id)            => AdminDeleteAsync($"api/admin-data/categories/{id}");

        // Cours
        public Task<List<CoursAdminDto>>          GetAdminCoursAsync()                        => AdminGetListAsync<CoursAdminDto>("api/admin-data/cours");
        public Task<(bool Ok, string? Err)>       CreateAdminCoursAsync(object dto)            => AdminPostAsync("api/admin-data/cours", dto);
        public Task<(bool Ok, string? Err)>       DeleteAdminCoursAsync(int id)               => AdminDeleteAsync($"api/admin-data/cours/{id}");

        // Niveaux
        public Task<List<NiveauAdminDto>>         GetAdminNiveauxAsync()                      => AdminGetListAsync<NiveauAdminDto>("api/admin-data/niveaux");
        public Task<(bool Ok, string? Err)>       CreateAdminNiveauAsync(object dto)           => AdminPostAsync("api/admin-data/niveaux", dto);
        public Task<(bool Ok, string? Err)>       DeleteAdminNiveauAsync(int id)              => AdminDeleteAsync($"api/admin-data/niveaux/{id}");

        // Professeurs (lecture seule)
        public Task<List<ProfesseurAdminDto>>     GetAdminProfesseursAsync()                  => AdminGetListAsync<ProfesseurAdminDto>("api/admin-data/professeurs");

        // Liaisons Cours-Niveau
        public Task<List<CoursNiveauAdminDto>>    GetAdminCoursNiveauxAsync()                 => AdminGetListAsync<CoursNiveauAdminDto>("api/admin-data/cours-niveaux");
        public Task<(bool Ok, string? Err)>       CreateAdminCoursNiveauAsync(object dto)      => AdminPostAsync("api/admin-data/cours-niveaux", dto);
        public Task<(bool Ok, string? Err)>       DeleteAdminCoursNiveauAsync(int id)         => AdminDeleteAsync($"api/admin-data/cours-niveaux/{id}");

        // Domaines
        public Task<List<DomaineAdminDto>>        GetAdminDomainesAsync()                     => AdminGetListAsync<DomaineAdminDto>("api/admin-data/domaines");
        public Task<(bool Ok, string? Err)>       CreateAdminDomaineAsync(object dto)          => AdminPostAsync("api/admin-data/domaines", dto);
        public Task<(bool Ok, string? Err)>       DeleteAdminDomaineAsync(int id)             => AdminDeleteAsync($"api/admin-data/domaines/{id}");

        // Compétences
        public Task<List<CompetenceAdminDto>>     GetAdminCompetencesAsync()                  => AdminGetListAsync<CompetenceAdminDto>("api/admin-data/competences");
        public Task<(bool Ok, string? Err)>       CreateAdminCompetenceAsync(object dto)       => AdminPostAsync("api/admin-data/competences", dto);
        public Task<(bool Ok, string? Err)>       DeleteAdminCompetenceAsync(int id)          => AdminDeleteAsync($"api/admin-data/competences/{id}");

        // Aptitudes
        public Task<List<AptitudeAdminDto>>       GetAdminAptitudesAsync()                    => AdminGetListAsync<AptitudeAdminDto>("api/admin-data/aptitudes");
        public Task<(bool Ok, string? Err)>       CreateAdminAptitudeAsync(object dto)         => AdminPostAsync("api/admin-data/aptitudes", dto);
        public Task<(bool Ok, string? Err)>       DeleteAdminAptitudeAsync(int id)            => AdminDeleteAsync($"api/admin-data/aptitudes/{id}");

        // Noms de visées
        public Task<List<NomViseeAdminDto>>       GetAdminNomViseesAsync()                    => AdminGetListAsync<NomViseeAdminDto>("api/admin-data/nom-visees");
        public Task<(bool Ok, string? Err)>       CreateAdminNomViseeAsync(object dto)         => AdminPostAsync("api/admin-data/nom-visees", dto);
        public Task<(bool Ok, string? Err)>       DeleteAdminNomViseeAsync(int id)            => AdminDeleteAsync($"api/admin-data/nom-visees/{id}");

        // Visées à maîtriser
        public Task<List<ViseesMaitriserAdminDto>> GetAdminViseesMaitriserAsync()              => AdminGetListAsync<ViseesMaitriserAdminDto>("api/admin-data/visees-maitriser");
        public Task<(bool Ok, string? Err)>        CreateAdminViseesMaitriserAsync(object dto) => AdminPostAsync("api/admin-data/visees-maitriser", dto);
        public Task<(bool Ok, string? Err)>        DeleteAdminViseesMaitriserAsync(int id)     => AdminDeleteAsync($"api/admin-data/visees-maitriser/{id}");

        // Sous-domaines
        public Task<List<SousDomaineAdminDto>>    GetAdminSousDomainesAsync()                 => AdminGetListAsync<SousDomaineAdminDto>("api/admin-data/sous-domaines");
        public Task<(bool Ok, string? Err)>       CreateAdminSousDomaineAsync(object dto)      => AdminPostAsync("api/admin-data/sous-domaines", dto);
        public Task<(bool Ok, string? Err)>       DeleteAdminSousDomaineAsync(int id)         => AdminDeleteAsync($"api/admin-data/sous-domaines/{id}");

        // Visées
        public Task<List<ViseeAdminDto>>          GetAdminViseesAsync()                       => AdminGetListAsync<ViseeAdminDto>("api/admin-data/visees");
        public Task<(bool Ok, string? Err)>       CreateAdminViseeAsync(object dto)            => AdminPostAsync("api/admin-data/visees", dto);
        public Task<(bool Ok, string? Err)>       DeleteAdminViseeAsync(int id)               => AdminDeleteAsync($"api/admin-data/visees/{id}");

        // Liaisons visée ↔ visée à maîtriser
        public Task<List<LienViseeMaitriseAdminDto>> GetAdminLiensViseeMaitriseAsync()         => AdminGetListAsync<LienViseeMaitriseAdminDto>("api/admin-data/lien-visee-maitrise");
        public Task<(bool Ok, string? Err)>          CreateAdminLienViseeMaitriseAsync(object dto) => AdminPostAsync("api/admin-data/lien-visee-maitrise", dto);
        public Task<(bool Ok, string? Err)>          DeleteAdminLienViseeMaitriseAsync(int idVisee, int idVm) => AdminDeleteAsync($"api/admin-data/lien-visee-maitrise/{idVisee}/{idVm}");

        // Liaisons visée_maitriser ↔ aptitude ↔ compétence
        public Task<List<AppartenirAdminDto>>     GetAdminAppartenirAsync()                   => AdminGetListAsync<AppartenirAdminDto>("api/admin-data/appartenir-visee-aptitude");
        public Task<(bool Ok, string? Err)>       CreateAdminAppartenirAsync(object dto)       => AdminPostAsync("api/admin-data/appartenir-visee-aptitude", dto);
        public Task<(bool Ok, string? Err)>       DeleteAdminAppartenirAsync(int id)          => AdminDeleteAsync($"api/admin-data/appartenir-visee-aptitude/{id}");

        // ──────────────────────────────────────────────────────────────────
        // DONNÉES DE RÉFÉRENCE — LISTES DÉROULANTES EN CASCADE (TestPage)
        // ──────────────────────────────────────────────────────────────────

        // Construit un HttpRequestMessage avec l'en-tête Authorization Bearer défini depuis localStorage.
        // Utilisé par les endpoints de référence comme solution de repli pour s'assurer que le jeton
        // est toujours attaché, que AuthHeaderHandler ait déjà renseigné l'en-tête ou non.
        // method : la méthode HTTP (GET, POST, etc.)
        // url : l'URL relative de l'endpoint API.
        private async Task<HttpRequestMessage> BuildAuthRequest(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);
            // Lit le jeton JWT stocké et l'attache comme en-tête Authorization Bearer
            var token = await _auth.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return request;
        }

        // Récupère la liste principale de toutes les catégories de matières depuis la base de données de référence.
        // Utilisé pour peupler la première liste déroulante (niveau supérieur) de la page de sélection en cascade.
        // Endpoint : GET api/ref/categories
        // Lève HttpRequestException si le serveur retourne un code de statut non-succès.
        // Retourne une liste de CategorieDto triée par ordre d'affichage.
        public async Task<List<CategorieDto>> GetCategoriesAsync()
        {
            var request = await BuildAuthRequest(HttpMethod.Get, "api/ref/categories");
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"api/ref/categories a retourné {(int)response.StatusCode} {response.StatusCode}.");
            return await response.Content.ReadFromJsonAsync<List<CategorieDto>>() ?? new();
        }

        // Récupère tous les cours (matières) appartenant à une catégorie spécifique.
        // Utilisé pour peupler la deuxième liste déroulante après qu'une catégorie a été sélectionnée.
        // Endpoint : GET api/ref/cours/{idCat}
        // Lève HttpRequestException si le serveur retourne un code de statut non-succès.
        // idCat : la clé primaire de la catégorie sélectionnée.
        // Retourne une liste de CoursDto.
        public async Task<List<CoursDto>> GetCoursAsync(int idCat)
        {
            var request = await BuildAuthRequest(HttpMethod.Get, $"api/ref/cours/{idCat}");
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"api/ref/cours/{idCat} a retourné {(int)response.StatusCode} {response.StatusCode}.");
            return await response.Content.ReadFromJsonAsync<List<CoursDto>>() ?? new();
        }

        // Récupère les niveaux disponibles pour un code de cours spécifique.
        // Appelé lorsque l'utilisateur sélectionne un cours dans la première liste déroulante de la page de test.
        // Endpoint : GET api/ref/niveaux/{codeCours}
        // Lève HttpRequestException si le serveur retourne un code de statut non-succès.
        // codeCours : le code de cours (ex. : "LM", "SC") pour filtrer les niveaux.
        // Retourne une liste de NiveauDto.
        public async Task<List<NiveauDto>> GetNiveauxAsync(string codeCours)
        {
            var request = await BuildAuthRequest(HttpMethod.Get, $"api/ref/niveaux/{codeCours}");
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"api/ref/niveaux/{codeCours} a retourné {(int)response.StatusCode} {response.StatusCode}.");
            return await response.Content.ReadFromJsonAsync<List<NiveauDto>>() ?? new();
        }

        // Récupère les domaines disponibles pour une combinaison cours et niveau spécifique.
        // Appelé lorsque l'utilisateur sélectionne un niveau dans la deuxième liste déroulante de la page de test.
        // Endpoint : GET api/ref/domaines/{codeCours}/{codeNiveau}
        // Lève HttpRequestException si le serveur retourne un code de statut non-succès.
        // codeCours : le code de cours utilisé pour filtrer les domaines.
        // codeNiveau : le code de niveau utilisé conjointement avec le cours pour filtrer les domaines.
        // Retourne une liste de DomaineDto.
        public async Task<List<DomaineDto>> GetDomainesAsync(string codeCours, string codeNiveau)
        {
            var request = await BuildAuthRequest(HttpMethod.Get, $"api/ref/domaines/{codeCours}/{codeNiveau}");
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"api/ref/domaines/{codeCours}/{codeNiveau} a retourné {(int)response.StatusCode} {response.StatusCode}.");
            return await response.Content.ReadFromJsonAsync<List<DomaineDto>>() ?? new();
        }

        // Retourne les sous-domaines d'un domaine donné.
        // Endpoint : GET api/ref/sous-domaines/{idDomaine}
        public async Task<List<SousDomaineRefDto>> GetSousDomainesRefAsync(int idDomaine)
        {
            var request = await BuildAuthRequest(HttpMethod.Get, $"api/ref/sous-domaines/{idDomaine}");
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"api/ref/sous-domaines/{idDomaine} a retourné {(int)response.StatusCode} {response.StatusCode}.");
            return await response.Content.ReadFromJsonAsync<List<SousDomaineRefDto>>() ?? new();
        }

        // Retourne les visées d'un domaine, filtrées optionnellement par sous-domaine.
        // Endpoint : GET api/ref/visees/{idDomaine}?sousDomaine={idSousDomaine}
        public async Task<List<ViseeRefDto>> GetViseesRefAsync(int idDomaine, int? idSousDomaine = null)
        {
            var url = $"api/ref/visees/{idDomaine}";
            if (idSousDomaine.HasValue && idSousDomaine.Value > 0)
                url += $"?sousDomaine={idSousDomaine.Value}";
            var request = await BuildAuthRequest(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"{url} a retourné {(int)response.StatusCode} {response.StatusCode}.");
            return await response.Content.ReadFromJsonAsync<List<ViseeRefDto>>() ?? new();
        }

        // Retourne les visées à maîtriser liées à une visée donnée.
        // Endpoint : GET api/ref/visees-maitriser/{idVisee}
        public async Task<List<ViseesMaitriserRefDto>> GetViseesMaitriserRefAsync(int idVisee)
        {
            var request = await BuildAuthRequest(HttpMethod.Get, $"api/ref/visees-maitriser/{idVisee}");
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"api/ref/visees-maitriser/{idVisee} a retourné {(int)response.StatusCode} {response.StatusCode}.");
            return await response.Content.ReadFromJsonAsync<List<ViseesMaitriserRefDto>>() ?? new();
        }

        // Retourne les entrées appartenir_visee_aptitude d'une visée à maîtriser.
        // Endpoint : GET api/ref/appartenir/{idVm}
        public async Task<List<AppartenirRefDto>> GetAppartenirRefAsync(int idVm)
        {
            var request = await BuildAuthRequest(HttpMethod.Get, $"api/ref/appartenir/{idVm}");
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"api/ref/appartenir/{idVm} a retourné {(int)response.StatusCode} {response.StatusCode}.");
            return await response.Content.ReadFromJsonAsync<List<AppartenirRefDto>>() ?? new();
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // DTO DE SUPPORT ET TYPES DE RÉSULTAT
    // ──────────────────────────────────────────────────────────────────────

    // Représente le corps JSON retourné par l'endpoint GET api/access/check.
    // Contient une seule propriété booléenne indiquant si le code de licence est toujours actif.
    public class LicenseCheckResult
    {
        // True lorsque le code de licence est valide et n'a pas été révoqué ou expiré.
        [JsonPropertyName("valid")]
        public bool Valid { get; set; }
    }

    // Objet de transfert de données représentant un enregistrement de licence retourné par l'API admin.
    // Tous les noms de propriétés sont explicitement mappés à leurs équivalents JSON en camelCase
    // pour éviter les erreurs de désérialisation dues aux conventions PascalCase par défaut de .NET.
    public class LicenseDto
    {
        // Identifiant unique assigné par le serveur pour cette licence.
        [JsonPropertyName("id")]
        public int Id { get; set; }

        // Chaîne de code d'accès unique pour cette licence (ex. : "ABCDE-23456").
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        // Libellé lisible optionnel assigné par l'admin (ex. : "PROF-DUPONT").
        [JsonPropertyName("label")]
        public string? Label { get; set; }

        // True lorsque la licence est actuellement active et accorde l'accès à l'application.
        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }

        // Chaîne de statut conviviale (ex. : "Active", "Révoqué", "Expiré") dérivée côté serveur.
        // Utilisée pour afficher un badge coloré dans la liste des licences.
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        // L'adresse email de l'utilisateur auquel cette licence a été assignée, ou null si pas encore assignée.
        [JsonPropertyName("assignedEmail")]
        public string? AssignedEmail { get; set; }

        // L'horodatage UTC de création de cette licence.
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        // La date/heure UTC optionnelle après laquelle cette licence devient automatiquement inactive.
        // Null signifie que la licence n'expire pas.
        [JsonPropertyName("expiresAt")]
        public DateTime? ExpiresAt { get; set; }

        // L'horodatage UTC de la première utilisation/assignation de la licence à un compte utilisateur.
        // Null lorsque la licence n'a jamais été activée.
        [JsonPropertyName("assignedAt")]
        public DateTime? AssignedAt { get; set; }
    }

    // Objet de transfert de données représentant une catégorie de matière de la table categorie_cours.
    // Utilisé pour peupler la liste déroulante de catégorie de premier niveau sur la page de test.
    public class CategorieDto
    {
        // Identifiant unique assigné par le serveur pour cette catégorie.
        [JsonPropertyName("idCat")]
        public int IdCat { get; set; }

        // Nom d'affichage de la catégorie (ex. : "Sciences et techniques").
        [JsonPropertyName("nomCat")]
        public string NomCat { get; set; } = string.Empty;

        // Ordre de tri contrôlant la séquence dans la liste déroulante.
        [JsonPropertyName("ordre")]
        public int Ordre { get; set; }
    }

    // Objet de transfert de données représentant un cours de la base de données de référence.
    // Utilisé pour peupler la liste déroulante de cours en cascade sur la page de test.
    public class CoursDto
    {
        // Code court du cours utilisé comme clé unique (ex. : "LM", "SC", "MA").
        [JsonPropertyName("codeCours")]
        public string CodeCours { get; set; } = string.Empty;

        // Nom complet d'affichage du cours (ex. : "Langues Modernes").
        [JsonPropertyName("nomCours")]
        public string NomCours { get; set; } = string.Empty;

        // Chaîne de couleur CSS optionnelle utilisée pour afficher ce cours dans la vue agenda.
        // Peut être null si aucune couleur n'a été configurée.
        [JsonPropertyName("couleurAgenda")]
        public string? CouleurAgenda { get; set; }
    }

    // Objet de transfert de données représentant un niveau scolaire au sein d'un cours.
    // Utilisé pour peupler la deuxième liste déroulante après qu'un cours a été sélectionné.
    public class NiveauDto
    {
        // Code court identifiant ce niveau (ex. : "1A", "2B").
        [JsonPropertyName("codeNiveau")]
        public string CodeNiveau { get; set; } = string.Empty;

        // Nom lisible du niveau (ex. : "Première Année").
        [JsonPropertyName("nomNiveau")]
        public string NomNiveau { get; set; } = string.Empty;
    }

    // Objet de transfert de données représentant un domaine pédagogique au sein d'un cours et d'un niveau.
    // Utilisé pour peupler la troisième liste déroulante après qu'un cours et un niveau ont été sélectionnés.
    public class DomaineDto
    {
        // Identifiant unique assigné par le serveur pour ce domaine.
        [JsonPropertyName("idDom")]
        public int IdDom { get; set; }

        // Nom d'affichage du domaine (ex. : "Compréhension écrite").
        [JsonPropertyName("nom")]
        public string Nom { get; set; } = string.Empty;
    }

    // DTO sous-domaine pour la sélection en cascade (ref)
    public class SousDomaineRefDto
    {
        [JsonPropertyName("idSousDomaine")] public int    IdSousDomaine { get; set; }
        [JsonPropertyName("nomComp")]       public string NomComp       { get; set; } = string.Empty;
    }

    // DTO visée pour la sélection en cascade (ref)
    public class ViseeRefDto
    {
        [JsonPropertyName("idVisee")] public int    IdVisee { get; set; }
        [JsonPropertyName("label")]   public string Label   { get; set; } = string.Empty;
    }

    // DTO visée à maîtriser pour la sélection en cascade (ref)
    public class ViseesMaitriserRefDto
    {
        [JsonPropertyName("idViseesMaitriser")]  public int    IdViseesMaitriser  { get; set; }
        [JsonPropertyName("nomViseesMaitriser")] public string NomViseesMaitriser { get; set; } = string.Empty;
    }

    // DTO appartenir_visee_aptitude pour la sélection en cascade (ref)
    // Représente une aptitude + compétence liées à une visée à maîtriser
    public class AppartenirRefDto
    {
        [JsonPropertyName("idAppartenirViseeAptitude")] public int     IdAppartenirViseeAptitude { get; set; }
        [JsonPropertyName("idAptitude")]                public int?    IdAptitude                { get; set; }
        [JsonPropertyName("nomAptitude")]               public string? NomAptitude               { get; set; }
        [JsonPropertyName("idCompetenceFk")]            public int     IdCompetenceFk            { get; set; }
        [JsonPropertyName("nomCompetence")]             public string  NomCompetence             { get; set; } = string.Empty;
    }

    // ──────────────────────────────────────────────────────────────────────
    // DTOs ADMIN — PAGE DE GESTION DES DONNÉES PÉDAGOGIQUES
    // ──────────────────────────────────────────────────────────────────────

    // DTO représentant une catégorie de cours pour la page admin données
    public class CategorieAdminDto
    {
        [JsonPropertyName("idCat")]  public int    IdCat  { get; set; }
        [JsonPropertyName("nomCat")] public string NomCat { get; set; } = "";
        [JsonPropertyName("ordre")]  public int    Ordre  { get; set; }
    }

    // DTO représentant un cours pour la page admin données
    public class CoursAdminDto
    {
        [JsonPropertyName("idCours")]       public int     IdCours       { get; set; }
        [JsonPropertyName("nomCours")]      public string  NomCours      { get; set; } = "";
        [JsonPropertyName("codeCours")]     public string  CodeCours     { get; set; } = "";
        [JsonPropertyName("prefixCours")]   public string  PrefixCours   { get; set; } = "";
        [JsonPropertyName("couleurAgenda")] public string  CouleurAgenda { get; set; } = "";
        [JsonPropertyName("idCatFk")]       public int?    IdCatFk       { get; set; }
        [JsonPropertyName("nomCat")]        public string? NomCat        { get; set; }
    }

    // DTO représentant un niveau d'enseignement pour la page admin données
    public class NiveauAdminDto
    {
        [JsonPropertyName("idNiveau")]   public int    IdNiveau   { get; set; }
        [JsonPropertyName("codeNiveau")] public string CodeNiveau { get; set; } = "";
        [JsonPropertyName("nomNiveau")]  public string NomNiveau  { get; set; } = "";
        [JsonPropertyName("ordre")]      public int?   Ordre      { get; set; }
    }

    // DTO représentant un utilisateur (professeur) pour les sélecteurs admin
    public class ProfesseurAdminDto
    {
        [JsonPropertyName("idUser")] public int     IdUser { get; set; }
        [JsonPropertyName("email")]  public string  Email  { get; set; } = "";
        [JsonPropertyName("nom")]    public string? Nom    { get; set; }
        [JsonPropertyName("prenom")] public string? Prenom { get; set; }
    }

    // DTO représentant une liaison cours-niveau-professeur pour la page admin données
    public class CoursNiveauAdminDto
    {
        [JsonPropertyName("idCoursNiveau")] public int     IdCoursNiveau { get; set; }
        [JsonPropertyName("idCoursFk")]     public int     IdCoursFk     { get; set; }
        [JsonPropertyName("nomCours")]      public string  NomCours      { get; set; } = "";
        [JsonPropertyName("idNiveauFk")]    public int     IdNiveauFk    { get; set; }
        [JsonPropertyName("nomNiveau")]     public string  NomNiveau     { get; set; } = "";
        [JsonPropertyName("idProfFk")]      public int     IdProfFk      { get; set; }
        [JsonPropertyName("emailProf")]     public string  EmailProf     { get; set; } = "";
        [JsonPropertyName("nomProf")]       public string? NomProf       { get; set; }
    }

    // DTO représentant un domaine pédagogique pour la page admin données
    public class DomaineAdminDto
    {
        [JsonPropertyName("idDom")]          public int    IdDom          { get; set; }
        [JsonPropertyName("nom")]            public string Nom            { get; set; } = "";
        [JsonPropertyName("idCoursNiveauFk")] public int   IdCoursNiveauFk { get; set; }
        [JsonPropertyName("nomCours")]       public string NomCours       { get; set; } = "";
        [JsonPropertyName("nomNiveau")]      public string NomNiveau      { get; set; } = "";
    }

    // DTO représentant une compétence pour la page admin données
    public class CompetenceAdminDto
    {
        [JsonPropertyName("idCompetence")]   public int    IdCompetence   { get; set; }
        [JsonPropertyName("nomCompetence")]  public string NomCompetence  { get; set; } = "";
    }

    // DTO représentant une aptitude pour la page admin données
    public class AptitudeAdminDto
    {
        [JsonPropertyName("idAptitude")]  public int    IdAptitude  { get; set; }
        [JsonPropertyName("nomAptitude")] public string NomAptitude { get; set; } = "";
    }

    // DTO représentant un nom de visée pour la page admin données
    public class NomViseeAdminDto
    {
        [JsonPropertyName("idNomVisee")]  public int    IdNomVisee  { get; set; }
        [JsonPropertyName("nomVisee1")]   public string NomVisee1   { get; set; } = "";
    }

    // DTO représentant une visée à maîtriser pour la page admin données
    public class ViseesMaitriserAdminDto
    {
        [JsonPropertyName("idViseesMaitriser")]  public int    IdViseesMaitriser  { get; set; }
        [JsonPropertyName("nomViseesMaitriser")] public string NomViseesMaitriser { get; set; } = "";
    }

    // DTO représentant un sous-domaine pour la page admin données
    public class SousDomaineAdminDto
    {
        [JsonPropertyName("idSousDomaine")] public int    IdSousDomaine { get; set; }
        [JsonPropertyName("nomComp")]       public string NomComp       { get; set; } = "";
        [JsonPropertyName("idDomFk")]       public int    IdDomFk       { get; set; }
        [JsonPropertyName("nomDom")]        public string NomDom        { get; set; } = "";
    }

    // DTO représentant une visée (objectif d'apprentissage) avec son contexte complet
    public class ViseeAdminDto
    {
        [JsonPropertyName("idVisee")]        public int     IdVisee        { get; set; }
        [JsonPropertyName("idNomViseeFk")]   public int     IdNomViseeFk   { get; set; }
        [JsonPropertyName("nomViseeType")]   public string  NomViseeType   { get; set; } = "";
        [JsonPropertyName("idDomaineFk")]    public int     IdDomaineFk    { get; set; }
        [JsonPropertyName("nomDomaine")]     public string  NomDomaine     { get; set; } = "";
        [JsonPropertyName("idSousDomaineFk")] public int?  IdSousDomaineFk { get; set; }
        [JsonPropertyName("nomSousDomaine")] public string? NomSousDomaine { get; set; }
        [JsonPropertyName("idCompFk")]       public int     IdCompFk       { get; set; }
        [JsonPropertyName("nomCompetence")]  public string  NomCompetence  { get; set; } = "";
        [JsonPropertyName("nomCours")]       public string  NomCours       { get; set; } = "";
        [JsonPropertyName("nomNiveau")]      public string  NomNiveau      { get; set; } = "";
    }

    // DTO représentant un lien entre une visée et une visée à maîtriser
    public class LienViseeMaitriseAdminDto
    {
        [JsonPropertyName("idVisee")]            public int    IdVisee            { get; set; }
        [JsonPropertyName("contexteVisee")]      public string ContexteVisee      { get; set; } = "";
        [JsonPropertyName("idViseesMaitriser")]  public int    IdViseesMaitriser  { get; set; }
        [JsonPropertyName("nomViseesMaitriser")] public string NomViseesMaitriser { get; set; } = "";
    }

    // DTO représentant une liaison visée_maitriser ↔ aptitude ↔ compétence
    public class AppartenirAdminDto
    {
        [JsonPropertyName("idAppartenirViseeAptitude")] public int     IdAppartenirViseeAptitude { get; set; }
        [JsonPropertyName("idViseesMaitriserFk")]       public int     IdViseesMaitriserFk       { get; set; }
        [JsonPropertyName("nomVm")]                     public string  NomVm                     { get; set; } = "";
        [JsonPropertyName("idAptitudeFk")]              public int?    IdAptitudeFk              { get; set; }
        [JsonPropertyName("nomAptitude")]               public string? NomAptitude               { get; set; }
        [JsonPropertyName("idCompetenceFk")]            public int     IdCompetenceFk            { get; set; }
        [JsonPropertyName("nomComp")]                   public string  NomComp                   { get; set; } = "";
    }
}
