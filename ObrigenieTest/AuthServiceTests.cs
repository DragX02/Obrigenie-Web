using Blazored.LocalStorage;
using Moq;
using Obrigenie.Services;

namespace ObrigenieTest;

/// <summary>
/// Tests unitaires pour AuthService :
/// décodage JWT client-side et gestion du stockage local.
/// ILocalStorageService est mocké — aucun navigateur requis.
/// </summary>
public class AuthServiceTests
{
    // ── Helper ───────────────────────────────────────────────────────────

    /// Génère un JWT minimal avec le claim "role" voulu.
    /// La signature est factice car GetRoleAsync ne la vérifie pas.
    private static string MakeJwt(string role)
    {
        var payloadJson = $"{{\"role\":\"{role}\"}}";
        var payloadB64  = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payloadJson))
                              .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"eyJhbGciOiJIUzI1NiJ9.{payloadB64}.FAKE_SIG";
    }

    private static AuthService CreateService(string? storedToken)
    {
        var mock = new Mock<ILocalStorageService>();
        mock.Setup(s => s.GetItemAsStringAsync("jwt_token", default))
            .ReturnsAsync(storedToken);
        return new AuthService(mock.Object);
    }

    // ── GetRoleAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetRoleAsync_TokenAdmin_RetourneAdmin()
    {
        var svc = CreateService(MakeJwt("ADMIN"));
        Assert.Equal("ADMIN", await svc.GetRoleAsync());
    }

    [Fact]
    public async Task GetRoleAsync_TokenProf_RetourneProf()
    {
        var svc = CreateService(MakeJwt("PROF"));
        Assert.Equal("PROF", await svc.GetRoleAsync());
    }

    [Fact]
    public async Task GetRoleAsync_SansToken_RetourneNull()
    {
        var svc = CreateService(null);
        Assert.Null(await svc.GetRoleAsync());
    }

    [Fact]
    public async Task GetRoleAsync_TokenInvalide_RetourneNull()
    {
        var svc = CreateService("pas.un.jwt.valide.du.tout");
        Assert.Null(await svc.GetRoleAsync());
    }

    [Fact]
    public async Task GetRoleAsync_TokenMalForme_RetourneNull()
    {
        // Payload base64 invalide
        var svc = CreateService("header.!!!INVALID_BASE64!!!.sig");
        Assert.Null(await svc.GetRoleAsync());
    }

    [Fact]
    public async Task GetRoleAsync_PayloadSansRoleClaim_RetourneNull()
    {
        var payloadJson = "{\"sub\":\"user@test.com\",\"exp\":9999999999}";
        var payloadB64  = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payloadJson))
                              .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var token = $"eyJhbGciOiJIUzI1NiJ9.{payloadB64}.sig";

        var svc = CreateService(token);
        Assert.Null(await svc.GetRoleAsync());
    }

    // ── IsLoggedInAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task IsLoggedInAsync_AvecToken_RetourneTrue()
    {
        var svc = CreateService(MakeJwt("PROF"));
        Assert.True(await svc.IsLoggedInAsync());
    }

    [Fact]
    public async Task IsLoggedInAsync_SansToken_RetourneFalse()
    {
        var svc = CreateService(null);
        Assert.False(await svc.IsLoggedInAsync());
    }

    [Fact]
    public async Task IsLoggedInAsync_TokenVide_RetourneFalse()
    {
        var svc = CreateService("");
        Assert.False(await svc.IsLoggedInAsync());
    }

    // ── SaveTokenAsync / GetTokenAsync ────────────────────────────────────

    [Fact]
    public async Task SaveTokenAsync_AppelleLocalStorage()
    {
        var mock = new Mock<ILocalStorageService>();
        var svc  = new AuthService(mock.Object);

        await svc.SaveTokenAsync("mon_token");

        mock.Verify(s => s.SetItemAsStringAsync("jwt_token", "mon_token", default), Times.Once);
    }

    [Fact]
    public async Task RemoveTokenAsync_SupprimeTokenEtEmail()
    {
        var mock = new Mock<ILocalStorageService>();
        var svc  = new AuthService(mock.Object);

        await svc.RemoveTokenAsync();

        mock.Verify(s => s.RemoveItemAsync("jwt_token",  default), Times.Once);
        mock.Verify(s => s.RemoveItemAsync("user_email", default), Times.Once);
    }
}
