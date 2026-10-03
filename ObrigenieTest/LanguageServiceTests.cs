using Blazored.LocalStorage;
using Moq;
using Obrigenie.Services;

namespace ObrigenieTest;

public class LanguageServiceTests
{
    private static (LanguageService svc, Mock<ILocalStorageService> mock)
        CreateService(string? storedLang = null)
    {
        var mock = new Mock<ILocalStorageService>();
        mock.Setup(s => s.GetItemAsStringAsync("lang", default))
            .ReturnsAsync(storedLang);
        return (new LanguageService(mock.Object), mock);
    }

    [Fact]
    public void Current_BeforeInit_DefaultsFR()
    {
        var (svc, _) = CreateService();

        Assert.Equal("FR", svc.Current);
    }

    [Fact]
    public void T_WithoutInit_UsesDefaultFR()
    {
        var (svc, _) = CreateService();

        Assert.Equal("Agenda", svc.T("nav.calendar"));
    }

    [Theory]
    [InlineData("FR", "nav.calendar",   "Agenda")]
    [InlineData("EN", "nav.calendar",   "Calendar")]
    [InlineData("NL", "nav.calendar",   "Kalender")]
    [InlineData("FR", "nav.referents",  "Référentiels")]
    [InlineData("EN", "nav.referents",  "References")]
    [InlineData("NL", "nav.referents",  "Referenties")]
    [InlineData("FR", "action.logout",  "Déconnexion")]
    [InlineData("EN", "action.logout",  "Logout")]
    [InlineData("NL", "action.logout",  "Uitloggen")]
    [InlineData("FR", "action.account", "Mon compte")]
    [InlineData("EN", "action.account", "My account")]
    [InlineData("NL", "action.account", "Mijn account")]
    [InlineData("FR", "account.langFR", "Français")]
    [InlineData("EN", "account.langFR", "French")]
    [InlineData("NL", "account.langFR", "Frans")]
    [InlineData("FR", "account.langEN", "Anglais")]
    [InlineData("EN", "account.langEN", "English")]
    [InlineData("NL", "account.langEN", "Engels")]
    [InlineData("FR", "account.langNL", "Néerlandais")]
    [InlineData("EN", "account.langNL", "Dutch")]
    [InlineData("NL", "account.langNL", "Nederlands")]
    [InlineData("FR", "server.online",  "Serveur connecté")]
    [InlineData("EN", "server.online",  "Server connected")]
    [InlineData("NL", "server.online",  "Server verbonden")]
    [InlineData("FR", "theme.light",    "Mode clair")]
    [InlineData("EN", "theme.light",    "Light mode")]
    [InlineData("NL", "theme.light",    "Lichte modus")]
    public async Task T_AfterInit_ReturnsExpectedTranslation(string lang, string key, string expected)
    {
        var (svc, _) = CreateService(lang);
        await svc.InitAsync();

        Assert.Equal(expected, svc.T(key));
    }

    [Fact]
    public void T_UnknownKey_ReturnsKey()
    {
        var (svc, _) = CreateService();

        Assert.Equal("unknown.key", svc.T("unknown.key"));
    }

    [Theory]
    [InlineData("FR")]
    [InlineData("EN")]
    [InlineData("NL")]
    public async Task T_KnownKey_NeverReturnsNull(string lang)
    {
        var (svc, _) = CreateService(lang);
        await svc.InitAsync();

        Assert.NotNull(svc.T("nav.calendar"));
        Assert.NotNull(svc.T("account.title"));
        Assert.NotNull(svc.T("action.logout"));
    }

    [Theory]
    [InlineData("FR", "FR")]
    [InlineData("EN", "EN")]
    [InlineData("NL", "NL")]
    public async Task InitAsync_LoadsStoredLanguage(string stored, string expected)
    {
        var (svc, _) = CreateService(stored);

        await svc.InitAsync();

        Assert.Equal(expected, svc.Current);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ES")]
    [InlineData("IT")]
    [InlineData("fr")]
    [InlineData("en")]
    public async Task InitAsync_DefaultsToFR_WhenValueIsInvalid(string? stored)
    {
        var (svc, _) = CreateService(stored);

        await svc.InitAsync();

        Assert.Equal("FR", svc.Current);
    }

    [Fact]
    public async Task InitAsync_IsIdempotent_LocalStorageReadOnlyOnce()
    {
        var (svc, mock) = CreateService("EN");

        await svc.InitAsync();
        await svc.InitAsync();

        mock.Verify(s => s.GetItemAsStringAsync("lang", default), Times.Once);
    }

    [Fact]
    public async Task InitAsync_SecondCall_DoesNotResetLanguage()
    {
        var (svc, _) = CreateService("EN");

        await svc.InitAsync();
        await svc.InitAsync();

        Assert.Equal("EN", svc.Current);
    }

    [Theory]
    [InlineData("EN")]
    [InlineData("NL")]
    [InlineData("FR")]
    public async Task SetAsync_ValidLanguage_ChangesCurrentLanguage(string lang)
    {
        var (svc, _) = CreateService();

        await svc.SetAsync(lang);

        Assert.Equal(lang, svc.Current);
    }

    [Theory]
    [InlineData("EN")]
    [InlineData("NL")]
    [InlineData("FR")]
    public async Task SetAsync_ValidLanguage_PersistsToLocalStorage(string lang)
    {
        var (svc, mock) = CreateService();

        await svc.SetAsync(lang);

        mock.Verify(s => s.SetItemAsStringAsync("lang", lang, default), Times.Once);
    }

    [Theory]
    [InlineData("ES")]
    [InlineData("IT")]
    [InlineData("")]
    [InlineData("fr")]
    [InlineData("EN-US")]
    public async Task SetAsync_InvalidLanguage_DoesNotChangeState(string invalid)
    {
        var (svc, mock) = CreateService();

        await svc.SetAsync(invalid);

        Assert.Equal("FR", svc.Current);
        mock.Verify(
            s => s.SetItemAsStringAsync(It.IsAny<string>(), It.IsAny<string>(), default),
            Times.Never);
    }

    [Fact]
    public async Task SetAsync_ValidLanguage_FiresOnChangeEvent()
    {
        var (svc, _) = CreateService();
        var fired = false;
        svc.OnChange += () => fired = true;

        await svc.SetAsync("EN");

        Assert.True(fired);
    }

    [Fact]
    public async Task SetAsync_InvalidLanguage_DoesNotFireOnChange()
    {
        var (svc, _) = CreateService();
        var fired = false;
        svc.OnChange += () => fired = true;

        await svc.SetAsync("ZZ");

        Assert.False(fired);
    }

    [Fact]
    public async Task SetAsync_CalledTwice_FiresOnChangeTwice()
    {
        var (svc, _) = CreateService();
        var count = 0;
        svc.OnChange += () => count++;

        await svc.SetAsync("EN");
        await svc.SetAsync("NL");

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task SetAsync_MultipleSubscribers_AllNotified()
    {
        var (svc, _) = CreateService();
        var counter1 = 0;
        var counter2 = 0;
        svc.OnChange += () => counter1++;
        svc.OnChange += () => counter2++;

        await svc.SetAsync("EN");

        Assert.Equal(1, counter1);
        Assert.Equal(1, counter2);
    }

    [Fact]
    public async Task SetAsync_ThenT_ReturnsUpdatedTranslations()
    {
        var (svc, _) = CreateService();

        await svc.SetAsync("EN");

        Assert.Equal("Calendar", svc.T("nav.calendar"));
        Assert.Equal("Logout",   svc.T("action.logout"));
        Assert.Equal("English",  svc.T("account.langEN"));
    }
}
