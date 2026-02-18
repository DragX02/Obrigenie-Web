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
    }
}
