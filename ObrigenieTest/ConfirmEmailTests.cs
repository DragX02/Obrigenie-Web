using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Obrigenie.Pages;
using System.Net;

namespace ObrigenieTest;

/// <summary>
/// Tests bUnit pour la page ConfirmEmail.razor :
/// vérifie les 3 états (chargement → succès / erreur) selon la réponse API.
/// HttpClient est mocké avec un handler personnalisé.
/// </summary>
public class ConfirmEmailTests : TestContext
{
    // ── Handler HTTP mock ─────────────────────────────────────────────────

    private class FakeHttpHandler(HttpStatusCode status, string body = "") : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body)
            });
    }

    private void RegisterHttp(HttpStatusCode status, string body = "")
    {
        Services.AddSingleton(new HttpClient(new FakeHttpHandler(status, body))
        {
            BaseAddress = new Uri("http://localhost/")
        });
    }

    private void NavigateTo(string relativeUrl)
    {
        var nav = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        nav.NavigateTo("http://localhost" + relativeUrl);
    }

    // ── Tests ─────────────────────────────────────────────────────────────

    [Fact]
    public void ConfirmEmail_SansToken_AfficheErreur()
    {
        RegisterHttp(HttpStatusCode.OK); // pas appelé

        var cut = RenderComponent<ConfirmEmail>();

        Assert.Contains("invalide", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Compte confirmé", cut.Markup);
    }

    [Fact]
    public async Task ConfirmEmail_TokenValide_AfficheSucces()
    {
        RegisterHttp(HttpStatusCode.OK);
        NavigateTo("/confirm-email?token=valid-token-abc123");

        var cut = RenderComponent<ConfirmEmail>();
        await cut.InvokeAsync(() => Task.CompletedTask);

        Assert.Contains("confirmé", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("invalide", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmEmail_TokenInvalide_AfficheErreur()
    {
        var errorBody = "{\"message\":\"Token invalide ou expiré.\"}";
        RegisterHttp(HttpStatusCode.BadRequest, errorBody);
        NavigateTo("/confirm-email?token=bad-token");

        var cut = RenderComponent<ConfirmEmail>();
        await cut.InvokeAsync(() => Task.CompletedTask);

        Assert.Contains("invalide", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Compte confirmé", cut.Markup);
    }

    [Fact]
    public async Task ConfirmEmail_ServeurInjoignable_AfficheErreurServeur()
    {
        Services.AddSingleton(new HttpClient(new ThrowingHandler())
        {
            BaseAddress = new Uri("http://localhost/")
        });
        NavigateTo("/confirm-email?token=some-token");

        var cut = RenderComponent<ConfirmEmail>();
        await cut.InvokeAsync(() => Task.CompletedTask);

        Assert.Contains("serveur", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConfirmEmail_BoutonConnexion_EstPresent()
    {
        RegisterHttp(HttpStatusCode.OK);

        var cut = RenderComponent<ConfirmEmail>();

        // Toujours un bouton de retour/connexion visible (état erreur sans token)
        Assert.Contains("connexion", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    // ── Handler qui lève une exception ───────────────────────────────────

    private class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Connexion refusée");
    }
}
