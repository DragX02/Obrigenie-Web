using Blazored.LocalStorage;

namespace Obrigenie.Services
{
    public class AuthService
    {
        private readonly ILocalStorageService _localStorage;
        private const string TokenKey = "jwt_token";
        private const string EmailKey = "user_email";

        public AuthService(ILocalStorageService localStorage)
        {
            _localStorage = localStorage;
        }

        public async Task SaveTokenAsync(string token)
        {
            await _localStorage.SetItemAsStringAsync(TokenKey, token);
        }

        public async Task<string?> GetTokenAsync()
        {
            return await _localStorage.GetItemAsStringAsync(TokenKey);
        }

        public async Task SaveEmailAsync(string email)
        {
            await _localStorage.SetItemAsStringAsync(EmailKey, email);
        }

        public async Task<string?> GetEmailAsync()
        {
            return await _localStorage.GetItemAsStringAsync(EmailKey);
        }

        public async Task RemoveTokenAsync()
        {
            await _localStorage.RemoveItemAsync(TokenKey);
            await _localStorage.RemoveItemAsync(EmailKey);
        }

        public async Task<bool> IsLoggedInAsync()
        {
            var token = await GetTokenAsync();
            return !string.IsNullOrEmpty(token);
        }

        // Décode le JWT côté client pour lire le rôle (ClaimTypes.Role → "role" dans le payload)
        public async Task<string?> GetRoleAsync()
        {
            var token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return null;
            try
            {
                var parts = token.Split('.');
                if (parts.Length != 3) return null;
                var payload = parts[1];
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                // ASP.NET Core JwtSecurityTokenHandler mappe ClaimTypes.Role → "role"
                if (doc.RootElement.TryGetProperty("role", out var role))
                    return role.GetString();
            }
            catch { }
            return null;
        }
    }
}
