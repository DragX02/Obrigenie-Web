using Obrigenie.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Obrigenie.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;

       
        private readonly AuthService _auth;

        public ApiService(HttpClient httpClient, AuthService auth)
        {
            _httpClient = httpClient;
            _auth = auth;
        }

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
        public async Task<(AuthResponse? Auth, string? Error)> LoginAsync(LoginDto loginDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginDto);

                // Désérialise et retourne le payload du jeton sur HTTP 200
                if (response.IsSuccessStatusCode)
                    return (await response.Content.ReadFromJsonAsync<AuthResponse>(), null);

                // 429 : la politique de limitation de débit du serveur a rejeté la requête
                // (5 requêtes / 15 min par IP, partagées entre connexion et inscription)
                if ((int)response.StatusCode == 429)
                    return (null, "Trop de tentatives. Veuillez réessayer dans quelques minutes.");

                var message = await ReadMessageAsync(response);
                return (null, message ?? "Email ou mot de passe incorrect.");
            }
            catch
            {
                return (null, "Impossible de contacter le serveur. Veuillez réessayer plus tard.");
            }
        }

        public async Task<(bool Success, string Message)> RegisterAsync(RegisterDto registerDto)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/auth/register", registerDto);

                var message = await ReadMessageAsync(response);

                if (response.IsSuccessStatusCode)
                    return (true, message ?? "Compte créé ! Vérifiez votre e-mail pour confirmer votre inscription.");

                if ((int)response.StatusCode == 429)
                    return (false, "Trop de tentatives. Veuillez réessayer dans quelques minutes.");

                return (false, message ?? "Impossible de créer le compte. Veuillez réessayer.");
            }
            catch
            {
                return (false, "Impossible de contacter le serveur. Veuillez réessayer plus tard.");
            }
        }

        public async Task<(bool Success, string Message)> ForgotPasswordAsync(string email)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    "api/auth/forgot-password", new ForgotPasswordDto { Email = email });

                var message = await ReadMessageAsync(response);

                if (response.IsSuccessStatusCode)
                    return (true, message ?? "Si un compte existe avec cette adresse, un e-mail vient d'être envoyé.");

                if ((int)response.StatusCode == 429)
                    return (false, "Trop de tentatives. Veuillez réessayer dans quelques minutes.");

                return (false, message ?? "Impossible d'envoyer l'e-mail de réinitialisation.");
            }
            catch
            {
                return (false, "Impossible de contacter le serveur. Veuillez réessayer plus tard.");
            }
        }

        public async Task<(bool Valid, string? Error)> ValidateResetTokenAsync(string token)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"api/auth/validate-reset-token?token={Uri.EscapeDataString(token)}");

                if (response.IsSuccessStatusCode) return (true, null);

                var message = await ReadMessageAsync(response);
                return (false, message ?? "Ce lien de réinitialisation est invalide ou a expiré.");
            }
            catch
            {
                return (false, "Impossible de contacter le serveur. Veuillez réessayer plus tard.");
            }
        }

        public async Task<(bool Success, string Message)> ResetPasswordAsync(
            string token, string password, string confirmPassword)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/auth/reset-password", new ResetPasswordDto
                {
                    Token           = token,
                    Password        = password,
                    ConfirmPassword = confirmPassword
                });

                var message = await ReadMessageAsync(response);

                if (response.IsSuccessStatusCode)
                    return (true, message ?? "Mot de passe modifié ! Vous pouvez maintenant vous connecter.");

                if ((int)response.StatusCode == 429)
                    return (false, "Trop de tentatives. Veuillez réessayer dans quelques minutes.");

                return (false, message ?? "Impossible de modifier le mot de passe.");
            }
            catch
            {
                return (false, "Impossible de contacter le serveur. Veuillez réessayer plus tard.");
            }
        }

        private static async Task<string?> ReadMessageAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(body)) return null;

            try
            {
                using var json = System.Text.Json.JsonDocument.Parse(body);
                if (json.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    json.RootElement.TryGetProperty("message", out var msg))
                {
                    return msg.GetString();
                }

                return body.Trim('"');
            }
            catch
            {
                return body;
            }
        }

        public async Task<List<Course>> GetCoursesForDateAsync(DateTime date)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<Course>>(
                    $"api/courses/date/{date:yyyy-MM-dd}") ?? new();
            }
            catch
            {
                return new();
            }
        }

        public async Task<Dictionary<DateTime, List<Course>>> GetCoursesForRangeAsync(DateTime start, DateTime end)
        {
            try
            {
                var jours = await _httpClient.GetFromJsonAsync<List<CoursesJourDto>>(
                    $"api/courses/range?start={start:yyyy-MM-dd}&end={end:yyyy-MM-dd}");

                if (jours == null) return new();

                return jours.ToDictionary(j => j.Date.Date, j => j.Courses);
            }
            catch
            {
                return new();
            }
        }

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
        public async Task SaveCourseAsync(Course course)
        {
            await _httpClient.PostAsJsonAsync("api/courses", course);
        }

        public async Task DeleteCourseAsync(int id)
        {
            await _httpClient.DeleteAsync($"api/courses/{id}");
        }

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

        public async Task<(bool Success, string? Error)> SaveNoteAsync(Note note)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/notes", note);

                if (response.IsSuccessStatusCode) return (true, null);

                var body = await response.Content.ReadAsStringAsync();
                return (false, $"Error {(int)response.StatusCode}: {body}");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool Ok, int Copiees, string? Err)> CopierNotesAsync(
            IEnumerable<int> idsNotes, IEnumerable<int> decalages, bool marquer,
            IEnumerable<Note>? modeles = null)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/notes/copier", new
                {
                    idsNotes  = idsNotes.ToList(),
                    modeles   = modeles?.ToList() ?? new List<Note>(),
                    decalages = decalages.ToList(),
                    marquer
                });

                if (!response.IsSuccessStatusCode)
                    return (false, 0, await LireMessageErreur(response));

                var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                return (true, json.TryGetProperty("copiees", out var n) ? n.GetInt32() : 0, null);
            }
            catch (Exception ex)
            {
                return (false, 0, ex.Message);
            }
        }

        public async Task DeleteNoteAsync(int id)
        {
            await _httpClient.DeleteAsync($"api/notes/{id}");
        }

        public async Task<(List<UserConge> Conges, string? Error)> GetCongesAsync()
        {
            try
            {
                var reponse = await _httpClient.GetAsync("api/conges");

                if (!reponse.IsSuccessStatusCode)
                {
                    var body = await reponse.Content.ReadAsStringAsync();
                    return (new(), $"Error {(int)reponse.StatusCode}: {body}");
                }

                var conges = await reponse.Content.ReadFromJsonAsync<List<UserConge>>();
                return (conges ?? new(), null);
            }
            catch (Exception ex)
            {
                return (new(), ex.Message);
            }
        }
        public async Task<(bool Success, string? Error)> SaveCongeAsync(UserConge conge)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/conges", conge);

                if (response.IsSuccessStatusCode) return (true, null);

                var body = await response.Content.ReadAsStringAsync();
                return (false, $"Error {(int)response.StatusCode}: {body}");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
        public async Task DeleteCongeAsync(int id)
        {
            await _httpClient.DeleteAsync($"api/conges/{id}");
        }

        public async Task<(List<Lecon> Lecons, string? Error)> GetLeconsAsync()
        {
            try
            {
                var reponse = await _httpClient.GetAsync("api/lecons");

                if (!reponse.IsSuccessStatusCode)
                {
                    var body = await reponse.Content.ReadAsStringAsync();
                    return (new(), $"Error {(int)reponse.StatusCode}: {body}");
                }

                var lecons = await reponse.Content.ReadFromJsonAsync<List<Lecon>>();
                return (lecons ?? new(), null);
            }
            catch (Exception ex)
            {
                return (new(), ex.Message);
            }
        }

        // Crée une préparation (Id == 0) ou met à jour une existante.
        // Le serveur réécrit le déroulement en bloc et renumérote les phases.
        // Endpoint : POST api/lecons
        // Retourne (true, la préparation enregistrée, null) en cas de succès ;
        // (false, null, message) sinon, pour que l'interface affiche le refus inline.
        public async Task<(bool Ok, Lecon? Lecon, string? Err)> SaveLeconAsync(Lecon lecon)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/lecons", lecon);

                if (!response.IsSuccessStatusCode)
                    return (false, null, await LireMessageErreur(response));

                var enregistree = await response.Content.ReadFromJsonAsync<Lecon>();
                return (true, enregistree, null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        public async Task<(bool Ok, string? Err)> DeleteLeconAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/lecons/{id}");
                if (response.IsSuccessStatusCode) return (true, null);
                return (false, await LireMessageErreur(response));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

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

        public async Task<bool> CheckLicenseAsync(string code)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"api/access/check?code={Uri.EscapeDataString(code)}");

                if (!response.IsSuccessStatusCode) return false;

                var result = await response.Content.ReadFromJsonAsync<LicenseCheckResult>();
                return result?.Valid == true;
            }
            catch
            {
                return false;
            }
        }

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

        public async Task<(LicenseDto? License, string? Error)> CreateLicenseAsync(
            string? label, DateTime? expiresAt, string? code = null)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    "api/admin/licenses", new { code, label, expiresAt });

                if (response.IsSuccessStatusCode)
                    return (await response.Content.ReadFromJsonAsync<LicenseDto>(), null);

                try
                {
                    var err = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                    var msg = err.GetProperty("message").GetString();
                    return (null, msg ?? $"Error {(int)response.StatusCode}");
                }
                catch
                {
                    return (null, $"Error {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }
        public async Task<bool> RevokeLicenseAsync(int id)
        {
            try
            {
                var response = await _httpClient.PutAsync($"api/admin/licenses/{id}/revoke", null);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
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

        public string BaseUrl => _httpClient.BaseAddress?.ToString() ?? "";
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

   
        private async Task<HttpRequestMessage> BuildAuthRequest(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);
            // Lit le jeton JWT stocké et l'attache comme en-tête Authorization Bearer
            var token = await _auth.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return request;
        }

        public async Task<(bool Ok, string? Err)> SupprimerCompteAsync(string confirmation)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/compte/supprimer", new { Confirmation = confirmation });
                if (response.IsSuccessStatusCode) return (true, null);
                return (false, await LireMessageErreur(response));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(List<PageGarde> Pages, string? Err)> GetPagesGardeAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/pagesgarde");
                if (!response.IsSuccessStatusCode)
                    return (new(), await LireMessageErreur(response));

                var pages = await response.Content.ReadFromJsonAsync<List<PageGarde>>();
                return (pages ?? new(), null);
            }
            catch (Exception ex)
            {
                return (new(), ex.Message);
            }
        }

        public async Task<(PageGarde? Page, string? Err)> GetPageGardeAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/pagesgarde/{id}");
                if (!response.IsSuccessStatusCode)
                    return (null, await LireMessageErreur(response));

                var page = await response.Content.ReadFromJsonAsync<PageGarde>();
                return (page, null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        public async Task<(bool Ok, PageGarde? Page, string? Err)> SavePageGardeAsync(PageGarde page)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/pagesgarde", page);
                if (!response.IsSuccessStatusCode)
                    return (false, null, await LireMessageErreur(response));

                var enregistree = await response.Content.ReadFromJsonAsync<PageGarde>();
                return (true, enregistree, null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        public async Task<(bool Ok, string? Err)> DeletePageGardeAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/pagesgarde/{id}");
                if (response.IsSuccessStatusCode) return (true, null);
                return (false, await LireMessageErreur(response));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private Dictionary<string, string> imagesChargees = new Dictionary<string, string>();

        public async Task<(List<ImageGarde> Images, string? Err)> GetImagesGardeAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/imagesgarde");
                if (!response.IsSuccessStatusCode)
                    return (new(), await LireMessageErreur(response));

                var images = await response.Content.ReadFromJsonAsync<List<ImageGarde>>();
                return (images ?? new(), null);
            }
            catch (Exception ex)
            {
                return (new(), ex.Message);
            }
        }

        public async Task<string?> GetImageGardeDataUrlAsync(string source)
        {
            if (imagesChargees.ContainsKey(source))
                return imagesChargees[source];

            try
            {
                var response = await _httpClient.GetAsync("api/imagesgarde/fichier?source=" + Uri.EscapeDataString(source));
                if (!response.IsSuccessStatusCode)
                    return null;

                var octets = await response.Content.ReadAsByteArrayAsync();
                string type = "image/png";
                if (source.EndsWith(".jpg"))
                    type = "image/jpeg";

                string dataUrl = "data:" + type + ";base64," + Convert.ToBase64String(octets);
                imagesChargees[source] = dataUrl;
                return dataUrl;
            }
            catch
            {
                return null;
            }
        }

        public async Task<(ImageGarde? Image, string? Err)> UploadImagePersoAsync(string nom, string typeMime, string donneesBase64)
        {
            try
            {
                var envoi = new { Nom = nom, TypeMime = typeMime, DonneesBase64 = donneesBase64 };
                var response = await _httpClient.PostAsJsonAsync("api/imagesgarde/perso", envoi);
                if (!response.IsSuccessStatusCode)
                    return (null, await LireMessageErreur(response));

                var image = await response.Content.ReadFromJsonAsync<ImageGarde>();
                return (image, null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        public async Task<(bool Ok, string? Err)> DeleteImagePersoAsync(string source)
        {
            try
            {
                var response = await _httpClient.DeleteAsync("api/imagesgarde/perso?source=" + Uri.EscapeDataString(source));
                if (response.IsSuccessStatusCode)
                {
                    imagesChargees.Remove(source);
                    return (true, null);
                }
                return (false, await LireMessageErreur(response));
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<List<EmojiItem>> GetEmojisAsync()
        {
            try
            {
                var request = await BuildAuthRequest(HttpMethod.Get, "api/emojis");
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return new();
                return await response.Content.ReadFromJsonAsync<List<EmojiItem>>() ?? new();
            }
            catch
            {
                return new();
            }
        }

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

        // Récupère TOUS les niveaux disponibles (ayant des visées), indépendamment du cours.
        // Alimente la première liste déroulante de la cascade réordonnée (Année en premier).
        // Endpoint : GET api/ref/niveaux
        // Retourne une liste de NiveauDto triée par code de niveau.
        public async Task<List<NiveauDto>> GetNiveauxTousAsync()
        {
            var request = await BuildAuthRequest(HttpMethod.Get, "api/ref/niveaux");
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"api/ref/niveaux a retourné {(int)response.StatusCode} {response.StatusCode}.");
            return await response.Content.ReadFromJsonAsync<List<NiveauDto>>() ?? new();
        }

        public async Task<List<CategorieDto>> GetCategoriesByNiveauAsync(string codeNiveau)
        {
            var request = await BuildAuthRequest(HttpMethod.Get, $"api/ref/categories/by-niveau/{Uri.EscapeDataString(codeNiveau)}");
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"api/ref/categories/by-niveau/{codeNiveau} a retourné {(int)response.StatusCode} {response.StatusCode}.");
            return await response.Content.ReadFromJsonAsync<List<CategorieDto>>() ?? new();
        }

        public async Task<List<CoursDto>> GetCoursByCatNiveauAsync(int idCat, string codeNiveau)
        {
            var request = await BuildAuthRequest(HttpMethod.Get, $"api/ref/cours/by-cat-niveau/{idCat}/{Uri.EscapeDataString(codeNiveau)}");
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"api/ref/cours/by-cat-niveau/{idCat}/{codeNiveau} a retourné {(int)response.StatusCode} {response.StatusCode}.");
            return await response.Content.ReadFromJsonAsync<List<CoursDto>>() ?? new();
        }

        public async Task<List<CoursDto>> GetCoursByNiveauAsync(string codeNiveau)
        {
            var request = await BuildAuthRequest(HttpMethod.Get, $"api/ref/cours/by-niveau/{Uri.EscapeDataString(codeNiveau)}");
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"api/ref/cours/by-niveau/{codeNiveau} a retourné {(int)response.StatusCode} {response.StatusCode}.");
            return await response.Content.ReadFromJsonAsync<List<CoursDto>>() ?? new();
        }

        public async Task<List<NiveauDto>> GetNiveauxAsync(string codeCours)
        {
            var request = await BuildAuthRequest(HttpMethod.Get, $"api/ref/niveaux/{codeCours}");
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"api/ref/niveaux/{codeCours} a retourné {(int)response.StatusCode} {response.StatusCode}.");
            return await response.Content.ReadFromJsonAsync<List<NiveauDto>>() ?? new();
        }

        public async Task<List<DomaineDto>> GetDomainesAsync(string codeCours, string codeNiveau)
        {
            var request = await BuildAuthRequest(HttpMethod.Get, $"api/ref/domaines/{codeCours}/{codeNiveau}");
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"api/ref/domaines/{codeCours}/{codeNiveau} a retourné {(int)response.StatusCode} {response.StatusCode}.");
            return await response.Content.ReadFromJsonAsync<List<DomaineDto>>() ?? new();
        }
        public async Task<List<SousDomaineRefDto>> GetSousDomainesRefAsync(int idDomaine)
        {
            var request = await BuildAuthRequest(HttpMethod.Get, $"api/ref/sous-domaines/{idDomaine}");
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"api/ref/sous-domaines/{idDomaine} a retourné {(int)response.StatusCode} {response.StatusCode}.");
            return await response.Content.ReadFromJsonAsync<List<SousDomaineRefDto>>() ?? new();
        }

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

        public async Task<List<ViseesMaitriserRefDto>> GetViseesMaitriserRefAsync(int idVisee)
        {
            var request = await BuildAuthRequest(HttpMethod.Get, $"api/ref/visees-maitriser/{idVisee}");
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"api/ref/visees-maitriser/{idVisee} a retourné {(int)response.StatusCode} {response.StatusCode}.");
            return await response.Content.ReadFromJsonAsync<List<ViseesMaitriserRefDto>>() ?? new();
        }

        public async Task<List<ViseesMaitriserRefDto>> GetViseesMaitriserParViseesAsync(IEnumerable<int> idVisees)
        {
            var ids = idVisees.Distinct().ToList();

            if (ids.Count == 0) return new();

            var url = "api/ref/visees-maitriser/par-visees?" + string.Join("&", ids.Select(id => $"ids={id}"));
            var request = await BuildAuthRequest(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"api/ref/visees-maitriser/par-visees a retourné {(int)response.StatusCode} {response.StatusCode}.");
            return await response.Content.ReadFromJsonAsync<List<ViseesMaitriserRefDto>>() ?? new();
        }

        public async Task<List<AppartenirRefDto>> GetAppartenirRefAsync(int idVm, int? idVisee = null)
        {
            var url = idVisee.HasValue
                ? $"api/ref/appartenir/{idVm}?idVisee={idVisee.Value}"
                : $"api/ref/appartenir/{idVm}";
            var request = await BuildAuthRequest(HttpMethod.Get, url);
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"api/ref/appartenir/{idVm} a retourné {(int)response.StatusCode} {response.StatusCode}.");
            return await response.Content.ReadFromJsonAsync<List<AppartenirRefDto>>() ?? new();
        }

        public async Task<List<CompetenceRefDto>> GetCompetencesRefAsync()
        {
            var request = await BuildAuthRequest(HttpMethod.Get, "api/ref/competences");
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"api/ref/competences a retourné {(int)response.StatusCode} {response.StatusCode}.");
            return await response.Content.ReadFromJsonAsync<List<CompetenceRefDto>>() ?? new();
        }

        public async Task<List<NomViseeRefDto>> GetNomViseesRefAsync()
        {
            var request = await BuildAuthRequest(HttpMethod.Get, "api/ref/nom-visees");
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"api/ref/nom-visees a retourné {(int)response.StatusCode} {response.StatusCode}.");
            return await response.Content.ReadFromJsonAsync<List<NomViseeRefDto>>() ?? new();
        }

        public async Task<List<ViseesMaitriserRefDto>> GetToutesViseesMaitriserRefAsync()
        {
            var request = await BuildAuthRequest(HttpMethod.Get, "api/ref/visees-maitriser");
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"api/ref/visees-maitriser a retourné {(int)response.StatusCode} {response.StatusCode}.");
            return await response.Content.ReadFromJsonAsync<List<ViseesMaitriserRefDto>>() ?? new();
        }

        public async Task<(bool Ok, int Id, string? Err)> CreateRefDomaineAsync(
            string nom, string codeCours, string codeNiveau)
        {
            try
            {
                var request = await BuildAuthRequest(HttpMethod.Post, "api/ref/domaines");
                request.Content = JsonContent.Create(new { nom, codeCours, codeNiveau });
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                    return (true, json.TryGetProperty("idDom", out var id) ? id.GetInt32() : 0, null);
                }
                return (false, 0, await LireMessageErreur(response));
            }
            catch (Exception ex) { return (false, 0, ex.Message); }
        }

        public async Task<(bool Ok, int Id, string? Err)> CreateRefSousDomaineAsync(string nom, int idDomaine)
        {
            try
            {
                var request = await BuildAuthRequest(HttpMethod.Post, "api/ref/sous-domaines");
                request.Content = JsonContent.Create(new { nom, idDomaine });
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                    return (true, json.TryGetProperty("idSousDomaine", out var id) ? id.GetInt32() : 0, null);
                }
                return (false, 0, await LireMessageErreur(response));
            }
            catch (Exception ex) { return (false, 0, ex.Message); }
        }

        public Task<(bool Ok, int Id, string? Err)> CreateRefCompetenceAsync(string nom) =>
            CreerEntreeNommeeAsync("api/ref/competences", nom, "idCompetence");

        public Task<(bool Ok, int Id, string? Err)> CreateRefNomViseeAsync(string nom) =>
            CreerEntreeNommeeAsync("api/ref/nom-visees", nom, "idNomVisee");

        public Task<(bool Ok, int Id, string? Err)> CreateRefViseeMaitriserAsync(string nom) =>
            CreerEntreeNommeeAsync("api/ref/visees-maitriser", nom, "idViseesMaitriser");

        private async Task<(bool Ok, int Id, string? Err)> CreerEntreeNommeeAsync(
            string url, string nom, string proprieteId)
        {
            try
            {
                var request = await BuildAuthRequest(HttpMethod.Post, url);
                request.Content = JsonContent.Create(new { nom });
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                    return (true, json.TryGetProperty(proprieteId, out var id) ? id.GetInt32() : 0, null);
                }
                return (false, 0, await LireMessageErreur(response));
            }
            catch (Exception ex) { return (false, 0, ex.Message); }
        }

        public async Task<(bool Ok, int IdVisee, string? Err)> CreateRefViseeAsync(
            int idDomaine, int idSousDomaine, int idNomVisee, int idCompetence)
        {
            try
            {
                var request = await BuildAuthRequest(HttpMethod.Post, "api/ref/visees");
                request.Content = JsonContent.Create(new { idDomaine, idSousDomaine, idNomVisee, idCompetence });
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                    return (true, json.TryGetProperty("idVisee", out var id) ? id.GetInt32() : 0, null);
                }
                return (false, 0, await LireMessageErreur(response));
            }
            catch (Exception ex) { return (false, 0, ex.Message); }
        }

        public async Task<(bool Ok, string? Err)> CreateRefLienViseeMaitriseAsync(int idVisee, int idViseesMaitriser)
        {
            try
            {
                var request = await BuildAuthRequest(HttpMethod.Post, "api/ref/lien-visee-maitrise");
                request.Content = JsonContent.Create(new { idVisee, idViseesMaitriser });
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode) return (true, null);
                return (false, await LireMessageErreur(response));
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public async Task<(bool Ok, List<int> IdVisees, string? Err)> EnregistrerSelectionRefAsync(
            int idDomaine, int idSousDomaine, int idCompetence,
            IEnumerable<int> idNomVisees, IEnumerable<int> idsViseesMaitriser)
        {
            try
            {
                var request = await BuildAuthRequest(HttpMethod.Post, "api/ref/selection");
                request.Content = JsonContent.Create(new
                {
                    idDomaine,
                    idSousDomaine,
                    idCompetence,
                    idNomVisees        = idNomVisees.ToList(),
                    idsViseesMaitriser = idsViseesMaitriser.ToList()
                });

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                    return (false, new(), await LireMessageErreur(response));

                var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

                var idVisees = new List<int>();
                if (json.TryGetProperty("idVisees", out var tableau) &&
                    tableau.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var element in tableau.EnumerateArray()) idVisees.Add(element.GetInt32());
                }

                return (true, idVisees, null);
            }
            catch (Exception ex) { return (false, new(), ex.Message); }
        }

        private static async Task<string> LireMessageErreur(HttpResponseMessage response)
        {
            try
            {
                var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                return json.TryGetProperty("message", out var m)
                    ? m.GetString() ?? $"Erreur {(int)response.StatusCode}"
                    : $"Erreur {(int)response.StatusCode}";
            }
            catch { return $"Erreur {(int)response.StatusCode}"; }
        }
    }

    public class LicenseCheckResult
    {
        [JsonPropertyName("valid")]
        public bool Valid { get; set; }
    }
    public class LicenseDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("assignedEmail")]
        public string? AssignedEmail { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("expiresAt")]
        public DateTime? ExpiresAt { get; set; }

        [JsonPropertyName("assignedAt")]
        public DateTime? AssignedAt { get; set; }
    }

    public class CoursesJourDto
    {
        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

        [JsonPropertyName("courses")]
        public List<Course> Courses { get; set; } = new();
    }
    public class CategorieDto
    {
        [JsonPropertyName("idCat")]
        public int IdCat { get; set; }

        [JsonPropertyName("nomCat")]
        public string NomCat { get; set; } = string.Empty;

        [JsonPropertyName("ordre")]
        public int Ordre { get; set; }
    }

    public class CoursDto
    {
        [JsonPropertyName("codeCours")]
        public string CodeCours { get; set; } = string.Empty;

        [JsonPropertyName("nomCours")]
        public string NomCours { get; set; } = string.Empty;

        [JsonPropertyName("couleurAgenda")]
        public string? CouleurAgenda { get; set; }
    }

    public class NiveauDto
    {
        [JsonPropertyName("codeNiveau")]
        public string CodeNiveau { get; set; } = string.Empty;

        [JsonPropertyName("nomNiveau")]
        public string NomNiveau { get; set; } = string.Empty;
    }

    public class DomaineDto
    {
        [JsonPropertyName("idDom")]
        public int IdDom { get; set; }

        [JsonPropertyName("nom")]
        public string Nom { get; set; } = string.Empty;
    }

    public class SousDomaineRefDto
    {
        [JsonPropertyName("idSousDomaine")] public int    IdSousDomaine { get; set; }
        [JsonPropertyName("nomComp")]       public string NomComp       { get; set; } = string.Empty;
    }

    public class ViseeRefDto
    {
        [JsonPropertyName("idVisee")]       public int    IdVisee       { get; set; }
        [JsonPropertyName("idNomVisee")]    public int    IdNomVisee    { get; set; }
        [JsonPropertyName("nomVisee")]      public string NomVisee      { get; set; } = string.Empty;
        [JsonPropertyName("idCompetence")]  public int    IdCompetence  { get; set; }
        [JsonPropertyName("nomCompetence")] public string NomCompetence { get; set; } = string.Empty;
        [JsonPropertyName("label")]         public string Label         { get; set; } = string.Empty;
    }

    public class CompetenceRefDto
    {
        [JsonPropertyName("idCompetence")]  public int    IdCompetence  { get; set; }
        [JsonPropertyName("nomCompetence")] public string NomCompetence { get; set; } = string.Empty;
    }

    public class NomViseeRefDto
    {
        [JsonPropertyName("idNomVisee")] public int    IdNomVisee { get; set; }
        [JsonPropertyName("nomVisee")]   public string NomVisee   { get; set; } = string.Empty;
    }

    public class ViseesMaitriserRefDto
    {
        [JsonPropertyName("idViseesMaitriser")]  public int    IdViseesMaitriser  { get; set; }
        [JsonPropertyName("nomViseesMaitriser")] public string NomViseesMaitriser { get; set; } = string.Empty;
    }

    public class AppartenirRefDto
    {
        [JsonPropertyName("idAppartenirViseeAptitude")] public int     IdAppartenirViseeAptitude { get; set; }
        [JsonPropertyName("idAptitude")]                public int?    IdAptitude                { get; set; }
        [JsonPropertyName("nomAptitude")]               public string? NomAptitude               { get; set; }
        [JsonPropertyName("idCompetenceFk")]            public int     IdCompetenceFk            { get; set; }
        [JsonPropertyName("nomCompetence")]             public string  NomCompetence             { get; set; } = string.Empty;
    }

    public class CategorieAdminDto
    {
        [JsonPropertyName("idCat")]  public int    IdCat  { get; set; }
        [JsonPropertyName("nomCat")] public string NomCat { get; set; } = "";
        [JsonPropertyName("ordre")]  public int    Ordre  { get; set; }
    }

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

    public class NiveauAdminDto
    {
        [JsonPropertyName("idNiveau")]   public int    IdNiveau   { get; set; }
        [JsonPropertyName("codeNiveau")] public string CodeNiveau { get; set; } = "";
        [JsonPropertyName("nomNiveau")]  public string NomNiveau  { get; set; } = "";
        [JsonPropertyName("ordre")]      public int?   Ordre      { get; set; }
    }

    public class ProfesseurAdminDto
    {
        [JsonPropertyName("idUser")] public int     IdUser { get; set; }
        [JsonPropertyName("email")]  public string  Email  { get; set; } = "";
        [JsonPropertyName("nom")]    public string? Nom    { get; set; }
        [JsonPropertyName("prenom")] public string? Prenom { get; set; }
    }

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

    public class DomaineAdminDto
    {
        [JsonPropertyName("idDom")]          public int    IdDom          { get; set; }
        [JsonPropertyName("nom")]            public string Nom            { get; set; } = "";
        [JsonPropertyName("idCoursNiveauFk")] public int   IdCoursNiveauFk { get; set; }
        [JsonPropertyName("nomCours")]       public string NomCours       { get; set; } = "";
        [JsonPropertyName("nomNiveau")]      public string NomNiveau      { get; set; } = "";
    }

    public class CompetenceAdminDto
    {
        [JsonPropertyName("idCompetence")]   public int    IdCompetence   { get; set; }
        [JsonPropertyName("nomCompetence")]  public string NomCompetence  { get; set; } = "";
    }

    public class AptitudeAdminDto
    {
        [JsonPropertyName("idAptitude")]  public int    IdAptitude  { get; set; }
        [JsonPropertyName("nomAptitude")] public string NomAptitude { get; set; } = "";
    }

    public class NomViseeAdminDto
    {
        [JsonPropertyName("idNomVisee")]  public int    IdNomVisee  { get; set; }
        [JsonPropertyName("nomVisee1")]   public string NomVisee1   { get; set; } = "";
    }

    public class ViseesMaitriserAdminDto
    {
        [JsonPropertyName("idViseesMaitriser")]  public int    IdViseesMaitriser  { get; set; }
        [JsonPropertyName("nomViseesMaitriser")] public string NomViseesMaitriser { get; set; } = "";
    }

    public class SousDomaineAdminDto
    {
        [JsonPropertyName("idSousDomaine")] public int    IdSousDomaine { get; set; }
        [JsonPropertyName("nomComp")]       public string NomComp       { get; set; } = "";
        [JsonPropertyName("idDomFk")]       public int    IdDomFk       { get; set; }
        [JsonPropertyName("nomDom")]        public string NomDom        { get; set; } = "";
    }

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

    public class LienViseeMaitriseAdminDto
    {
        [JsonPropertyName("idVisee")]            public int    IdVisee            { get; set; }
        [JsonPropertyName("contexteVisee")]      public string ContexteVisee      { get; set; } = "";
        [JsonPropertyName("idViseesMaitriser")]  public int    IdViseesMaitriser  { get; set; }
        [JsonPropertyName("nomViseesMaitriser")] public string NomViseesMaitriser { get; set; } = "";
    }

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
