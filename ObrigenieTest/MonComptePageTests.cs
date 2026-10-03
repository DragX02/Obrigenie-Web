using Blazored.LocalStorage;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Obrigenie.Pages;
using Obrigenie.Services;

namespace ObrigenieTest;

public class MonComptePageTests : TestContext
{
    private static LanguageService MakeLangService(string lang = "FR")
    {
        var mock = new Mock<ILocalStorageService>();
        mock.Setup(s => s.GetItemAsStringAsync("lang", default)).ReturnsAsync(lang);
        return new LanguageService(mock.Object);
    }

    private static AuthService MakeAuthService(string? email = "prof@school.be")
    {
        var mock = new Mock<ILocalStorageService>();
        mock.Setup(s => s.GetItemAsStringAsync("user_email", default)).ReturnsAsync(email);
        mock.Setup(s => s.GetItemAsStringAsync("jwt_token", default))
            .ReturnsAsync(email is null ? null : "eyJhbGciOiJIUzI1NiJ9.eyJyb2xlIjoiUFJPRiIsImV4cCI6OTk5OTk5OTk5OX0.FAKE");
        return new AuthService(mock.Object);
    }

    private IRenderedComponent<MonComptePage> RenderPage(
        string lang  = "FR",
        string? email = "prof@school.be")
    {
        Services.AddScoped(_ => MakeLangService(lang));
        Services.AddScoped(_ => MakeAuthService(email));
        return RenderComponent<MonComptePage>();
    }

    [Fact]
    public void MonComptePage_ShowsUserEmail()
    {
        var cut = RenderPage(email: "alice@school.be");

        cut.WaitForAssertion(() =>
            Assert.Contains("alice@school.be", cut.Markup));
    }

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

    [Fact]
    public void MonComptePage_ClickEN_ENBecomesActive()
    {
        var cut = RenderPage(lang: "FR");

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".btn-lang").Count));
        cut.FindAll(".btn-lang")[1].Click();

        cut.WaitForAssertion(() =>
        {
            var buttons = cut.FindAll(".btn-lang");
            Assert.Contains("btn-lang-active", buttons[1].ClassName);
        });
    }

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

    [Fact]
    public void MonComptePage_AfterClick_ShowsSavedMessage()
    {
        var cut = RenderPage(lang: "FR");

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".btn-lang").Count));
        cut.FindAll(".btn-lang")[1].Click();

        cut.WaitForAssertion(() =>
            Assert.NotEmpty(cut.FindAll(".lang-saved-msg")));
    }

    [Fact]
    public void MonComptePage_AfterClick_StillExactlyOneActiveButton()
    {
        var cut = RenderPage(lang: "FR");

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".btn-lang").Count));
        cut.FindAll(".btn-lang")[1].Click();

        cut.WaitForAssertion(() =>
            Assert.Single(cut.FindAll(".btn-lang-active")));
    }

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

    [Fact]
    public void MonComptePage_BackButton_AlwaysPresent()
    {
        var cut = RenderPage();

        cut.WaitForAssertion(() =>
            Assert.NotEmpty(cut.FindAll(".btn-account-back")));
    }

    [Fact]
    public void MonComptePage_ClickBackButton_NavigatesWithoutError()
    {
        var cut = RenderPage();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".btn-account-back")));

        var ex = Record.Exception(() => cut.Find(".btn-account-back").Click());
        Assert.Null(ex);
    }
}
