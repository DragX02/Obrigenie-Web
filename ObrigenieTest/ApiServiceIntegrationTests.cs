using Blazored.LocalStorage;
using Moq;
using Obrigenie.Models;
using Obrigenie.Services;
using System.Net;
using System.Text.Json;

namespace ObrigenieTest;

public class ApiServiceIntegrationTests
{
    private class FakeHandler(HttpStatusCode status, string body = "") : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            });
    }

    private class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Simulated network failure");
    }

    private static AuthService CreateAuth(string? token = null)
    {
        var mock = new Mock<ILocalStorageService>();
        mock.Setup(s => s.GetItemAsStringAsync("jwt_token", default)).ReturnsAsync(token);
        return new AuthService(mock.Object);
    }

    private static ApiService Create(HttpStatusCode status, string body, string? token = null)
    {
        var http = new HttpClient(new FakeHandler(status, body))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        return new ApiService(http, CreateAuth(token));
    }

    private static ApiService CreateThrowing(string? token = null)
    {
        var http = new HttpClient(new ThrowingHandler())
        {
            BaseAddress = new Uri("http://localhost/")
        };
        return new ApiService(http, CreateAuth(token));
    }

    [Fact]
    public async Task LoginAsync_Ok_ReturnsAuthResponse()
    {
        var body = JsonSerializer.Serialize(new { token = "jwt123", email = "prof@school.be", nom = "Doe", prenom = "John" });
        var svc  = Create(HttpStatusCode.OK, body);

        var (auth, error) = await svc.LoginAsync(new LoginDto { Email = "prof@school.be", Password = "pass" });

        Assert.NotNull(auth);
        Assert.Equal("jwt123", auth.Token);
        Assert.Equal("prof@school.be", auth.Email);
        Assert.Null(error);
    }

    [Fact]
    public async Task LoginAsync_Unauthorized_ReturnsServerMessage()
    {
        var svc = Create(HttpStatusCode.Unauthorized, "Veuillez confirmer votre email avant de vous connecter.");

        var (auth, error) = await svc.LoginAsync(new LoginDto { Email = "x@x.com", Password = "wrong" });

        Assert.Null(auth);
        Assert.Equal("Veuillez confirmer votre email avant de vous connecter.", error);
    }

    [Fact]
    public async Task LoginAsync_TooManyRequests_ReturnsRateLimitMessage()
    {
        var svc = Create(HttpStatusCode.TooManyRequests, "");

        var (auth, error) = await svc.LoginAsync(new LoginDto { Email = "x@x.com", Password = "pass" });

        Assert.Null(auth);
        Assert.Contains("Trop de tentatives", error);
    }

    [Fact]
    public async Task RegisterAsync_Ok_ReturnsServerMessage()
    {
        var body = JsonSerializer.Serialize(new { message = "Compte créé ! Vérifiez votre email pour confirmer votre inscription." });
        var svc  = Create(HttpStatusCode.OK, body);

        var dto = new RegisterDto { Email = "new@school.be", Password = "Pass1!", ConfirmPassword = "Pass1!", Nom = "New", Prenom = "User" };
        var (success, message) = await svc.RegisterAsync(dto);

        Assert.True(success);
        Assert.Equal("Compte créé ! Vérifiez votre email pour confirmer votre inscription.", message);
    }

    [Fact]
    public async Task RegisterAsync_Rejected_ReturnsServerMessage()
    {
        var svc = Create(HttpStatusCode.BadRequest, "Un compte avec cet email existe déjà.");

        var dto = new RegisterDto { Email = "dup@school.be", Password = "Pass1!", ConfirmPassword = "Pass1!", Nom = "X", Prenom = "Y" };
        var (success, message) = await svc.RegisterAsync(dto);

        Assert.False(success);
        Assert.Equal("Un compte avec cet email existe déjà.", message);
    }

    [Fact]
    public async Task RegisterAsync_TooManyRequests_ReturnsRateLimitMessage()
    {
        var svc = Create(HttpStatusCode.TooManyRequests, "");

        var dto = new RegisterDto { Email = "new@school.be", Password = "Pass1!", ConfirmPassword = "Pass1!", Nom = "X", Prenom = "Y" };
        var (success, message) = await svc.RegisterAsync(dto);

        Assert.False(success);
        Assert.Contains("Trop de tentatives", message);
    }

    [Fact]
    public async Task ExchangeOAuthTokenAsync_Ok_ReturnsAuthResponse()
    {
        var body = JsonSerializer.Serialize(new { token = "oauthjwt", email = "oauth@school.be" });
        var svc  = Create(HttpStatusCode.OK, body);

        var result = await svc.ExchangeOAuthTokenAsync();

        Assert.NotNull(result);
        Assert.Equal("oauthjwt", result.Token);
    }

    [Fact]
    public async Task ExchangeOAuthTokenAsync_NetworkError_ReturnsNull()
    {
        var result = await CreateThrowing().ExchangeOAuthTokenAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task ExchangeOAuthTokenAsync_Unauthorized_ReturnsNull()
    {
        var svc = Create(HttpStatusCode.Unauthorized, "");

        Assert.Null(await svc.ExchangeOAuthTokenAsync());
    }

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

    [Fact]
    public async Task GetCoursesForDateAsync_ServerError_ReturnsEmptyList()
    {
        var svc = Create(HttpStatusCode.InternalServerError, "");

        Assert.Empty(await svc.GetCoursesForDateAsync(DateTime.Today));
    }

    [Fact]
    public async Task GetCoursesForDateAsync_NetworkError_ReturnsEmptyList()
    {
        Assert.Empty(await CreateThrowing().GetCoursesForDateAsync(DateTime.Today));
    }

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

    [Fact]
    public async Task GetNotesForDateAsync_ServerError_ReturnsEmptyList()
    {
        Assert.Empty(await Create(HttpStatusCode.InternalServerError, "").GetNotesForDateAsync(DateTime.Today));
    }

    [Fact]
    public async Task GetNotesForDateAsync_NetworkError_ReturnsEmptyList()
    {
        Assert.Empty(await CreateThrowing().GetNotesForDateAsync(DateTime.Today));
    }

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

    [Fact]
    public async Task GetNotesForRangeAsync_NetworkError_ReturnsEmptyList()
    {
        Assert.Empty(await CreateThrowing().GetNotesForRangeAsync(DateTime.Today, DateTime.Today.AddDays(7)));
    }

    [Fact]
    public async Task SaveNoteAsync_Ok_ReturnsSuccess()
    {
        var svc = Create(HttpStatusCode.OK, "");

        var (success, error) = await svc.SaveNoteAsync(new Note { Id = 0, Content = "Test note" });

        Assert.True(success);
        Assert.Null(error);
    }

    [Fact]
    public async Task SaveNoteAsync_BadRequest_ReturnsError()
    {
        var svc = Create(HttpStatusCode.BadRequest, "Content required");

        var (success, error) = await svc.SaveNoteAsync(new Note { Content = "" });

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Contains("400", error);
    }

    [Fact]
    public async Task SaveNoteAsync_NetworkError_ReturnsError()
    {
        var (success, error) = await CreateThrowing().SaveNoteAsync(new Note { Content = "x" });

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task ValidateAccessCodeAsync_Ok_ReturnsTrue()
    {
        Assert.True(await Create(HttpStatusCode.OK, "").ValidateAccessCodeAsync("VALID-CODE"));
    }

    [Fact]
    public async Task ValidateAccessCodeAsync_NotFound_ReturnsFalse()
    {
        Assert.False(await Create(HttpStatusCode.NotFound, "").ValidateAccessCodeAsync("BAD-CODE"));
    }

    [Fact]
    public async Task ValidateAccessCodeAsync_NetworkError_ReturnsFalse()
    {
        Assert.False(await CreateThrowing().ValidateAccessCodeAsync("CODE"));
    }

    [Fact]
    public async Task CheckLicenseAsync_ValidLicense_ReturnsTrue()
    {
        var body = JsonSerializer.Serialize(new { valid = true });

        Assert.True(await Create(HttpStatusCode.OK, body).CheckLicenseAsync("ACTIVE-CODE"));
    }

    [Fact]
    public async Task CheckLicenseAsync_RevokedLicense_ReturnsFalse()
    {
        var body = JsonSerializer.Serialize(new { valid = false });

        Assert.False(await Create(HttpStatusCode.OK, body).CheckLicenseAsync("REVOKED-CODE"));
    }

    [Fact]
    public async Task CheckLicenseAsync_ServerError_ReturnsFalse()
    {
        Assert.False(await Create(HttpStatusCode.InternalServerError, "").CheckLicenseAsync("CODE"));
    }

    [Fact]
    public async Task CheckLicenseAsync_NetworkError_ReturnsFalse()
    {
        Assert.False(await CreateThrowing().CheckLicenseAsync("CODE"));
    }

    [Fact]
    public async Task RevokeLicenseAsync_Ok_ReturnsTrue()
    {
        Assert.True(await Create(HttpStatusCode.OK, "").RevokeLicenseAsync(42));
    }

    [Fact]
    public async Task RevokeLicenseAsync_NotFound_ReturnsFalse()
    {
        Assert.False(await Create(HttpStatusCode.NotFound, "").RevokeLicenseAsync(99));
    }

    [Fact]
    public async Task RevokeLicenseAsync_NetworkError_ReturnsFalse()
    {
        Assert.False(await CreateThrowing().RevokeLicenseAsync(1));
    }

    [Fact]
    public async Task ReactivateLicenseAsync_Ok_ReturnsTrue()
    {
        Assert.True(await Create(HttpStatusCode.OK, "").ReactivateLicenseAsync(42));
    }

    [Fact]
    public async Task ReactivateLicenseAsync_ServerError_ReturnsFalse()
    {
        Assert.False(await Create(HttpStatusCode.InternalServerError, "").ReactivateLicenseAsync(42));
    }

    [Fact]
    public async Task DeleteLicenseAsync_Ok_ReturnsTrue()
    {
        Assert.True(await Create(HttpStatusCode.OK, "").DeleteLicenseAsync(10));
    }

    [Fact]
    public async Task DeleteLicenseAsync_NotFound_ReturnsFalse()
    {
        Assert.False(await Create(HttpStatusCode.NotFound, "").DeleteLicenseAsync(99));
    }

    [Fact]
    public async Task CheckHealthAsync_Ok_ReturnsTrue()
    {
        Assert.True(await Create(HttpStatusCode.OK, "").CheckHealthAsync());
    }

    [Fact]
    public async Task CheckHealthAsync_ServiceUnavailable_ReturnsFalse()
    {
        Assert.False(await Create(HttpStatusCode.ServiceUnavailable, "").CheckHealthAsync());
    }

    [Fact]
    public async Task CheckHealthAsync_NetworkError_ReturnsFalse()
    {
        Assert.False(await CreateThrowing().CheckHealthAsync());
    }

    [Fact]
    public async Task TriggerScraperAsync_Ok_ReturnsSuccessWithMessage()
    {
        var svc = Create(HttpStatusCode.OK, "Calendrier mis à jour");

        var (success, message) = await svc.TriggerScraperAsync();

        Assert.True(success);
        Assert.Equal("Calendrier mis à jour", message);
    }

    [Fact]
    public async Task TriggerScraperAsync_ServerError_ReturnsFailure()
    {
        var (success, _) = await Create(HttpStatusCode.InternalServerError, "").TriggerScraperAsync();

        Assert.False(success);
    }

    [Fact]
    public async Task TriggerScraperAsync_NetworkError_ReturnsFailure()
    {
        var (success, message) = await CreateThrowing().TriggerScraperAsync();

        Assert.False(success);
        Assert.NotNull(message);
    }
}
