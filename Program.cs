using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Obrigenie;
using Obrigenie.Services;
using Blazored.LocalStorage;

// ──────────────────────────────────────────────────────────────────────────────
// Blazor WebAssembly entry point for the Obrigenie application.
// This file configures and registers all application services, the HTTP client
// pipeline (including the JWT auth handler), and the root Blazor components,
// then starts the WASM host.
// ──────────────────────────────────────────────────────────────────────────────

// Create the standard Blazor WebAssembly host builder using the command-line args
// passed from the HTML bootstrap script.
var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Register the root App component (App.razor) as the entry point.
// "#app" is the CSS selector of the <div id="app"> element in index.html.
builder.RootComponents.Add<App>("#app");

// Register the HeadOutlet component so that Blazor pages can use <PageTitle> and
// other head-level elements. Appended after any existing <head> content.
builder.RootComponents.Add<HeadOutlet>("head::after");

// Register Blazored.LocalStorage so that AuthService and CalendarService can
// read and write values in the browser's localStorage API.
builder.Services.AddBlazoredLocalStorage();

// Register AuthService as a scoped service.
// It manages JWT token storage and user role decoding on the client side.
builder.Services.AddScoped<AuthService>();

// Register AuthHeaderHandler as a scoped service.
// This delegating handler intercepts outgoing HTTP requests and automatically
// attaches the Bearer token to the Authorization header of every API call.
builder.Services.AddScoped<AuthHeaderHandler>();

// Configure the named "API" HttpClient that will be used by ApiService and CalendarService.
//   BaseAddress: the same origin as the Blazor app (served by the ASP.NET Core host).
//   Timeout: 15 seconds — prevents requests from hanging indefinitely on a slow connection.
//   AddHttpMessageHandler: inserts AuthHeaderHandler into the request pipeline so every
//                          request made with this client carries the JWT token.
builder.Services.AddHttpClient("API", client =>
{
    var apiUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
    client.BaseAddress = new Uri(apiUrl.EndsWith('/') ? apiUrl : apiUrl + "/");
    client.Timeout = TimeSpan.FromSeconds(15);
}).AddHttpMessageHandler<AuthHeaderHandler>();

// Register a plain HttpClient (resolved by type, not name) that is backed by the
// "API" named factory. This allows services like ApiService and CalendarService to
// receive an HttpClient via constructor injection without explicitly knowing the client name.
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("API"));

// Register ApiService as a scoped service.
// Provides strongly typed methods for all REST API operations (auth, courses, notes, licenses).
builder.Services.AddScoped<ApiService>();

// Register CalendarService as a scoped service.
// Fetches school-year calendar data from the API, caches it in localStorage, and
// provides an offline fallback when neither the API nor the cache is available.
builder.Services.AddScoped<CalendarService>();

// Build the host and start the Blazor WebAssembly application.
// RunAsync keeps the application alive until the browser tab is closed.
await builder.Build().RunAsync();
