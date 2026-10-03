using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Obrigenie.Pages;
using System.Net;

namespace ObrigenieTest;

public class ConfirmEmailTests : TestContext
{
    private class FakeHttpHandler(HttpStatusCode status, string body = "") : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
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

    [Fact]
    public void ConfirmEmail_NoToken_ShowsError()
    {
        RegisterHttp(HttpStatusCode.OK);

        var cut = RenderComponent<ConfirmEmail>();

        Assert.Contains("invalid", cut.Markup, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("confirmed", cut.Markup);
    }

    [Fact]
    public void ConfirmEmail_ValidToken_ShowsSuccess()
    {
        RegisterHttp(HttpStatusCode.OK);

        NavigateTo("/confirm-email?token=valid-token-abc123");

        var cut = RenderComponent<ConfirmEmail>();

        cut.WaitForAssertion(() => Assert.Contains("confirmé", cut.Markup, StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain("invalide", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmEmail_InvalidToken_ShowsError()
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
    public void ConfirmEmail_ServerUnreachable_ShowsServerError()
    {
        Services.AddSingleton(new HttpClient(new ThrowingHandler())
        {
            BaseAddress = new Uri("http://localhost/")
        });

        NavigateTo("/confirm-email?token=some-token");

        var cut = RenderComponent<ConfirmEmail>();

        cut.WaitForAssertion(() => Assert.Contains("serveur", cut.Markup, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ConfirmEmail_LoginButton_AlwaysPresent()
    {
        RegisterHttp(HttpStatusCode.OK);

        var cut = RenderComponent<ConfirmEmail>();

        cut.WaitForAssertion(() =>
            Assert.Contains("connexion", cut.Markup, StringComparison.OrdinalIgnoreCase));
    }

    private class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("Connection refused");
    }
}
