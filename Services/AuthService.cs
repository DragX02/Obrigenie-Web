using Blazored.LocalStorage;

namespace Obrigenie.Services
{
    /// <summary>
    /// Manages authentication state for the Blazor WebAssembly client.
    /// Provides methods to persist and retrieve the JWT token and user email in the browser's
    /// local storage, to check whether the user is currently logged in, and to decode the
    /// user's role from the JWT payload without requiring a server round-trip.
    /// This service is registered as Scoped in Program.cs and injected into pages and layouts
    /// that need to react to authentication state.
    /// </summary>
    public class AuthService
    {
        /// <summary>
        /// The Blazored.LocalStorage service used to read and write values in browser local storage.
        /// Injected via constructor dependency injection.
        /// </summary>
        private readonly ILocalStorageService _localStorage;

        /// <summary>
        /// The local storage key under which the JWT bearer token is stored.
        /// </summary>
        private const string TokenKey = "jwt_token";

        /// <summary>
        /// The local storage key under which the authenticated user's email address is stored.
        /// </summary>
        private const string EmailKey = "user_email";

        /// <summary>
        /// Initialises the service with the required local storage dependency.
        /// </summary>
        /// <param name="localStorage">The Blazored local storage abstraction.</param>
        public AuthService(ILocalStorageService localStorage)
        {
            _localStorage = localStorage;
        }

        /// <summary>
        /// Persists the JWT bearer token to browser local storage so that it survives page refreshes.
        /// Called immediately after a successful login or registration response is received.
        /// </summary>
        /// <param name="token">The JWT token string returned by the server.</param>
        public async Task SaveTokenAsync(string token)
        {
            // Write the token as a raw string (not JSON-serialised) to keep it compact
            await _localStorage.SetItemAsStringAsync(TokenKey, token);
        }

        /// <summary>
        /// Retrieves the stored JWT bearer token from browser local storage.
        /// Returns null if no token has been saved (i.e., the user is not logged in).
        /// </summary>
        /// <returns>The stored token string, or null if absent.</returns>
        public async Task<string?> GetTokenAsync()
        {
            return await _localStorage.GetItemAsStringAsync(TokenKey);
        }

        /// <summary>
        /// Persists the authenticated user's email address to browser local storage.
        /// Called alongside SaveTokenAsync so the email is available for display without
        /// requiring a separate API call.
        /// </summary>
        /// <param name="email">The email address of the authenticated user.</param>
        public async Task SaveEmailAsync(string email)
        {
            await _localStorage.SetItemAsStringAsync(EmailKey, email);
        }

        /// <summary>
        /// Retrieves the stored email address from browser local storage.
        /// Returns null if the email has not been saved.
        /// </summary>
        /// <returns>The stored email address, or null if absent.</returns>
        public async Task<string?> GetEmailAsync()
        {
            return await _localStorage.GetItemAsStringAsync(EmailKey);
        }

        /// <summary>
        /// Removes both the JWT token and the stored email address from local storage,
        /// effectively logging the user out on the client side.
        /// Called by the logout button in MainLayout and the AccessCodePage.
        /// </summary>
        public async Task RemoveTokenAsync()
        {
            // Remove the token so IsLoggedInAsync returns false on the next check
            await _localStorage.RemoveItemAsync(TokenKey);
            // Also remove the cached email to avoid stale data being shown after re-login
            await _localStorage.RemoveItemAsync(EmailKey);
        }

        /// <summary>
        /// Checks whether the user is currently authenticated by verifying that a non-empty
        /// JWT token exists in local storage. Does not validate the token's signature or expiry.
        /// </summary>
        /// <returns>True if a token is present; false otherwise.</returns>
        public async Task<bool> IsLoggedInAsync()
        {
            var token = await GetTokenAsync();
            // A non-null, non-empty token string is treated as proof of a logged-in session
            return !string.IsNullOrEmpty(token);
        }

        /// <summary>
        /// Decodes the JWT payload on the client side to extract the user's role claim.
        /// This avoids a server round-trip for role-based UI decisions (e.g., showing the
        /// admin panel). Note: the signature is NOT verified client-side; the server still
        /// validates the token on every protected API call.
        ///
        /// The JWT format is three base64url-encoded parts separated by dots:
        ///   header.payload.signature
        ///
        /// ASP.NET Core's JwtSecurityTokenHandler maps ClaimTypes.Role to the short key "role"
        /// in the JSON payload, which is what this method reads.
        /// </summary>
        /// <returns>
        /// The role string (e.g., "ADMIN", "PROF") extracted from the token,
        /// or null if no token exists, the token is malformed, or the "role" claim is absent.
        /// </returns>
        public async Task<string?> GetRoleAsync()
        {
            var token = await GetTokenAsync();

            // No token means the user is not logged in; return null immediately
            if (string.IsNullOrEmpty(token)) return null;

            try
            {
                // Split the JWT into its three dot-separated components
                var parts = token.Split('.');

                // A valid JWT must have exactly 3 parts; anything else is malformed
                if (parts.Length != 3) return null;

                // The second part (index 1) is the Base64url-encoded JSON payload
                var payload = parts[1];

                // Base64url uses '-' and '_' instead of '+' and '/'; padding '=' may be stripped.
                // Re-add the required '=' padding so Convert.FromBase64String can decode it.
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

                // Decode the base64 bytes and interpret them as UTF-8 JSON
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));

                // Parse the JSON and look for the "role" property
                using var doc = System.Text.Json.JsonDocument.Parse(json);

                // ASP.NET Core JwtSecurityTokenHandler maps ClaimTypes.Role to the key "role"
                if (doc.RootElement.TryGetProperty("role", out var role))
                    return role.GetString();
            }
            catch
            {
                // Any decoding or parsing error (invalid base64, malformed JSON, etc.) is
                // silently swallowed; the method returns null to indicate an unknown role.
            }

            return null;
        }
    }
}
