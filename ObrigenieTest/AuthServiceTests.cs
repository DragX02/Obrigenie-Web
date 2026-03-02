using Blazored.LocalStorage;
using Moq;
using Obrigenie.Services;

namespace ObrigenieTest;

/// <summary>
/// Unit tests for the <see cref="AuthService"/> class.
/// Covers:
///   - GetRoleAsync: client-side JWT payload decoding for the "role" claim.
///   - IsLoggedInAsync: presence check for the stored JWT token.
///   - SaveTokenAsync: verifies that the correct localStorage key is written.
///   - RemoveTokenAsync: verifies that both the token and email keys are removed.
///
/// <see cref="ILocalStorageService"/> is mocked with Moq so no browser APIs
/// or real localStorage access is required. All tests run in a standard
/// xUnit test host without a browser.
/// </summary>
public class AuthServiceTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Test helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a syntactically valid JWT string whose payload encodes a single
    /// <c>"role"</c> claim with the given value.
    /// The header uses a real HS256 algorithm identifier, but the signature is
    /// a fake placeholder because <see cref="AuthService.GetRoleAsync"/> does
    /// not verify the signature on the client side.
    /// </summary>
    /// <param name="role">The role value to embed in the JWT payload (e.g., "ADMIN").</param>
    /// <returns>A three-part dot-separated JWT string.</returns>
    private static string MakeJwt(string role)
    {
        // Build a minimal JSON payload containing only the role claim
        var payloadJson = $"{{\"role\":\"{role}\"}}";

        // Base64url-encode the payload (no padding, URL-safe characters)
        var payloadB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payloadJson))
                             .TrimEnd('=')           // remove Base64 padding
                             .Replace('+', '-')      // URL-safe: + → -
                             .Replace('/', '_');      // URL-safe: / → _

        // Combine with a fixed HS256 header and a fake signature segment
        return $"eyJhbGciOiJIUzI1NiJ9.{payloadB64}.FAKE_SIG";
    }

    /// <summary>
    /// Creates an <see cref="AuthService"/> instance backed by a mock
    /// <see cref="ILocalStorageService"/> that returns the specified token
    /// whenever <c>GetItemAsStringAsync("jwt_token")</c> is called.
    /// </summary>
    /// <param name="storedToken">The token to return, or null to simulate no stored token.</param>
    /// <returns>A configured <see cref="AuthService"/> instance.</returns>
    private static AuthService CreateService(string? storedToken)
    {
        var mock = new Mock<ILocalStorageService>();

        // Set up the mock to return the given token for the JWT key
        mock.Setup(s => s.GetItemAsStringAsync("jwt_token", default))
            .ReturnsAsync(storedToken);

        return new AuthService(mock.Object);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetRoleAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a JWT with role "ADMIN" causes GetRoleAsync to return "ADMIN".
    /// </summary>
    [Fact]
    public async Task GetRoleAsync_AdminToken_ReturnsAdmin()
    {
        var svc = CreateService(MakeJwt("ADMIN"));

        // The decoded role claim must equal "ADMIN"
        Assert.Equal("ADMIN", await svc.GetRoleAsync());
    }

    /// <summary>
    /// Verifies that a JWT with role "PROF" causes GetRoleAsync to return "PROF".
    /// Ensures that the decoding is not hard-coded for the admin role.
    /// </summary>
    [Fact]
    public async Task GetRoleAsync_ProfToken_ReturnsProf()
    {
        var svc = CreateService(MakeJwt("PROF"));

        Assert.Equal("PROF", await svc.GetRoleAsync());
    }

    /// <summary>
    /// Verifies that GetRoleAsync returns null when no token is stored in localStorage.
    /// </summary>
    [Fact]
    public async Task GetRoleAsync_NoToken_ReturnsNull()
    {
        var svc = CreateService(null);  // null simulates no stored token

        Assert.Null(await svc.GetRoleAsync());
    }

    /// <summary>
    /// Verifies that GetRoleAsync returns null when the stored string is not a valid JWT
    /// (wrong number of dot-separated segments).
    /// </summary>
    [Fact]
    public async Task GetRoleAsync_InvalidToken_ReturnsNull()
    {
        // A string with more than three segments is not a valid JWT
        var svc = CreateService("not.a.valid.jwt.at.all");

        Assert.Null(await svc.GetRoleAsync());
    }

    /// <summary>
    /// Verifies that GetRoleAsync returns null when the payload segment is not
    /// valid Base64 and therefore cannot be decoded.
    /// </summary>
    [Fact]
    public async Task GetRoleAsync_MalformedPayload_ReturnsNull()
    {
        // "!!!" is not valid Base64url — decoding should fail silently
        var svc = CreateService("header.!!!INVALID_BASE64!!!.sig");

        Assert.Null(await svc.GetRoleAsync());
    }

    /// <summary>
    /// Verifies that GetRoleAsync returns null when the JWT payload is valid JSON
    /// but does not contain the "role" claim at all.
    /// </summary>
    [Fact]
    public async Task GetRoleAsync_PayloadWithoutRoleClaim_ReturnsNull()
    {
        // Build a payload with "sub" and "exp" but no "role" property
        var payloadJson = "{\"sub\":\"user@test.com\",\"exp\":9999999999}";
        var payloadB64  = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payloadJson))
                              .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var token = $"eyJhbGciOiJIUzI1NiJ9.{payloadB64}.sig";

        var svc = CreateService(token);

        // No "role" property in the payload → must return null
        Assert.Null(await svc.GetRoleAsync());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IsLoggedInAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that IsLoggedInAsync returns true when a non-empty token exists.
    /// </summary>
    [Fact]
    public async Task IsLoggedInAsync_WithToken_ReturnsTrue()
    {
        // Any non-empty string is treated as "logged in"
        var svc = CreateService(MakeJwt("PROF"));

        Assert.True(await svc.IsLoggedInAsync());
    }

    /// <summary>
    /// Verifies that IsLoggedInAsync returns false when no token is stored (null).
    /// </summary>
    [Fact]
    public async Task IsLoggedInAsync_NoToken_ReturnsFalse()
    {
        var svc = CreateService(null);

        Assert.False(await svc.IsLoggedInAsync());
    }

    /// <summary>
    /// Verifies that IsLoggedInAsync returns false when the stored token is an empty string.
    /// An empty string is not a valid JWT and must be treated the same as null.
    /// </summary>
    [Fact]
    public async Task IsLoggedInAsync_EmptyToken_ReturnsFalse()
    {
        var svc = CreateService("");

        Assert.False(await svc.IsLoggedInAsync());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SaveTokenAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that SaveTokenAsync calls <c>SetItemAsStringAsync</c> on the localStorage mock
    /// exactly once, using the correct key ("jwt_token") and the provided token value.
    /// </summary>
    [Fact]
    public async Task SaveTokenAsync_CallsLocalStorage()
    {
        var mock = new Mock<ILocalStorageService>();
        var svc  = new AuthService(mock.Object);

        await svc.SaveTokenAsync("my_token");

        // Verify the exact call was made with the expected key and value
        mock.Verify(s => s.SetItemAsStringAsync("jwt_token", "my_token", default), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RemoveTokenAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that RemoveTokenAsync removes both the "jwt_token" and "user_email" keys
    /// from localStorage, effectively clearing all authentication state from the browser.
    /// </summary>
    [Fact]
    public async Task RemoveTokenAsync_RemovesBothTokenAndEmail()
    {
        var mock = new Mock<ILocalStorageService>();
        var svc  = new AuthService(mock.Object);

        await svc.RemoveTokenAsync();

        // Both keys must be removed exactly once
        mock.Verify(s => s.RemoveItemAsync("jwt_token",  default), Times.Once);
        mock.Verify(s => s.RemoveItemAsync("user_email", default), Times.Once);
    }
}
