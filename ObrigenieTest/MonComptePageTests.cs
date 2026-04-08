using Blazored.LocalStorage;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Obrigenie.Pages;
using Obrigenie.Services;

namespace ObrigenieTest;

/// <summary>
/// Tests de composant bUnit pour <see cref="MonComptePage"/>.
///
/// Couvre :
///   - Affichage de l'e-mail et de l'initiale de l'utilisateur.
///   - Présence et état actif des 3 boutons de langue.
///   - Clic sur un bouton de langue → changement d'état actif + message "enregistré".
///   - Traductions affichées selon la langue active.
///   - Présence du bouton retour.
///
/// <see cref="LanguageService"/> et <see cref="AuthService"/> sont instanciés avec
/// des mocks <see cref="ILocalStorageService"/> enregistrés dans le DI de bUnit.
/// </summary>
public class MonComptePageTests : TestContext
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Crée un <see cref="LanguageService"/> dont le localStorage renvoie
    /// <paramref name="lang"/> pour la clé "lang".
    /// </summary>
    private static LanguageService MakeLangService(string lang = "FR")
    {
        var mock = new Mock<ILocalStorageService>();
        mock.Setup(s => s.GetItemAsStringAsync("lang", default)).ReturnsAsync(lang);
        return new LanguageService(mock.Object);
    }

    /// <summary>
    /// Crée un <see cref="AuthService"/> dont le localStorage renvoie
    /// <paramref name="email"/> pour la clé "user_email".
    /// </summary>
    private static AuthService MakeAuthService(string? email = "prof@school.be")
    {
        var mock = new Mock<ILocalStorageService>();
        mock.Setup(s => s.GetItemAsStringAsync("user_email", default)).ReturnsAsync(email);
        // jwt_token : présent si email non null (pour IsLoggedInAsync, non utilisé ici)
        mock.Setup(s => s.GetItemAsStringAsync("jwt_token", default))
            .ReturnsAsync(email is null ? null : "eyJhbGciOiJIUzI1NiJ9.eyJyb2xlIjoiUFJPRiIsImV4cCI6OTk5OTk5OTk5OX0.FAKE");
        return new AuthService(mock.Object);
    }

    /// <summary>
    /// Enregistre les services et rend le composant <see cref="MonComptePage"/>.
    /// </summary>
    private IRenderedComponent<MonComptePage> RenderPage(
        string lang  = "FR",
        string? email = "prof@school.be")
    {
        Services.AddScoped(_ => MakeLangService(lang));
        Services.AddScoped(_ => MakeAuthService(email));
        return RenderComponent<MonComptePage>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Affichage de l'e-mail et de l'initiale
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// L'adresse e-mail de l'utilisateur est affichée dans le composant.
    /// </summary>
    [Fact]
    public void MonComptePage_ShowsUserEmail()
    {
        var cut = RenderPage(email: "alice@school.be");

        cut.WaitForAssertion(() =>
            Assert.Contains("alice@school.be", cut.Markup));
    }

    /// <summary>
    /// L'initiale affichée correspond à la première lettre de l'e-mail, en majuscule.
    /// </summary>
    [Theory]
    [InlineData("alice@school.be", "A")]
    [InlineData("bob@school.be",   "B")]
    [InlineData("Prof@school.be",  "P")]
    public void MonComptePage_ShowsUppercaseInitial(string email, string initial)
    {
        var cut = RenderPage(email: email);

        cut.WaitForAssertion(() =>
        {
            var avatar = cut.Find(".account-avatar-large");
            Assert.Equal(initial, avatar.TextContent.Trim());
        });
    }

    /// <summary>
    /// Quand aucun e-mail n'est stocké, l'initiale par défaut est "U".
    /// </summary>
    [Fact]
    public void MonComptePage_NoEmail_ShowsDefaultInitialU()
    {
        var cut = RenderPage(email: null);

        cut.WaitForAssertion(() =>
        {
            var avatar = cut.Find(".account-avatar-large");
            Assert.Equal("U", avatar.TextContent.Trim());
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Boutons de langue
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Exactement 3 boutons de langue sont affichés.
    /// </summary>
    [Fact]
    public void MonComptePage_ShowsExactlyThreeLanguageButtons()
    {
        var cut = RenderPage();

        cut.WaitForAssertion(() =>
        {
            var buttons = cut.FindAll(".btn-lang");
            Assert.Equal(3, buttons.Count);
        });
    }

    /// <summary>
    /// Exactement un bouton porte la classe active quelle que soit la langue initiale.
    /// </summary>
    [Theory]
    [InlineData("FR")]
    [InlineData("EN")]
    [InlineData("NL")]
    public void MonComptePage_ExactlyOneActiveButton(string lang)
    {
        var cut = RenderPage(lang: lang);

        cut.WaitForAssertion(() =>
        {
            var active = cut.FindAll(".btn-lang-active");
            Assert.Single(active);
        });
    }

    /// <summary>
    /// Le bouton FR est actif quand la langue courante est FR.
    /// </summary>
    [Fact]
    public void MonComptePage_FR_FirstButtonIsActive()
    {
        var cut = RenderPage(lang: "FR");

        cut.WaitForAssertion(() =>
        {
            var buttons = cut.FindAll(".btn-lang");
            Assert.Contains("btn-lang-active", buttons[0].ClassName);
            Assert.DoesNotContain("btn-lang-active", buttons[1].ClassName ?? "");
            Assert.DoesNotContain("btn-lang-active", buttons[2].ClassName ?? "");
        });
    }

    /// <summary>
    /// Le bouton EN est actif quand la langue courante est EN.
    /// </summary>
    [Fact]
    public void MonComptePage_EN_SecondButtonIsActive()
    {
        var cut = RenderPage(lang: "EN");

        cut.WaitForAssertion(() =>
        {
            var buttons = cut.FindAll(".btn-lang");
            Assert.DoesNotContain("btn-lang-active", buttons[0].ClassName ?? "");
            Assert.Contains("btn-lang-active", buttons[1].ClassName);
            Assert.DoesNotContain("btn-lang-active", buttons[2].ClassName ?? "");
        });
    }

    /// <summary>
    /// Le bouton NL est actif quand la langue courante est NL.
    /// </summary>
    [Fact]
    public void MonComptePage_NL_ThirdButtonIsActive()
    {
        var cut = RenderPage(lang: "NL");

        cut.WaitForAssertion(() =>
        {
            var buttons = cut.FindAll(".btn-lang");
            Assert.DoesNotContain("btn-lang-active", buttons[0].ClassName ?? "");
            Assert.DoesNotContain("btn-lang-active", buttons[1].ClassName ?? "");
            Assert.Contains("btn-lang-active", buttons[2].ClassName);
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Clic sur un bouton de langue
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Cliquer sur le bouton EN (2ème) quand FR est actif rend EN actif.
    /// </summary>
    [Fact]
    public void MonComptePage_ClickEN_ENBecomesActive()
    {
        var cut = RenderPage(lang: "FR");

        // Attend le rendu initial puis clique sur le 2ème bouton (EN)
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".btn-lang").Count));
        cut.FindAll(".btn-lang")[1].Click();

        cut.WaitForAssertion(() =>
        {
            var buttons = cut.FindAll(".btn-lang");
            Assert.Contains("btn-lang-active", buttons[1].ClassName);
        });
    }

    /// <summary>
    /// Cliquer sur le bouton NL (3ème) quand FR est actif rend NL actif.
    /// </summary>
    [Fact]
    public void MonComptePage_ClickNL_NLBecomesActive()
    {
        var cut = RenderPage(lang: "FR");

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".btn-lang").Count));
        cut.FindAll(".btn-lang")[2].Click();

        cut.WaitForAssertion(() =>
        {
            var buttons = cut.FindAll(".btn-lang");
            Assert.Contains("btn-lang-active", buttons[2].ClassName);
        });
    }

    /// <summary>
    /// Après un clic sur un bouton, le message de confirmation "enregistré" apparaît.
    /// </summary>
    [Fact]
    public void MonComptePage_AfterClick_ShowsSavedMessage()
    {
        var cut = RenderPage(lang: "FR");

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".btn-lang").Count));
        cut.FindAll(".btn-lang")[1].Click(); // clique sur EN

        cut.WaitForAssertion(() =>
            Assert.NotEmpty(cut.FindAll(".lang-saved-msg")));
    }

    /// <summary>
    /// Un seul bouton reste actif après le changement (pas de double sélection).
    /// </summary>
    [Fact]
    public void MonComptePage_AfterClick_StillExactlyOneActiveButton()
    {
        var cut = RenderPage(lang: "FR");

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".btn-lang").Count));
        cut.FindAll(".btn-lang")[1].Click();

        cut.WaitForAssertion(() =>
            Assert.Single(cut.FindAll(".btn-lang-active")));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Traductions affichées
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// En mode FR, les labels de langue s'affichent en français.
    /// </summary>
    [Fact]
    public void MonComptePage_FR_ShowsFrenchLabels()
    {
        var cut = RenderPage(lang: "FR");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Français",    cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Anglais",     cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Néerlandais", cut.Markup, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>
    /// En mode EN, les labels de langue s'affichent en anglais.
    /// </summary>
    [Fact]
    public void MonComptePage_EN_ShowsEnglishLabels()
    {
        var cut = RenderPage(lang: "EN");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("French",  cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("English", cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Dutch",   cut.Markup, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>
    /// En mode NL, les labels de langue s'affichent en néerlandais.
    /// </summary>
    [Fact]
    public void MonComptePage_NL_ShowsDutchLabels()
    {
        var cut = RenderPage(lang: "NL");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Frans",      cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Engels",     cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Nederlands", cut.Markup, StringComparison.OrdinalIgnoreCase);
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Bouton retour
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Le bouton retour est toujours présent dans le composant.
    /// </summary>
    [Fact]
    public void MonComptePage_BackButton_AlwaysPresent()
    {
        var cut = RenderPage();

        cut.WaitForAssertion(() =>
            Assert.NotEmpty(cut.FindAll(".btn-account-back")));
    }

    /// <summary>
    /// Cliquer sur le bouton retour déclenche une navigation sans lever d'exception.
    /// </summary>
    [Fact]
    public void MonComptePage_ClickBackButton_NavigatesWithoutError()
    {
        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".btn-account-back")));

        // Ne doit pas lever d'exception
        var ex = Record.Exception(() => cut.Find(".btn-account-back").Click());
        Assert.Null(ex);
    }
}
