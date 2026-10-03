using Blazored.LocalStorage;
using Moq;
using Obrigenie.Services;

namespace ObrigenieTest;

public class AuthServiceTests
{
    private static string MakeJwt(string role)
    {
        var payloadJson = $"{{\"role\":\"{role}\",\"exp\":9999999999}}";

        var payloadB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payloadJson))
                             .TrimEnd('=')
                             .Replace('+', '-')
                             .Replace('/', '_');

        return $"eyJhbGciOiJIUzI1NiJ9.{payloadB64}.FAKE_SIG";
    }

    private static AuthService CreateService(string? storedToken)
    {
        var mock = new Mock<ILocalStorageService>();

        mock.Setup(s => s.GetItemAsStringAsync("jwt_token", default))
            .ReturnsAsync(storedToken);

        return new AuthService(mock.Object);
    }

    [Fact]
    public async Task GetRoleAsync_AdminToken_ReturnsAdmin()
    {
        var svc = CreateService(MakeJwt("ADMIN"));

        Assert.Equal("ADMIN", await svc.GetRoleAsync());
    }

    [Fact]
    public async Task GetRoleAsync_ProfToken_ReturnsProf()
    {
        var svc = CreateService(MakeJwt("PROF"));

        Assert.Equal("PROF", await svc.GetRoleAsync());
    }

    [Fact]
    public async Task GetRoleAsync_NoToken_ReturnsNull()
    {
        var svc = CreateService(null);

        Assert.Null(await svc.GetRoleAsync());
    }

    [Fact]
    public async Task GetRoleAsync_InvalidToken_ReturnsNull()
    {
        var svc = CreateService("not.a.valid.jwt.at.all");

        Assert.Null(await svc.GetRoleAsync());
    }

    [Fact]
    public async Task GetRoleAsync_MalformedPayload_ReturnsNull()
    {
        var svc = CreateService("header.!!!INVALID_BASE64!!!.sig");

        Assert.Null(await svc.GetRoleAsync());
    }

    [Fact]
    public async Task GetRoleAsync_PayloadWithoutRoleClaim_ReturnsNull()
    {
        var payloadJson = "{\"sub\":\"user@test.com\",\"exp\":9999999999}";
        var payloadB64  = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payloadJson))
                              .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var token = $"eyJhbGciOiJIUzI1NiJ9.{payloadB64}.sig";

        var svc = CreateService(token);

        Assert.Null(await svc.GetRoleAsync());
    }

    [Fact]
    public async Task IsLoggedInAsync_WithToken_ReturnsTrue()
    {
        var svc = CreateService(MakeJwt("PROF"));

        Assert.True(await svc.IsLoggedInAsync());
    }

    [Fact]
    public async Task IsLoggedInAsync_NoToken_ReturnsFalse()
    {
        var svc = CreateService(null);

        Assert.False(await svc.IsLoggedInAsync());
    }

    [Fact]
    public async Task IsLoggedInAsync_EmptyToken_ReturnsFalse()
    {
        var svc = CreateService("");

        Assert.False(await svc.IsLoggedInAsync());
    }

    [Fact]
    public async Task SaveTokenAsync_CallsLocalStorage()
    {
        var mock = new Mock<ILocalStorageService>();
        var svc  = new AuthService(mock.Object);

        await svc.SaveTokenAsync("my_token");

        mock.Verify(s => s.SetItemAsStringAsync("jwt_token", "my_token", default), Times.Once);
    }

    [Fact]
    public async Task RemoveTokenAsync_RemovesBothTokenAndEmail()
    {
        var mock = new Mock<ILocalStorageService>();
        var svc  = new AuthService(mock.Object);

        await svc.RemoveTokenAsync();

        mock.Verify(s => s.RemoveItemAsync("jwt_token",  default), Times.Once);
        mock.Verify(s => s.RemoveItemAsync("user_email", default), Times.Once);
    }
}
