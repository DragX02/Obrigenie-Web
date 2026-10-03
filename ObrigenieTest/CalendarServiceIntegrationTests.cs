using Blazored.LocalStorage;
using Moq;
using Obrigenie.Services;
using System.Net;
using System.Text.Json;

namespace ObrigenieTest;

public class CalendarServiceIntegrationTests
{
    private class FakeHandler(HttpStatusCode status, string body = "") : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body)
            });
    }

    private class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Simulated network failure");
    }

    private static CalendarService CreateService(
        HttpMessageHandler handler,
        Mock<ILocalStorageService> localStorage)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return new CalendarService(http, localStorage.Object);
    }

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

    [Fact]
    public async Task GetCalendarData_ApiRentreeOnAnotherDay_DoesNotAddASecondMarker()
    {
        int startYear = CurrentSchoolYearStart();

        var payload = JsonSerializer.Serialize(new[]
        {
            new
            {
                nomEvenement  = "Rentrée scolaire",
                dateDebut     = $"{startYear}-08-24",
                dateFin       = $"{startYear}-08-24",
                typeEvenement = "RENTREE",
                anneeScolaire = $"{startYear}-{startYear + 1}"
            }
        });

        var svc = CreateService(new FakeHandler(HttpStatusCode.OK, payload), new Mock<ILocalStorageService>());
        var calendar = await svc.GetCalendarData();

        var rentrees = calendar.Holidays
            .Where(h => CalendarService.EstRentree(h.Name)
                     && h.StartDate.Year == startYear && h.StartDate.Month == 8)
            .ToList();

        var rentree = Assert.Single(rentrees);
        Assert.Equal(new DateTime(startYear, 8, 24), rentree.StartDate);
    }

    private static int CurrentSchoolYearStart()
    {
        var today = DateTime.Today;
        return today.Month >= 8 ? today.Year : today.Year - 1;
    }

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

    [Fact]
    public async Task GetCalendarData_ApiReturnsRentree_SetsSchoolYearStart()
    {
        int startYear   = CurrentSchoolYearStart();
        var mockStorage = new Mock<ILocalStorageService>();
        var svc         = CreateService(new FakeHandler(HttpStatusCode.OK, MakeApiPayload(startYear)), mockStorage);

        var calendar = await svc.GetCalendarData();

        Assert.Equal(new DateTime(startYear, 9, 1), calendar.SchoolYearStart);
    }

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
        Assert.Contains(calendar.Holidays, h =>
            h.Name.Contains("Noel") || h.Name.Contains("Noël") || h.Name.Contains("hiver"));
    }

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

        mockStorage.Verify(
            s => s.SetItemAsStringAsync(It.IsAny<string>(), It.IsAny<string>(), default),
            Times.Never);
    }

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

    [Fact]
    public async Task GetCalendarData_OfflineCalendar_SchoolYearStartIsAugust26()
    {
        var mockStorage = new Mock<ILocalStorageService>();
        mockStorage.Setup(s => s.GetItemAsStringAsync("CachedCalendarData", default))
                   .ReturnsAsync((string?)null);

        var svc = CreateService(new ThrowingHandler(), mockStorage);

        var calendar = await svc.GetCalendarData();

        Assert.NotEqual(DateTime.MinValue, calendar.SchoolYearStart);
        Assert.Equal(8,  calendar.SchoolYearStart.Month);
        Assert.Equal(26, calendar.SchoolYearStart.Day);
    }

    [Fact]
    public async Task GetCalendarData_OfflineCalendar_ContainsRentreeMarkersForBothYears()
    {
        var mockStorage = new Mock<ILocalStorageService>();
        mockStorage.Setup(s => s.GetItemAsStringAsync("CachedCalendarData", default))
                   .ReturnsAsync((string?)null);

        var svc = CreateService(new ThrowingHandler(), mockStorage);

        var calendar = await svc.GetCalendarData();
        var rentrees = calendar.Holidays.Where(h => h.Name.Contains("Rentree")).ToList();

        Assert.True(rentrees.Count >= 2,
            $"Expected at least 2 Rentrée markers, but found {rentrees.Count}.");
    }

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
