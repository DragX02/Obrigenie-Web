using Blazored.LocalStorage;
using Moq;
using Obrigenie.Services;
using System.Net;
using System.Text.Json;

namespace ObrigenieTest;

/// <summary>
/// Integration tests for <see cref="CalendarService"/>.
///
/// CalendarService uses a three-level data strategy:
///   1. Fetch live data from the API  → cache result in localStorage.
///   2. If the API fails, read from the localStorage cache.
///   3. If both fail, return a hard-coded offline fallback calendar.
///
/// Each strategy is exercised by combining a fake <see cref="HttpMessageHandler"/>
/// with a mocked <see cref="ILocalStorageService"/>.  No real network calls or
/// browser storage access are required.
/// </summary>
public class CalendarServiceIntegrationTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // HTTP mock infrastructure
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns a fixed HTTP response without any real network activity.</summary>
    private class FakeHandler(HttpStatusCode status, string body = "") : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body)
            });
    }

    /// <summary>Always throws <see cref="HttpRequestException"/> to simulate a downed server.</summary>
    private class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Simulated network failure");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Factory helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static CalendarService CreateService(
        HttpMessageHandler handler,
        Mock<ILocalStorageService> localStorage)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return new CalendarService(http, localStorage.Object);
    }

    /// <summary>
    /// Builds a minimal API payload for the current school year containing a
    /// Rentrée event and one vacation period.
    /// </summary>
    private static string MakeApiPayload(int startYear)
    {
        var items = new[]
        {
            new
            {
                nomEvenement  = "Rentree scolaire",
                dateDebut     = $"{startYear}-09-01",
                dateFin       = $"{startYear}-09-01",
                typeEvenement = "RENTREE",
                anneeScolaire = $"{startYear}-{startYear + 1}"
            },
            new
            {
                nomEvenement  = "Conge d'automne (Toussaint)",
                dateDebut     = $"{startYear}-10-28",
                dateFin       = $"{startYear}-11-03",
                typeEvenement = "CONGE",
                anneeScolaire = $"{startYear}-{startYear + 1}"
            }
        };
        return JsonSerializer.Serialize(items);
    }

    /// <summary>Returns the start year of the current Belgian school year.</summary>
    private static int CurrentSchoolYearStart()
    {
        var today = DateTime.Today;
        return today.Month >= 8 ? today.Year : today.Year - 1;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Strategy 1 — API returns live data
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that when the API returns a valid JSON array, GetCalendarData returns
    /// a non-null calendar with at least one holiday (the events sent by the API
    /// plus any synthetic Rentrée markers injected by the service).
    /// </summary>
    [Fact]
    public async Task GetCalendarData_ApiReturnsData_ReturnsPopulatedCalendar()
    {
        int startYear   = CurrentSchoolYearStart();
        var mockStorage = new Mock<ILocalStorageService>();
        var svc         = CreateService(new FakeHandler(HttpStatusCode.OK, MakeApiPayload(startYear)), mockStorage);

        var calendar = await svc.GetCalendarData();

        Assert.NotNull(calendar);
        Assert.NotEmpty(calendar.Holidays);
    }

    /// <summary>
    /// Verifies that when the API returns data successfully, the raw JSON response
    /// is written to localStorage under the key "CachedCalendarData" exactly once.
    /// This ensures the cache-write step is not skipped.
    /// </summary>
    [Fact]
    public async Task GetCalendarData_ApiReturnsData_CachesResponseToLocalStorage()
    {
        int startYear   = CurrentSchoolYearStart();
        var mockStorage = new Mock<ILocalStorageService>();
        var svc         = CreateService(new FakeHandler(HttpStatusCode.OK, MakeApiPayload(startYear)), mockStorage);

        await svc.GetCalendarData();

        mockStorage.Verify(
            s => s.SetItemAsStringAsync("CachedCalendarData", It.IsAny<string>(), default),
            Times.Once);
    }

    /// <summary>
    /// Verifies that the SchoolYearStart date extracted from a "Rentree scolaire"
    /// event in the API response matches the date sent by the fake API (Sep 1 of startYear).
    /// </summary>
    [Fact]
    public async Task GetCalendarData_ApiReturnsRentree_SetsSchoolYearStart()
    {
        int startYear   = CurrentSchoolYearStart();
        var mockStorage = new Mock<ILocalStorageService>();
        var svc         = CreateService(new FakeHandler(HttpStatusCode.OK, MakeApiPayload(startYear)), mockStorage);

        var calendar = await svc.GetCalendarData();

        // The Rentrée sent by the API is Sep 1; SchoolYearStart should be that date.
        Assert.Equal(new DateTime(startYear, 9, 1), calendar.SchoolYearStart);
    }

    /// <summary>
    /// Verifies that when the API returns an empty JSON array ([]), the service
    /// falls through to the offline fallback and still returns a non-empty calendar.
    /// </summary>
    [Fact]
    public async Task GetCalendarData_ApiReturnsEmptyArray_FallsBackToOffline()
    {
        var mockStorage = new Mock<ILocalStorageService>();
        mockStorage.Setup(s => s.GetItemAsStringAsync("CachedCalendarData", default))
                   .ReturnsAsync((string?)null);

        var svc = CreateService(new FakeHandler(HttpStatusCode.OK, "[]"), mockStorage);

        var calendar = await svc.GetCalendarData();

        Assert.NotNull(calendar);
        Assert.NotEmpty(calendar.Holidays);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Strategy 2 — API fails, cache exists
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that when the HTTP client throws (network failure) but the
    /// localStorage cache contains a valid JSON payload, the service returns
    /// a calendar populated with the cached holiday data.
    /// </summary>
    [Fact]
    public async Task GetCalendarData_ApiFailsCacheExists_ReturnsCachedCalendar()
    {
        int startYear = CurrentSchoolYearStart();

        var cachedItems = new[]
        {
            new
            {
                nomEvenement  = "Vacances d'hiver (Noel)",
                dateDebut     = $"{startYear}-12-23",
                dateFin       = $"{startYear + 1}-01-05",
                typeEvenement = "CONGE",
                anneeScolaire = $"{startYear}-{startYear + 1}"
            }
        };

        var mockStorage = new Mock<ILocalStorageService>();
        mockStorage.Setup(s => s.GetItemAsStringAsync("CachedCalendarData", default))
                   .ReturnsAsync(JsonSerializer.Serialize(cachedItems));

        var svc = CreateService(new ThrowingHandler(), mockStorage);

        var calendar = await svc.GetCalendarData();

        Assert.NotNull(calendar);
        // The cached Christmas holiday must be present in the returned calendar.
        Assert.True(calendar.Holidays.Any(h =>
            h.Name.Contains("Noel") || h.Name.Contains("Noël") || h.Name.Contains("hiver")));
    }

    /// <summary>
    /// Verifies that the localStorage cache is read (not written) when the API call
    /// fails — the cache-write must not happen on the fallback path.
    /// </summary>
    [Fact]
    public async Task GetCalendarData_ApiFailsCacheExists_DoesNotOverwriteCache()
    {
        int startYear = CurrentSchoolYearStart();

        var cachedItems = new[]
        {
            new
            {
                nomEvenement  = "Conge de detente (Carnaval)",
                dateDebut     = $"{startYear + 1}-02-16",
                dateFin       = $"{startYear + 1}-03-01",
                typeEvenement = "CONGE",
                anneeScolaire = $"{startYear}-{startYear + 1}"
            }
        };

        var mockStorage = new Mock<ILocalStorageService>();
        mockStorage.Setup(s => s.GetItemAsStringAsync("CachedCalendarData", default))
                   .ReturnsAsync(JsonSerializer.Serialize(cachedItems));

        var svc = CreateService(new ThrowingHandler(), mockStorage);

        await svc.GetCalendarData();

        // SetItemAsStringAsync must NOT have been called — the cache should be preserved.
        mockStorage.Verify(
            s => s.SetItemAsStringAsync(It.IsAny<string>(), It.IsAny<string>(), default),
            Times.Never);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Strategy 3 — API fails and cache is empty → offline fallback
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that when both the API and the localStorage cache are unavailable,
    /// GetCalendarData returns a non-empty offline calendar instead of null or empty.
    /// </summary>
    [Fact]
    public async Task GetCalendarData_ApiFailsNoCacheExists_ReturnsOfflineCalendar()
    {
        var mockStorage = new Mock<ILocalStorageService>();
        mockStorage.Setup(s => s.GetItemAsStringAsync("CachedCalendarData", default))
                   .ReturnsAsync((string?)null);

        var svc = CreateService(new ThrowingHandler(), mockStorage);

        var calendar = await svc.GetCalendarData();

        Assert.NotNull(calendar);
        Assert.NotEmpty(calendar.Holidays);
    }

    /// <summary>
    /// Verifies that the offline fallback calendar contains all five standard
    /// Belgian school vacation periods by checking for each period's key term.
    /// </summary>
    [Fact]
    public async Task GetCalendarData_OfflineCalendar_ContainsFiveStandardVacations()
    {
        var mockStorage = new Mock<ILocalStorageService>();
        mockStorage.Setup(s => s.GetItemAsStringAsync("CachedCalendarData", default))
                   .ReturnsAsync((string?)null);

        var svc = CreateService(new ThrowingHandler(), mockStorage);

        var calendar = await svc.GetCalendarData();
        var names    = calendar.Holidays.Select(h => h.Name).ToList();

        Assert.Contains(names, n => n.Contains("Toussaint") || n.Contains("automne"));
        Assert.Contains(names, n => n.Contains("Noel")      || n.Contains("Noël")    || n.Contains("hiver"));
        Assert.Contains(names, n => n.Contains("Carnaval")  || n.Contains("detente"));
        Assert.Contains(names, n => n.Contains("Paques")    || n.Contains("Pâques")  || n.Contains("printemps"));
        Assert.Contains(names, n => n.Contains("ete")       || n.Contains("été")     || n.Contains("Ete"));
    }

    /// <summary>
    /// Verifies that the offline fallback calendar always sets SchoolYearStart to
    /// August 26 of the appropriate year (the earliest possible Belgian Rentrée date).
    /// </summary>
    [Fact]
    public async Task GetCalendarData_OfflineCalendar_SchoolYearStartIsAugust26()
    {
        var mockStorage = new Mock<ILocalStorageService>();
        mockStorage.Setup(s => s.GetItemAsStringAsync("CachedCalendarData", default))
                   .ReturnsAsync((string?)null);

        var svc = CreateService(new ThrowingHandler(), mockStorage);

        var calendar = await svc.GetCalendarData();

        Assert.NotEqual(DateTime.MinValue, calendar.SchoolYearStart);
        Assert.Equal(8,  calendar.SchoolYearStart.Month);  // August
        Assert.Equal(26, calendar.SchoolYearStart.Day);    // 26th
    }

    /// <summary>
    /// Verifies that the offline calendar includes Rentrée markers for both the
    /// current and the following school year, ensuring navigation to the next year works.
    /// </summary>
    [Fact]
    public async Task GetCalendarData_OfflineCalendar_ContainsRentreeMarkersForBothYears()
    {
        var mockStorage = new Mock<ILocalStorageService>();
        mockStorage.Setup(s => s.GetItemAsStringAsync("CachedCalendarData", default))
                   .ReturnsAsync((string?)null);

        var svc = CreateService(new ThrowingHandler(), mockStorage);

        var calendar = await svc.GetCalendarData();
        var rentrees = calendar.Holidays.Where(h => h.Name.Contains("Rentree")).ToList();

        // There should be at least two Rentrée markers (current year + next year).
        Assert.True(rentrees.Count >= 2,
            $"Expected at least 2 Rentrée markers, but found {rentrees.Count}.");
    }

    /// <summary>
    /// Verifies that even when the localStorage read throws an exception (corrupt storage),
    /// the service gracefully falls back to the offline calendar.
    /// </summary>
    [Fact]
    public async Task GetCalendarData_CacheReadThrows_FallsBackToOfflineCalendar()
    {
        var mockStorage = new Mock<ILocalStorageService>();
        mockStorage.Setup(s => s.GetItemAsStringAsync("CachedCalendarData", default))
                   .ThrowsAsync(new InvalidOperationException("Storage corrupted"));

        var svc = CreateService(new ThrowingHandler(), mockStorage);

        var calendar = await svc.GetCalendarData();

        Assert.NotNull(calendar);
        Assert.NotEmpty(calendar.Holidays);
    }
}
