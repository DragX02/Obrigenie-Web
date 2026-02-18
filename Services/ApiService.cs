using Obrigenie.Models;
using System.Net.Http.Json;

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

        public async Task SaveNoteAsync(Note note)
        {
            await _httpClient.PostAsJsonAsync("api/notes", note);
        }

        public async Task DeleteNoteAsync(int id)
        {
            await _httpClient.DeleteAsync($"api/notes/{id}");
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
}
