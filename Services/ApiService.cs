using Obrigenie.Models;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Obrigenie.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;

        public ApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // Auth
        // Echange le cookie temporaire auth_pending (défini après OAuth) contre les données d'auth
        public async Task<AuthResponse?> ExchangeOAuthTokenAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/auth/exchange");
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<AuthResponse>();
                return null;
            }
            catch { return null; }
        }

        public async Task<AuthResponse?> LoginAsync(LoginDto loginDto)
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginDto);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<AuthResponse>();
            return null;
        }

        public async Task<AuthResponse?> RegisterAsync(RegisterDto registerDto)
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/register", registerDto);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<AuthResponse>();
            return null;
        }

        // Courses
        public async Task<List<Course>> GetCoursesForDateAsync(DateTime date)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<Course>>($"api/courses/date/{date:yyyy-MM-dd}") ?? new();
            }
            catch { return new(); }
        }

        public async Task<List<Note>> GetNotesForRangeAsync(DateTime start, DateTime end)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<Note>>(
                    $"api/notes/range?start={start:yyyy-MM-dd}&end={end:yyyy-MM-dd}") ?? new();
            }
            catch { return new(); }
        }

        public async Task SaveCourseAsync(Course course)
        {
            await _httpClient.PostAsJsonAsync("api/courses", course);
        }

        public async Task DeleteCourseAsync(int id)
        {
            await _httpClient.DeleteAsync($"api/courses/{id}");
        }

        // Notes
        public async Task<List<Note>> GetNotesForDateAsync(DateTime date)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<Note>>($"api/notes/date/{date:yyyy-MM-dd}") ?? new();
            }
            catch { return new(); }
        }

        public async Task<(bool Success, string? Error)> SaveNoteAsync(Note note)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/notes", note);
                if (response.IsSuccessStatusCode) return (true, null);
                var body = await response.Content.ReadAsStringAsync();
                return (false, $"Erreur {(int)response.StatusCode} : {body}");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task DeleteNoteAsync(int id)
        {
            await _httpClient.DeleteAsync($"api/notes/{id}");
        }

        // Licence – validation initiale (utilisateur connecté entre son code)
        public async Task<bool> ValidateAccessCodeAsync(string code)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/access/validate", new { code });
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        // Licence – vérification à chaque chargement (révocation en temps réel)
        public async Task<bool> CheckLicenseAsync(string code)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/access/check?code={Uri.EscapeDataString(code)}");
                if (!response.IsSuccessStatusCode) return false;
                var result = await response.Content.ReadFromJsonAsync<LicenseCheckResult>();
                return result?.Valid == true;
            }
            catch { return false; }
        }

        // Admin – liste des licences
        public async Task<List<LicenseDto>> GetLicensesAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<List<LicenseDto>>("api/admin/licenses") ?? new();
            }
            catch { return new(); }
        }

        // Admin – créer une licence
        public async Task<(LicenseDto? License, string? Error)> CreateLicenseAsync(string? label, DateTime? expiresAt, string? code = null)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/admin/licenses", new { code, label, expiresAt });
                if (response.IsSuccessStatusCode)
                    return (await response.Content.ReadFromJsonAsync<LicenseDto>(), null);

                // Lire le message d'erreur réel du serveur
                try
                {
                    var err = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                    var msg = err.GetProperty("message").GetString();
                    return (null, msg ?? $"Erreur {(int)response.StatusCode}");
                }
                catch
                {
                    return (null, $"Erreur {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        // Admin – révoquer
        public async Task<bool> RevokeLicenseAsync(int id)
        {
            try
            {
                var response = await _httpClient.PutAsync($"api/admin/licenses/{id}/revoke", null);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        // Admin – réactiver
        public async Task<bool> ReactivateLicenseAsync(int id)
        {
            try
            {
                var response = await _httpClient.PutAsync($"api/admin/licenses/{id}/reactivate", null);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        // Admin – supprimer
        public async Task<bool> DeleteLicenseAsync(int id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/admin/licenses/{id}");
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        // Admin – déclencher le scraper calendrier scolaire
        public async Task<(bool Success, string Message)> TriggerScraperAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/update-scolaire");
                if (response.IsSuccessStatusCode)
                    return (true, await response.Content.ReadAsStringAsync());
                return (false, $"Erreur {(int)response.StatusCode}");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // Health
        public async Task<bool> CheckHealthAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/health");
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }

    public class LicenseCheckResult
    {
        [JsonPropertyName("valid")]
        public bool Valid { get; set; }
    }

    public class LicenseDto
    {
        [JsonPropertyName("id")]       public int Id { get; set; }
        [JsonPropertyName("code")]     public string Code { get; set; } = string.Empty;
        [JsonPropertyName("label")]    public string? Label { get; set; }
        [JsonPropertyName("isActive")] public bool IsActive { get; set; }
        [JsonPropertyName("status")]   public string Status { get; set; } = string.Empty;
        [JsonPropertyName("assignedEmail")] public string? AssignedEmail { get; set; }
        [JsonPropertyName("createdAt")]     public DateTime CreatedAt { get; set; }
        [JsonPropertyName("expiresAt")]     public DateTime? ExpiresAt { get; set; }
        [JsonPropertyName("assignedAt")]    public DateTime? AssignedAt { get; set; }
    }
}
