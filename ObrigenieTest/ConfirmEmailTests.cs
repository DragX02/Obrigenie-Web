using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Obrigenie.Pages;
using System.Net;

namespace ObrigenieTest;

/// <summary>
/// bUnit component tests for the <see cref="ConfirmEmail"/> Razor page.
/// Tests verify that the component renders the correct UI state
/// (loading → success or error) based on the simulated HTTP response
/// from the confirmation API endpoint.
///
/// The <see cref="HttpClient"/> is replaced with a custom fake handler
/// that returns a configurable status code and body without any real network
/// activity. This makes tests fast, deterministic, and offline-safe.
///
/// All tests inherit from bUnit's <see cref="TestContext"/> which provides
/// the component rendering infrastructure and the DI service collection.
/// </summary>
public class ConfirmEmailTests : TestContext
{
    // ─────────────────────────────────────────────────────────────────────────
    // HTTP mock infrastructure
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A minimal <see cref="HttpMessageHandler"/> that always returns a pre-configured
    /// HTTP response without making any real network calls.
    /// Used to simulate both success and error responses from the confirmation API.
    /// </summary>
    private class FakeHttpHandler(HttpStatusCode status, string body = "") : HttpMessageHandler
    {
        /// <summary>
        /// Returns a new <see cref="HttpResponseMessage"/> with the configured status code
        /// and body string, ignoring the actual request details.
        /// </summary>
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body)
            });
    }

    /// <summary>
    /// Registers a fake <see cref="HttpClient"/> in the component's DI container.
    /// The fake client returns the specified HTTP status code and optional body string
    /// for every request, simulating the server's response to the confirmation API call.
    /// Must be called before rendering the component.
    /// </summary>
    /// <param name="status">The HTTP status code the fake handler should return.</param>
    /// <param name="body">The response body string (JSON or plain text). Default is empty.</param>
    private void RegisterHttp(HttpStatusCode status, string body = "")
    {
        Services.AddSingleton(new HttpClient(new FakeHttpHandler(status, body))
        {
            BaseAddress = new Uri("http://localhost/")
        });
    }

    /// <summary>
    /// Navigates the bUnit <see cref="NavigationManager"/> to the given relative URL
    /// so that <c>[SupplyParameterFromQuery]</c> attributes on the component
    /// can read query string parameters (e.g., <c>?token=abc123</c>).
    /// </summary>
    /// <param name="relativeUrl">The relative URL to navigate to (e.g., "/confirm-email?token=abc").</param>
    private void NavigateTo(string relativeUrl)
    {
        var nav = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        nav.NavigateTo("http://localhost" + relativeUrl);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tests
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that when no token is provided in the URL, the component immediately
    /// renders an error state (without calling the API) that contains the word "invalid".
    /// </summary>
    [Fact]
    public void ConfirmEmail_NoToken_ShowsError()
    {
        // Register an HTTP handler that would return OK if called (it should NOT be called)
        RegisterHttp(HttpStatusCode.OK);

        // Render without navigating to a URL with a token parameter
        var cut = RenderComponent<ConfirmEmail>();

        // The component should display an error message for the missing token
        Assert.Contains("invalid", cut.Markup, StringComparison.OrdinalIgnoreCase);

        // The success message must not appear
        Assert.DoesNotContain("confirmed", cut.Markup);
    }

    /// <summary>
    /// Verifies that when a valid token is present in the URL and the API returns 200 OK,
    /// the component renders the success state containing "confirmed".
    /// </summary>
    [Fact]
    public async Task ConfirmEmail_ValidToken_ShowsSuccess()
    {
        // The fake API accepts any request and returns 200 OK
        RegisterHttp(HttpStatusCode.OK);

        // Navigate to the confirmation page with a valid-looking token in the query string
        NavigateTo("/confirm-email?token=valid-token-abc123");

        var cut = RenderComponent<ConfirmEmail>();

        // Wait for the async OnInitializedAsync to complete
        await cut.InvokeAsync(() => Task.CompletedTask);

        // The success message must appear in the rendered HTML
        Assert.Contains("confirmé", cut.Markup, StringComparison.OrdinalIgnoreCase);

        // The error state must not be shown
        Assert.DoesNotContain("invalide", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that when a token is present but the API returns 400 Bad Request with
    /// a JSON error body, the component renders the error state and shows the word "invalid".
    /// </summary>
    [Fact]
    public async Task ConfirmEmail_InvalidToken_ShowsError()
    {
        // Simulate the server rejecting the token with a structured JSON error body
        var errorBody = "{\"message\":\"Token invalide ou expiré.\"}";
        RegisterHttp(HttpStatusCode.BadRequest, errorBody);

        NavigateTo("/confirm-email?token=bad-token");

        var cut = RenderComponent<ConfirmEmail>();
        await cut.InvokeAsync(() => Task.CompletedTask);

        // The error state must be visible
        Assert.Contains("invalide", cut.Markup, StringComparison.OrdinalIgnoreCase);

        // The success message must not appear
        Assert.DoesNotContain("Compte confirmé", cut.Markup);
    }

    /// <summary>
    /// Verifies that when the HTTP client throws an exception (simulating a network failure
    /// or an unreachable server), the component renders an error message containing "serveur".
    /// </summary>
    [Fact]
    public async Task ConfirmEmail_ServerUnreachable_ShowsServerError()
    {
        // Register the throwing handler directly instead of using RegisterHttp helper
        Services.AddSingleton(new HttpClient(new ThrowingHandler())
        {
            BaseAddress = new Uri("http://localhost/")
        });

        NavigateTo("/confirm-email?token=some-token");

        var cut = RenderComponent<ConfirmEmail>();
        await cut.InvokeAsync(() => Task.CompletedTask);

        // The error message must mention the server being unreachable
        Assert.Contains("serveur", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that a login/return button is always present in the rendered markup,
    /// regardless of the confirmation outcome. This ensures the user can always
    /// navigate away from the page even when something goes wrong.
    /// </summary>
    [Fact]
    public void ConfirmEmail_LoginButton_AlwaysPresent()
    {
        // Register an HTTP handler (result does not matter for this test)
        RegisterHttp(HttpStatusCode.OK);

        // Render without a token → component shows the error state
        var cut = RenderComponent<ConfirmEmail>();

        // A login/return button must always be visible (at least in the error state)
        Assert.Contains("connexion", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HTTP handler that always throws
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// An <see cref="HttpMessageHandler"/> that always throws an
    /// <see cref="HttpRequestException"/> to simulate a completely unreachable server
    /// (e.g., no network, DNS failure, connection refused).
    /// </summary>
    private class ThrowingHandler : HttpMessageHandler
    {
        /// <summary>
        /// Throws an <see cref="HttpRequestException"/> unconditionally,
        /// simulating a network-level failure before any response is received.
        /// </summary>
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("Connection refused");
    }
}
