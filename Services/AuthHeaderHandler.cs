using System.Net.Http.Headers;

namespace Obrigenie.Services
{
    /// <summary>
    /// A delegating HTTP message handler that automatically attaches the JWT bearer token
    /// to every outgoing HTTP request made through the named "API" HttpClient.
    /// This handler is registered in Program.cs via AddHttpMessageHandler and sits in the
    /// middleware pipeline between the HttpClient and the actual HTTP transport.
    /// As a result, all services that use the injected HttpClient (ApiService, CalendarService)
    /// will have the Authorization header added without any manual token management.
    /// </summary>
    public class AuthHeaderHandler : DelegatingHandler
    {
        /// <summary>
        /// The AuthService instance used to retrieve the stored JWT token from local storage.
        /// Injected via constructor dependency injection.
        /// </summary>
        private readonly AuthService _authService;

        /// <summary>
        /// Initialises the handler by storing a reference to the authentication service.
        /// </summary>
        /// <param name="authService">The service that provides access to the stored JWT token.</param>
        public AuthHeaderHandler(AuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Intercepts every outgoing HTTP request, reads the JWT token from local storage,
        /// and appends it as a "Bearer" Authorization header if a non-empty token is found.
        /// Execution then continues to the next handler in the pipeline (ultimately sending
        /// the request to the server).
        /// </summary>
        /// <param name="request">The outgoing HTTP request message to modify.</param>
        /// <param name="cancellationToken">Token to observe for cooperative cancellation.</param>
        /// <returns>The HTTP response message received from the server.</returns>
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Retrieve the JWT token that was saved to local storage after the last login
            var token = await _authService.GetTokenAsync();

            // Only set the Authorization header when a non-empty token is available.
            // Unauthenticated requests (e.g., login, register) pass through unmodified.
            if (!string.IsNullOrEmpty(token))
            {
                // Attach the token using the standard "Bearer" authentication scheme
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // Forward the (possibly modified) request to the next handler and return its response
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
