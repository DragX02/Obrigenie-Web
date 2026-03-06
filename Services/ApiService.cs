using Obrigenie.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Obrigenie.Services
{
    /// <summary>
    /// The central HTTP service for the Obrigenie Blazor WebAssembly client.
    /// Wraps all communication with the backend REST API and exposes strongly typed
    /// methods for authentication, courses, notes, license management, the school-calendar
    /// scraper trigger, and the cascading reference data (cours / niveaux / domaines).
    ///
    /// Every method handles its own exceptions and returns a safe default (null, false, or empty
    /// list) rather than propagating exceptions to the calling component, making the UI resilient
    /// to transient network failures.
    ///
    /// The underlying HttpClient is injected through the named "API" factory registered in
    /// Program.cs. AuthHeaderHandler automatically attaches the JWT Bearer token to every request,
    /// so this service does not need to manage authentication headers manually.
    /// </summary>
    public class ApiService
    {
        /// <summary>
        /// The HTTP client pre-configured with the API base address and the JWT auth handler.
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Provides direct access to the stored JWT token for endpoints that need
        /// the Bearer header added manually (belt-and-suspenders alongside AuthHeaderHandler).
        /// </summary>
        private readonly AuthService _auth;

        /// <summary>
        /// Initialises the service with the HTTP client and auth service provided by DI.
        /// </summary>
        /// <param name="httpClient">The HTTP client used for all API calls.</param>
        /// <param name="auth">The auth service used to read the JWT token from localStorage.</param>
        public ApiService(HttpClient httpClient, AuthService auth)
        {
            _httpClient = httpClient;
            _auth = auth;
        }

        // ──────────────────────────────────────────────────────────────────
        // AUTHENTICATION
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Exchanges the temporary "auth_pending" HttpOnly cookie (set by the server after a
        /// successful OAuth redirect) for the application's JWT token and user data.
        /// The token is never passed through the URL; this endpoint reads the cookie server-side
        /// and returns the auth payload directly to the client.
        /// Called by AuthCallback.razor immediately after the OAuth provider redirects back.
        /// Endpoint: GET api/auth/exchange
        /// </summary>
        /// <returns>
        /// An <see cref="AuthResponse"/> with the JWT and user details on success; null on failure.
        /// </returns>
        public async Task<AuthResponse?> ExchangeOAuthTokenAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/auth/exchange");

                // Return the deserialised AuthResponse on success; null on any non-success status
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<AuthResponse>();

                return null;
            }
            catch
            {
                // Network or serialisation errors return null; the caller handles the redirect
                return null;
            }
        }

        /// <summary>
        /// Sends email/password credentials to the login endpoint and returns the server's
        /// authentication response if the credentials are valid.
        /// Endpoint: POST api/auth/login
        /// </summary>
        /// <param name="loginDto">DTO containing the user's email and password.</param>
        /// <returns>
        /// An <see cref="AuthResponse"/> with the JWT and user details on success;
        /// null when credentials are invalid or a network error occurs.
        /// </returns>
        public async Task<AuthResponse?> LoginAsync(LoginDto loginDto)
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginDto);

            // Deserialise and return the token payload on HTTP 200; null on any other status
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<AuthResponse>();

            return null;
        }

        /// <summary>
        /// Sends registration data to the server to create a new user account.
        /// The server validates password strength and email uniqueness.
        /// Endpoint: POST api/auth/register
        /// </summary>
        /// <param name="registerDto">DTO with all registration fields including password confirmation.</param>
        /// <returns>
        /// An <see cref="AuthResponse"/> (and JWT) for the new account on success;
        /// null on validation error or network failure.
        /// </returns>
        public async Task<AuthResponse?> RegisterAsync(RegisterDto registerDto)
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/register", registerDto);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<AuthResponse>();

            return null;
        }

        // ──────────────────────────────────────────────────────────────────
        // COURSES
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Retrieves the list of courses scheduled for a specific calendar date.
        /// The server filters recurring courses by their DaysOfWeek bitmask and date range.
        /// Endpoint: GET api/courses/date/{yyyy-MM-dd}
        /// </summary>
        /// <param name="date">The date for which to load courses.</param>
        /// <returns>A list of courses for that date, or an empty list on error.</returns>
        public async Task<List<Course>> GetCoursesForDateAsync(DateTime date)
        {
            try
            {
                // Format the date in ISO 8601 (yyyy-MM-dd) as expected by the API route
                return await _httpClient.GetFromJsonAsync<List<Course>>(
                    $"api/courses/date/{date:yyyy-MM-dd}") ?? new();
            }
            catch
            {
                // Return an empty list so the calendar renders without crashing on API errors
                return new();
            }
        }

        /// <summary>
        /// Retrieves all user notes whose date falls within the given inclusive date range.
        /// Used by week and month views to load notes for all displayed days in a single request,
        /// which is more efficient than one request per day.
        /// Endpoint: GET api/notes/range?start={yyyy-MM-dd}&end={yyyy-MM-dd}
        /// </summary>
        /// <param name="start">The first date of the range (inclusive).</param>
        /// <param name="end">The last date of the range (inclusive).</param>
        /// <returns>All notes whose date is within [start, end], or an empty list on error.</returns>
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

        /// <summary>
        /// Saves a new course or updates an existing one by posting it to the courses endpoint.
        /// The server determines create vs. update based on the Course.Id value.
        /// Endpoint: POST api/courses
        /// </summary>
        /// <param name="course">The course model to create or update.</param>
        public async Task SaveCourseAsync(Course course)
        {
            await _httpClient.PostAsJsonAsync("api/courses", course);
        }

        /// <summary>
        /// Permanently deletes a course by its server-assigned identifier.
        /// Endpoint: DELETE api/courses/{id}
        /// </summary>
        /// <param name="id">The unique identifier of the course to delete.</param>
        public async Task DeleteCourseAsync(int id)
        {
            await _httpClient.DeleteAsync($"api/courses/{id}");
        }

        // ──────────────────────────────────────────────────────────────────
        // NOTES
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Retrieves all notes for a single specific date.
        /// Used by the single-day view when only one day's notes are needed.
        /// Endpoint: GET api/notes/date/{yyyy-MM-dd}
        /// </summary>
        /// <param name="date">The date for which to load notes.</param>
        /// <returns>All notes for that date, or an empty list on error.</returns>
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

        /// <summary>
        /// Creates a new note or updates an existing one (determined by Note.Id being 0 or set).
        /// Returns a success flag and an optional error message so the UI can show inline feedback.
        /// Endpoint: POST api/notes
        /// </summary>
        /// <param name="note">The note to save. Id == 0 means a new note.</param>
        /// <returns>
        /// (true, null) on success;
        /// (false, errorMessage) when the server returns a non-success status or a network error occurs.
        /// </returns>
        public async Task<(bool Success, string? Error)> SaveNoteAsync(Note note)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/notes", note);

                if (response.IsSuccessStatusCode) return (true, null);

                // Read the raw response body to include the server's error description
                var body = await response.Content.ReadAsStringAsync();
                return (false, $"Error {(int)response.StatusCode}: {body}");
            }
            catch (Exception ex)
            {
                // Network or serialisation failure: surface the exception message
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Permanently deletes a note by its server-assigned identifier.
        /// Endpoint: DELETE api/notes/{id}
        /// </summary>
        /// <param name="id">The unique identifier of the note to delete.</param>
        public async Task DeleteNoteAsync(int id)
        {
            await _httpClient.DeleteAsync($"api/notes/{id}");
        }

        // ──────────────────────────────────────────────────────────────────
        // LICENSE — VALIDATION AND CHECK
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Submits a license access code entered by the user on the AccessCodePage for initial validation.
        /// If valid, the code is stored in local storage and used for subsequent CheckLicenseAsync calls.
        /// Endpoint: POST api/access/validate  (body: { code })
        /// </summary>
        /// <param name="code">The access code string typed by the user.</param>
        /// <returns>True if the server accepts the code as valid; false otherwise.</returns>
        public async Task<bool> ValidateAccessCodeAsync(string code)
        {
            try
            {
                // Send the code as a JSON object with a single "code" property
                var response = await _httpClient.PostAsJsonAsync("api/access/validate", new { code });
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates that a previously accepted license code is still active on the server.
        /// Called on every app startup (in MainLayout) to enforce real-time license revocation:
        /// if an admin revokes a license, the user is redirected to the access-code page on next load.
        /// Endpoint: GET api/access/check?code={code}
        /// </summary>
        /// <param name="code">The license code stored in local storage.</param>
        /// <returns>True if the license is still active; false when revoked or not found.</returns>
        public async Task<bool> CheckLicenseAsync(string code)
        {
            try
            {
                var response = await _httpClient.GetAsync(
                    $"api/access/check?code={Uri.EscapeDataString(code)}");

                if (!response.IsSuccessStatusCode) return false;

                // The server returns { "valid": true/false } as the response body
                var result = await response.Content.ReadFromJsonAsync<LicenseCheckResult>();
                return result?.Valid == true;
            }
            catch
            {
                return false;
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // ADMIN — LICENSE MANAGEMENT
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Retrieves the full list of all license records for display in the admin page.
        /// Only accessible to users with the ADMIN role (enforced server-side).
        /// Endpoint: GET api/admin/licenses
        /// </summary>
        /// <returns>A list of all <see cref="LicenseDto"/> records, or an empty list on error.</returns>
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

        /// <summary>
        /// Creates a new license with an optional label, expiry date, and custom code.
        /// If no custom code is provided, the server generates one automatically.
        /// Endpoint: POST api/admin/licenses  (body: { code, label, expiresAt })
        /// </summary>
        /// <param name="label">An optional human-readable label (e.g., "PROF-DUPONT").</param>
        /// <param name="expiresAt">An optional expiry date after which the license becomes inactive.</param>
        /// <param name="code">An optional custom code string; null lets the server auto-generate.</param>
        /// <returns>
        /// (LicenseDto, null) on success;
        /// (null, errorMessage) when creation fails, including the server's error message if available.
        /// </returns>
        public async Task<(LicenseDto? License, string? Error)> CreateLicenseAsync(
            string? label, DateTime? expiresAt, string? code = null)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    "api/admin/licenses", new { code, label, expiresAt });

                if (response.IsSuccessStatusCode)
                    return (await response.Content.ReadFromJsonAsync<LicenseDto>(), null);

                // Attempt to extract the structured error message from the JSON response body
                try
                {
                    var err = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                    var msg = err.GetProperty("message").GetString();
                    return (null, msg ?? $"Error {(int)response.StatusCode}");
                }
                catch
                {
                    // If the body cannot be parsed as JSON, fall back to the status code
                    return (null, $"Error {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        /// <summary>
        /// Revokes an active license, preventing any user assigned to that code from accessing the app.
        /// Endpoint: PUT api/admin/licenses/{id}/revoke  (no body)
        /// </summary>
        /// <param name="id">The unique identifier of the license to revoke.</param>
        /// <returns>True if the server accepted the revocation; false on failure.</returns>
        public async Task<bool> RevokeLicenseAsync(int id)
        {
            try
            {
                // PUT with a null body — the route itself identifies the action and target
                var response = await _httpClient.PutAsync($"api/admin/licenses/{id}/revoke", null);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Re-activates a previously revoked license.
        /// Endpoint: PUT api/admin/licenses/{id}/reactivate  (no body)
        /// </summary>
        /// <param name="id">The unique identifier of the license to reactivate.</param>
        /// <returns>True if the reactivation was accepted; false on failure.</returns>
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

        /// <summary>
        /// Permanently deletes a license record from the database.
        /// Endpoint: DELETE api/admin/licenses/{id}
        /// </summary>
        /// <param name="id">The unique identifier of the license to delete.</param>
        /// <returns>True if the deletion was accepted; false on failure.</returns>
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
        // ADMIN — SCHOOL CALENDAR SCRAPER
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Triggers the server-side scraper that fetches the latest school-year holiday dates
        /// from the official enseignement.be website and updates the database.
        /// This operation may take several seconds; the UI disables the button while it runs.
        /// Endpoint: GET api/update-scolaire
        /// </summary>
        /// <returns>
        /// (true, successMessage) when the scraper completes without error;
        /// (false, errorDescription) when the scraper fails or a network error occurs.
        /// </returns>
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
        // HEALTH CHECK
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Performs a lightweight health-check ping to the server to determine whether the
        /// backend is reachable. The result drives the online/offline badge in MainLayout.
        /// Endpoint: GET api/health
        /// </summary>
        /// <returns>True if the server responds with a 2xx status; false otherwise.</returns>
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
        // REFERENCE DATA — CASCADING DROPDOWNS (TestPage)
        // ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds an HttpRequestMessage with the Authorization Bearer header set from localStorage.
        /// Used by ref endpoints as a fallback to ensure the token is always attached,
        /// regardless of whether AuthHeaderHandler has already populated the header.
        /// </summary>
        /// <param name="method">The HTTP method (GET, POST, etc.)</param>
        /// <param name="url">The relative URL of the API endpoint.</param>
        private async Task<HttpRequestMessage> BuildAuthRequest(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);
            // Read the stored JWT token and attach it as a Bearer Authorization header
            var token = await _auth.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return request;
        }

        /// <summary>
        /// Retrieves the master list of all courses (cours) available in the reference database.
        /// Used to populate the first dropdown in the cascading-selection test page.
        /// Endpoint: GET api/ref/cours
        /// Throws HttpRequestException if the server returns a non-success status code.
        /// </summary>
        /// <returns>A list of <see cref="CoursDto"/> items.</returns>
        public async Task<List<CoursDto>> GetCoursAsync()
        {
            // Build the request with the Bearer token attached manually for reliability
            var request = await BuildAuthRequest(HttpMethod.Get, "api/ref/cours");
            var response = await _httpClient.SendAsync(request);
            // Throws on non-2xx so the caller can surface the real error (401 = auth, 404 = not deployed)
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<CoursDto>>() ?? new();
        }

        /// <summary>
        /// Retrieves the levels (niveaux) available for a specific course code.
        /// Called when the user selects a course in the first dropdown on the test page.
        /// Endpoint: GET api/ref/niveaux/{codeCours}
        /// Throws HttpRequestException if the server returns a non-success status code.
        /// </summary>
        /// <param name="codeCours">The course code (e.g., "LM", "SC") to filter levels by.</param>
        /// <returns>A list of <see cref="NiveauDto"/> items.</returns>
        public async Task<List<NiveauDto>> GetNiveauxAsync(string codeCours)
        {
            var request = await BuildAuthRequest(HttpMethod.Get, $"api/ref/niveaux/{codeCours}");
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<NiveauDto>>() ?? new();
        }

        /// <summary>
        /// Retrieves the domains (domaines) available for a specific course and level combination.
        /// Called when the user selects a level in the second dropdown on the test page.
        /// Endpoint: GET api/ref/domaines/{codeCours}/{codeNiveau}
        /// Throws HttpRequestException if the server returns a non-success status code.
        /// </summary>
        /// <param name="codeCours">The course code used to filter domains.</param>
        /// <param name="codeNiveau">The level code used together with the course to filter domains.</param>
        /// <returns>A list of <see cref="DomaineDto"/> items.</returns>
        public async Task<List<DomaineDto>> GetDomainesAsync(string codeCours, string codeNiveau)
        {
            var request = await BuildAuthRequest(HttpMethod.Get, $"api/ref/domaines/{codeCours}/{codeNiveau}");
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<DomaineDto>>() ?? new();
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // SUPPORTING DTOs AND RESULT TYPES
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Represents the JSON body returned by the GET api/access/check endpoint.
    /// Contains a single boolean property indicating whether the license code is still active.
    /// </summary>
    public class LicenseCheckResult
    {
        /// <summary>
        /// True when the license code is valid and has not been revoked or expired.
        /// </summary>
        [JsonPropertyName("valid")]
        public bool Valid { get; set; }
    }

    /// <summary>
    /// Data Transfer Object that represents a license record returned by the admin API.
    /// All property names are explicitly mapped to their camelCase JSON counterparts
    /// so that .NET's PascalCase defaults do not cause deserialisation mismatches.
    /// </summary>
    public class LicenseDto
    {
        /// <summary>The server-assigned unique identifier for this license.</summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>The unique access code string for this license (e.g., "ABCDE-23456").</summary>
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>An optional human-readable label assigned by the admin (e.g., "PROF-DUPONT").</summary>
        [JsonPropertyName("label")]
        public string? Label { get; set; }

        /// <summary>True when the license is currently active and grants access to the application.</summary>
        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }

        /// <summary>
        /// A user-friendly status string (e.g., "Active", "Révoqué", "Expiré") derived server-side.
        /// Used to display a coloured badge in the license list.
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// The email address of the user who has been assigned this license, or null if not yet assigned.
        /// </summary>
        [JsonPropertyName("assignedEmail")]
        public string? AssignedEmail { get; set; }

        /// <summary>The UTC timestamp when this license was created.</summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// The optional UTC datetime after which this license automatically becomes inactive.
        /// Null means the license does not expire.
        /// </summary>
        [JsonPropertyName("expiresAt")]
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// The UTC timestamp when the license was first used/assigned to a user account.
        /// Null when the license has never been redeemed.
        /// </summary>
        [JsonPropertyName("assignedAt")]
        public DateTime? AssignedAt { get; set; }
    }

    /// <summary>
    /// Data Transfer Object representing a course entry from the reference database.
    /// Used to populate the cascading course dropdown on the test page.
    /// </summary>
    public class CoursDto
    {
        /// <summary>The short course code used as the unique key (e.g., "LM", "SC", "MA").</summary>
        [JsonPropertyName("codeCours")]
        public string CodeCours { get; set; } = string.Empty;

        /// <summary>The full display name of the course (e.g., "Langues Modernes").</summary>
        [JsonPropertyName("nomCours")]
        public string NomCours { get; set; } = string.Empty;

        /// <summary>
        /// Optional CSS color string used to display this course in the agenda view.
        /// May be null when no color has been configured.
        /// </summary>
        [JsonPropertyName("couleurAgenda")]
        public string? CouleurAgenda { get; set; }
    }

    /// <summary>
    /// Data Transfer Object representing an educational level within a course.
    /// Used to populate the second dropdown after a course has been selected.
    /// </summary>
    public class NiveauDto
    {
        /// <summary>The short code identifying this level (e.g., "1A", "2B").</summary>
        [JsonPropertyName("codeNiveau")]
        public string CodeNiveau { get; set; } = string.Empty;

        /// <summary>The human-readable name of the level (e.g., "Première Année").</summary>
        [JsonPropertyName("nomNiveau")]
        public string NomNiveau { get; set; } = string.Empty;
    }

    /// <summary>
    /// Data Transfer Object representing a pedagogical domain within a course and level.
    /// Used to populate the third dropdown after both a course and a level have been selected.
    /// </summary>
    public class DomaineDto
    {
        /// <summary>The server-assigned unique identifier for this domain.</summary>
        [JsonPropertyName("idDom")]
        public int IdDom { get; set; }

        /// <summary>The display name of the domain (e.g., "Compréhension écrite").</summary>
        [JsonPropertyName("nom")]
        public string Nom { get; set; } = string.Empty;
    }
}
