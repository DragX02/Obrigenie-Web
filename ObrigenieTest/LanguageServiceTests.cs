using Blazored.LocalStorage;
using Moq;
using Obrigenie.Services;

namespace ObrigenieTest;

/// <summary>
/// Tests unitaires pour <see cref="LanguageService"/>.
///
/// Couvre :
///   - Current      : langue par défaut = "FR".
///   - T()          : retour de la bonne traduction selon la langue active.
///   - InitAsync    : chargement depuis localStorage, valeur par défaut, idempotence.
///   - SetAsync     : changement de langue, persistance, validation, événement OnChange.
///
/// <see cref="ILocalStorageService"/> est mocké avec Moq ; aucun accès localStorage réel.
/// </summary>
public class LanguageServiceTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Crée un <see cref="LanguageService"/> dont le localStorage renvoie
    /// <paramref name="storedLang"/> pour la clé "lang".
    /// </summary>
    private static (LanguageService svc, Mock<ILocalStorageService> mock)
        CreateService(string? storedLang = null)
    {
        var mock = new Mock<ILocalStorageService>();
        mock.Setup(s => s.GetItemAsStringAsync("lang", default))
            .ReturnsAsync(storedLang);
        return (new LanguageService(mock.Object), mock);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Current — valeur par défaut
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Avant tout appel à InitAsync, Current doit valoir "FR".
    /// </summary>
    [Fact]
    public void Current_BeforeInit_DefaultsFR()
    {
        var (svc, _) = CreateService();

        Assert.Equal("FR", svc.Current);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // T() — traductions
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sans InitAsync, T() utilise "FR" par défaut et renvoie la traduction française.
    /// </summary>
    [Fact]
    public void T_WithoutInit_UsesDefaultFR()
    {
        var (svc, _) = CreateService();

        Assert.Equal("Agenda", svc.T("nav.calendar"));
    }

    /// <summary>
    /// T() retourne la traduction correcte pour chaque combinaison langue / clé.
    /// </summary>
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

    /// <summary>
    /// Quand la clé n'existe pas dans le dictionnaire, T() retourne la clé elle-même.
    /// </summary>
    [Fact]
    public void T_UnknownKey_ReturnsKey()
    {
        var (svc, _) = CreateService();

        Assert.Equal("unknown.key", svc.T("unknown.key"));
    }

    /// <summary>
    /// T() ne renvoie jamais null pour une clé connue.
    /// </summary>
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

    // ─────────────────────────────────────────────────────────────────────────
    // InitAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// InitAsync charge la langue valide stockée dans localStorage.
    /// </summary>
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

    /// <summary>
    /// InitAsync utilise "FR" par défaut quand la valeur stockée est absente ou invalide.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ES")]
    [InlineData("IT")]
    [InlineData("fr")]   // sensible à la casse
    [InlineData("en")]
    public async Task InitAsync_DefaultsToFR_WhenValueIsInvalid(string? stored)
    {
        var (svc, _) = CreateService(stored);

        await svc.InitAsync();

        Assert.Equal("FR", svc.Current);
    }

    /// <summary>
    /// Un second appel à InitAsync ne relit pas localStorage (idempotent).
    /// </summary>
    [Fact]
    public async Task InitAsync_IsIdempotent_LocalStorageReadOnlyOnce()
    {
        var (svc, mock) = CreateService("EN");

        await svc.InitAsync();
        await svc.InitAsync(); // deuxième appel

        mock.Verify(s => s.GetItemAsStringAsync("lang", default), Times.Once);
    }

    /// <summary>
    /// Après InitAsync avec "EN", un second InitAsync ne remet pas la langue à "FR".
    /// </summary>
    [Fact]
    public async Task InitAsync_SecondCall_DoesNotResetLanguage()
    {
        var (svc, _) = CreateService("EN");

        await svc.InitAsync();
        await svc.InitAsync();

        Assert.Equal("EN", svc.Current);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SetAsync
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// SetAsync met à jour Current avec la langue valide fournie.
    /// </summary>
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

    /// <summary>
    /// SetAsync persiste la langue dans localStorage sous la clé "lang".
    /// </summary>
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

    /// <summary>
    /// SetAsync ignore silencieusement les codes de langue inconnus sans modifier Current.
    /// </summary>
    [Theory]
    [InlineData("ES")]
    [InlineData("IT")]
    [InlineData("")]
    [InlineData("fr")]  // sensible à la casse : "fr" ≠ "FR"
    [InlineData("EN-US")]
    public async Task SetAsync_InvalidLanguage_DoesNotChangeState(string invalid)
    {
        var (svc, mock) = CreateService();

        await svc.SetAsync(invalid);

        // La langue ne doit pas avoir changé
        Assert.Equal("FR", svc.Current);
        // localStorage ne doit pas avoir été écrit
        mock.Verify(
            s => s.SetItemAsStringAsync(It.IsAny<string>(), It.IsAny<string>(), default),
            Times.Never);
    }

    /// <summary>
    /// SetAsync déclenche l'événement OnChange après un changement de langue valide.
    /// </summary>
    [Fact]
    public async Task SetAsync_ValidLanguage_FiresOnChangeEvent()
    {
        var (svc, _) = CreateService();
        var fired = false;
        svc.OnChange += () => fired = true;

        await svc.SetAsync("EN");

        Assert.True(fired);
    }

    /// <summary>
    /// SetAsync ne déclenche pas OnChange pour une langue invalide.
    /// </summary>
    [Fact]
    public async Task SetAsync_InvalidLanguage_DoesNotFireOnChange()
    {
        var (svc, _) = CreateService();
        var fired = false;
        svc.OnChange += () => fired = true;

        await svc.SetAsync("ZZ");

        Assert.False(fired);
    }

    /// <summary>
    /// Chaque SetAsync valide déclenche OnChange exactement une fois.
    /// </summary>
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

    /// <summary>
    /// Plusieurs abonnés à OnChange sont tous notifiés.
    /// </summary>
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

    /// <summary>
    /// Après SetAsync("EN"), T() retourne les traductions anglaises.
    /// </summary>
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
