using Blazored.LocalStorage;
using Moq;
using Obrigenie.Models;
using Obrigenie.Services;
using System.Net;
using System.Text.Json;

namespace ObrigenieTest;

/// <summary>
/// Integration tests for <see cref="ApiService"/>.
///
/// Rather than mocking ApiService itself, these tests wire up a real ApiService
/// instance against a <see cref="FakeHandler"/> that returns pre-configured HTTP
/// responses. This exercises the full request/response cycle — URL construction,
/// JSON serialization of request bodies, JSON deserialization of responses, and
/// all error-handling branches — without requiring a live backend.
///
/// Test groups:
///   - LoginAsync / RegisterAsync / ExchangeOAuthTokenAsync   (authentication)
///   - GetCoursesForDateAsync / GetNotesForDateAsync / GetNotesForRangeAsync  (read data)
///   - SaveNoteAsync / DeleteNoteAsync / SaveCourseAsync / DeleteCourseAsync  (write data)
///   - ValidateAccessCodeAsync / CheckLicenseAsync            (licence)
///   - RevokeLicenseAsync / ReactivateLicenseAsync / DeleteLicenseAsync       (admin)
///   - CheckHealthAsync / TriggerScraperAsync                 (infrastructure)
/// </summary>
public class ApiServiceIntegrationTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // HTTP mock infrastructure
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a fixed HTTP response for every request, without making real network calls.
    /// </summary>
    private class FakeHandler(HttpStatusCode status, string body = "") : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            });
    }

    /// <summary>
    /// Throws <see cref="HttpRequestException"/> unconditionally to simulate a network failure.
    /// </summary>
    private class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Simulated network failure");
    }

    /// <summary>Creates an <see cref="AuthService"/> backed by a mock localStorage.</summary>
    private static AuthService CreateAuth(string? token = null)
    {
        var mock = new Mock<ILocalStorageService>();
        mock.Setup(s => s.GetItemAsStringAsync("jwt_token", default)).ReturnsAsync(token);
        return new AuthService(mock.Object);
    }

    /// <summary>Creates an <see cref="ApiService"/> that returns the given status and body.</summary>
    private static ApiService Create(HttpStatusCode status, string body, string? token = null)
    {
        var http = new HttpClient(new FakeHandler(status, body))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        return new ApiService(http, CreateAuth(token));
    }

    /// <summary>Creates an <see cref="ApiService"/> whose HttpClient always throws.</summary>
    private static ApiService CreateThrowing(string? token = null)
    {
        var http = new HttpClient(new ThrowingHandler())
        {
            BaseAddress = new Uri("http://localhost/")
        };
        return new ApiService(http, CreateAuth(token));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LoginAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a 200 OK response with a valid JSON body is deserialized
    /// into a non-null <see cref="AuthResponse"/> with the expected Token and Email.
    /// </summary>
    [Fact]
    public async Task LoginAsync_Ok_ReturnsAuthResponse()
    {
        var body = JsonSerializer.Serialize(new { token = "jwt123", email = "prof@school.be", nom = "Doe", prenom = "John" });
        var svc  = Create(HttpStatusCode.OK, body);

        var result = await svc.LoginAsync(new LoginDto { Email = "prof@school.be", Password = "pass" });

        Assert.NotNull(result);
        Assert.Equal("jwt123", result.Token);
        Assert.Equal("prof@school.be", result.Email);
    }

    /// <summary>
    /// Verifies that a 401 Unauthorized response causes LoginAsync to return null.
    /// </summary>
    [Fact]
    public async Task LoginAsync_Unauthorized_ReturnsNull()
    {
        var svc = Create(HttpStatusCode.Unauthorized, "");

        var result = await svc.LoginAsync(new LoginDto { Email = "x@x.com", Password = "wrong" });

        Assert.Null(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RegisterAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a 200 OK response deserializes into a non-null AuthResponse
    /// with the expected token.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_Ok_ReturnsAuthResponse()
    {
        var body = JsonSerializer.Serialize(new { token = "newjwt", email = "new@school.be", nom = "New", prenom = "User" });
        var svc  = Create(HttpStatusCode.OK, body);

        var dto    = new RegisterDto { Email = "new@school.be", Password = "Pass1!", ConfirmPassword = "Pass1!", Nom = "New", Prenom = "User" };
        var result = await svc.RegisterAsync(dto);

        Assert.NotNull(result);
        Assert.Equal("newjwt", result.Token);
    }

    /// <summary>
    /// Verifies that a 409 Conflict (duplicate email) causes RegisterAsync to return null.
    /// </summary>
    [Fact]
    public async Task RegisterAsync_Conflict_ReturnsNull()
    {
        var svc = Create(HttpStatusCode.Conflict, "");

        var dto = new RegisterDto { Email = "dup@school.be", Password = "Pass1!", ConfirmPassword = "Pass1!", Nom = "X", Prenom = "Y" };

        Assert.Null(await svc.RegisterAsync(dto));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ExchangeOAuthTokenAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a 200 OK exchange response returns a populated AuthResponse.
    /// </summary>
    [Fact]
    public async Task ExchangeOAuthTokenAsync_Ok_ReturnsAuthResponse()
    {
        var body = JsonSerializer.Serialize(new { token = "oauthjwt", email = "oauth@school.be" });
        var svc  = Create(HttpStatusCode.OK, body);

        var result = await svc.ExchangeOAuthTokenAsync();

        Assert.NotNull(result);
        Assert.Equal("oauthjwt", result.Token);
    }

    /// <summary>
    /// Verifies that a network failure during OAuth exchange causes the method to return null
    /// instead of propagating the exception.
    /// </summary>
    [Fact]
    public async Task ExchangeOAuthTokenAsync_NetworkError_ReturnsNull()
    {
        var result = await CreateThrowing().ExchangeOAuthTokenAsync();

        Assert.Null(result);
    }

    /// <summary>
    /// Verifies that a non-success HTTP status (e.g., 401) causes the method to return null.
    /// </summary>
    [Fact]
    public async Task ExchangeOAuthTokenAsync_Unauthorized_ReturnsNull()
    {
        var svc = Create(HttpStatusCode.Unauthorized, "");

        Assert.Null(await svc.ExchangeOAuthTokenAsync());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetCoursesForDateAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a 200 OK response with a JSON array deserializes into a non-empty list.
    /// </summary>
    [Fact]
    public async Task GetCoursesForDateAsync_Ok_ReturnsCourseList()
    {
        var body = JsonSerializer.Serialize(new[]
        {
            new { id = 1, name = "Maths", daysOfWeek = 1, startTime = "08:00", endTime = "09:00", color = "#fff", startDate = "2025-09-01", endDate = "2026-06-30" }
        });
        var svc = Create(HttpStatusCode.OK, body);

        var result = await svc.GetCoursesForDateAsync(new DateTime(2025, 10, 6));

        Assert.NotEmpty(result);
    }

    /// <summary>
    /// Verifies that a 500 Internal Server Error causes the method to return an empty list
    /// rather than throwing.
    /// </summary>
    [Fact]
    public async Task GetCoursesForDateAsync_ServerError_ReturnsEmptyList()
    {
        var svc = Create(HttpStatusCode.InternalServerError, "");

        Assert.Empty(await svc.GetCoursesForDateAsync(DateTime.Today));
    }

    /// <summary>
    /// Verifies that a network failure causes the method to return an empty list.
    /// </summary>
    [Fact]
    public async Task GetCoursesForDateAsync_NetworkError_ReturnsEmptyList()
    {
        Assert.Empty(await CreateThrowing().GetCoursesForDateAsync(DateTime.Today));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetNotesForDateAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a 200 OK response with a JSON note array returns a non-empty list.
    /// </summary>
    [Fact]
    public async Task GetNotesForDateAsync_Ok_ReturnsNotes()
    {
        var body = JsonSerializer.Serialize(new[]
        {
            new { id = 1, date = "2025-10-06T00:00:00", content = "Bring textbook", hour = 8, endHour = 9, createdAt = "2025-10-01T00:00:00", modifiedAt = "2025-10-01T00:00:00" }
        });
        var svc = Create(HttpStatusCode.OK, body);

        var result = await svc.GetNotesForDateAsync(new DateTime(2025, 10, 6));

        Assert.NotEmpty(result);
    }

    /// <summary>
    /// Verifies that a 500 response returns an empty list without throwing.
    /// </summary>
    [Fact]
    public async Task GetNotesForDateAsync_ServerError_ReturnsEmptyList()
    {
        Assert.Empty(await Create(HttpStatusCode.InternalServerError, "").GetNotesForDateAsync(DateTime.Today));
    }

    /// <summary>
    /// Verifies that a network error returns an empty list without throwing.
    /// </summary>
    [Fact]
    public async Task GetNotesForDateAsync_NetworkError_ReturnsEmptyList()
    {
        Assert.Empty(await CreateThrowing().GetNotesForDateAsync(DateTime.Today));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetNotesForRangeAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a 200 OK response with a note array for a date range returns a non-empty list.
    /// </summary>
    [Fact]
    public async Task GetNotesForRangeAsync_Ok_ReturnsNotes()
    {
        var body = JsonSerializer.Serialize(new[]
        {
            new { id = 2, date = "2025-10-07T00:00:00", content = "Chapter 5", hour = 10, endHour = 11, createdAt = "2025-10-01T00:00:00", modifiedAt = "2025-10-01T00:00:00" }
        });
        var svc = Create(HttpStatusCode.OK, body);

        var result = await svc.GetNotesForRangeAsync(new DateTime(2025, 10, 6), new DateTime(2025, 10, 12));

        Assert.NotEmpty(result);
    }

    /// <summary>
    /// Verifies that a network error during range query returns an empty list.
    /// </summary>
    [Fact]
    public async Task GetNotesForRangeAsync_NetworkError_ReturnsEmptyList()
    {
        Assert.Empty(await CreateThrowing().GetNotesForRangeAsync(DateTime.Today, DateTime.Today.AddDays(7)));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SaveNoteAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a 200 OK response returns (true, null) — success with no error message.
    /// </summary>
    [Fact]
    public async Task SaveNoteAsync_Ok_ReturnsSuccess()
    {
        var svc = Create(HttpStatusCode.OK, "");

        var (success, error) = await svc.SaveNoteAsync(new Note { Id = 0, Content = "Test note" });

        Assert.True(success);
        Assert.Null(error);
    }

    /// <summary>
    /// Verifies that a 400 Bad Request response returns (false, errorMessage)
    /// where errorMessage contains the HTTP status code.
    /// </summary>
    [Fact]
    public async Task SaveNoteAsync_BadRequest_ReturnsError()
    {
        var svc = Create(HttpStatusCode.BadRequest, "Content required");

        var (success, error) = await svc.SaveNoteAsync(new Note { Content = "" });

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Contains("400", error);
    }

    /// <summary>
    /// Verifies that a network failure during note save returns (false, exceptionMessage).
    /// </summary>
    [Fact]
    public async Task SaveNoteAsync_NetworkError_ReturnsError()
    {
        var (success, error) = await CreateThrowing().SaveNoteAsync(new Note { Content = "x" });

        Assert.False(success);
        Assert.NotNull(error);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ValidateAccessCodeAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a 200 OK response from the validation endpoint returns true.
    /// </summary>
    [Fact]
    public async Task ValidateAccessCodeAsync_Ok_ReturnsTrue()
    {
        Assert.True(await Create(HttpStatusCode.OK, "").ValidateAccessCodeAsync("VALID-CODE"));
    }

    /// <summary>
    /// Verifies that a 404 Not Found response returns false.
    /// </summary>
    [Fact]
    public async Task ValidateAccessCodeAsync_NotFound_ReturnsFalse()
    {
        Assert.False(await Create(HttpStatusCode.NotFound, "").ValidateAccessCodeAsync("BAD-CODE"));
    }

    /// <summary>
    /// Verifies that a network failure returns false instead of throwing.
    /// </summary>
    [Fact]
    public async Task ValidateAccessCodeAsync_NetworkError_ReturnsFalse()
    {
        Assert.False(await CreateThrowing().ValidateAccessCodeAsync("CODE"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CheckLicenseAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a 200 OK response with { "valid": true } returns true.
    /// </summary>
    [Fact]
    public async Task CheckLicenseAsync_ValidLicense_ReturnsTrue()
    {
        var body = JsonSerializer.Serialize(new { valid = true });

        Assert.True(await Create(HttpStatusCode.OK, body).CheckLicenseAsync("ACTIVE-CODE"));
    }

    /// <summary>
    /// Verifies that a 200 OK response with { "valid": false } returns false
    /// (license was revoked server-side).
    /// </summary>
    [Fact]
    public async Task CheckLicenseAsync_RevokedLicense_ReturnsFalse()
    {
        var body = JsonSerializer.Serialize(new { valid = false });

        Assert.False(await Create(HttpStatusCode.OK, body).CheckLicenseAsync("REVOKED-CODE"));
    }

    /// <summary>
    /// Verifies that a 500 Internal Server Error returns false.
    /// </summary>
    [Fact]
    public async Task CheckLicenseAsync_ServerError_ReturnsFalse()
    {
        Assert.False(await Create(HttpStatusCode.InternalServerError, "").CheckLicenseAsync("CODE"));
    }

    /// <summary>
    /// Verifies that a network failure returns false instead of propagating the exception.
    /// </summary>
    [Fact]
    public async Task CheckLicenseAsync_NetworkError_ReturnsFalse()
    {
        Assert.False(await CreateThrowing().CheckLicenseAsync("CODE"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RevokeLicenseAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a 200 OK response from the revoke endpoint returns true.
    /// </summary>
    [Fact]
    public async Task RevokeLicenseAsync_Ok_ReturnsTrue()
    {
        Assert.True(await Create(HttpStatusCode.OK, "").RevokeLicenseAsync(42));
    }

    /// <summary>
    /// Verifies that a 404 Not Found (license does not exist) returns false.
    /// </summary>
    [Fact]
    public async Task RevokeLicenseAsync_NotFound_ReturnsFalse()
    {
        Assert.False(await Create(HttpStatusCode.NotFound, "").RevokeLicenseAsync(99));
    }

    /// <summary>
    /// Verifies that a network failure during revoke returns false.
    /// </summary>
    [Fact]
    public async Task RevokeLicenseAsync_NetworkError_ReturnsFalse()
    {
        Assert.False(await CreateThrowing().RevokeLicenseAsync(1));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ReactivateLicenseAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a 200 OK response from the reactivate endpoint returns true.
    /// </summary>
    [Fact]
    public async Task ReactivateLicenseAsync_Ok_ReturnsTrue()
    {
        Assert.True(await Create(HttpStatusCode.OK, "").ReactivateLicenseAsync(42));
    }

    /// <summary>
    /// Verifies that a 500 Internal Server Error returns false.
    /// </summary>
    [Fact]
    public async Task ReactivateLicenseAsync_ServerError_ReturnsFalse()
    {
        Assert.False(await Create(HttpStatusCode.InternalServerError, "").ReactivateLicenseAsync(42));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DeleteLicenseAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a 200 OK response from the delete endpoint returns true.
    /// </summary>
    [Fact]
    public async Task DeleteLicenseAsync_Ok_ReturnsTrue()
    {
        Assert.True(await Create(HttpStatusCode.OK, "").DeleteLicenseAsync(10));
    }

    /// <summary>
    /// Verifies that a 404 Not Found (license already deleted or unknown) returns false.
    /// </summary>
    [Fact]
    public async Task DeleteLicenseAsync_NotFound_ReturnsFalse()
    {
        Assert.False(await Create(HttpStatusCode.NotFound, "").DeleteLicenseAsync(99));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CheckHealthAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a 200 OK health response returns true (backend reachable).
    /// </summary>
    [Fact]
    public async Task CheckHealthAsync_Ok_ReturnsTrue()
    {
        Assert.True(await Create(HttpStatusCode.OK, "").CheckHealthAsync());
    }

    /// <summary>
    /// Verifies that a 503 Service Unavailable returns false.
    /// </summary>
    [Fact]
    public async Task CheckHealthAsync_ServiceUnavailable_ReturnsFalse()
    {
        Assert.False(await Create(HttpStatusCode.ServiceUnavailable, "").CheckHealthAsync());
    }

    /// <summary>
    /// Verifies that a network failure during the health check returns false.
    /// </summary>
    [Fact]
    public async Task CheckHealthAsync_NetworkError_ReturnsFalse()
    {
        Assert.False(await CreateThrowing().CheckHealthAsync());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TriggerScraperAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a 200 OK response returns (true, responseBody) where the message
    /// matches the server's plain-text response body.
    /// </summary>
    [Fact]
    public async Task TriggerScraperAsync_Ok_ReturnsSuccessWithMessage()
    {
        var svc = Create(HttpStatusCode.OK, "Calendrier mis à jour");

        var (success, message) = await svc.TriggerScraperAsync();

        Assert.True(success);
        Assert.Equal("Calendrier mis à jour", message);
    }

    /// <summary>
    /// Verifies that a 500 Internal Server Error returns (false, errorDescription).
    /// </summary>
    [Fact]
    public async Task TriggerScraperAsync_ServerError_ReturnsFailure()
    {
        var (success, _) = await Create(HttpStatusCode.InternalServerError, "").TriggerScraperAsync();

        Assert.False(success);
    }

    /// <summary>
    /// Verifies that a network failure returns (false, exceptionMessage) without throwing.
    /// </summary>
    [Fact]
    public async Task TriggerScraperAsync_NetworkError_ReturnsFailure()
    {
        var (success, message) = await CreateThrowing().TriggerScraperAsync();

        Assert.False(success);
        Assert.NotNull(message);
    }
}
