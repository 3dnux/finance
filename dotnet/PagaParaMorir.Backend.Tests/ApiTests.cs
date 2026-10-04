using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PagaParaMorir.Escrow;
using Xunit;

namespace PagaParaMorir.Backend.Tests;

public class ApiTests : IClassFixture<ApiTests.Factory>
{
    /// <summary>El backend real con la cadena y el lanzador falsos.</summary>
    public sealed class Factory : WebApplicationFactory<Program>
    {
        public FakeClock Clock { get; } = new();
        public FakeChain Chain { get; }
        public FakeLauncher Launcher { get; } = new();
        private readonly string _stateFile = Path.Combine(Path.GetTempPath(), $"ppm-api-{Guid.NewGuid():N}.json");

        public Factory()
        {
            Chain = new FakeChain(Clock);
        }

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IChain>();
                services.RemoveAll<IGameServerLauncher>();
                services.RemoveAll<TimeProvider>();
                services.RemoveAll<StateStore>();
                services.AddSingleton<IChain>(Chain);
                services.AddSingleton<IGameServerLauncher>(Launcher);
                services.AddSingleton<TimeProvider>(Clock);
                services.AddSingleton(new StateStore(_stateFile, 1000));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (File.Exists(_stateFile)) File.Delete(_stateFile);
        }
    }

    private readonly Factory _factory;

    public ApiTests(Factory factory)
    {
        _factory = factory;
    }

    private Orchestrator Orchestrator => _factory.Services.GetRequiredService<Orchestrator>();

    [Fact]
    public async Task Salas_y_servidor_de_una_partida()
    {
        var http = _factory.CreateClient();
        await Orchestrator.TickAsync();

        var health = await http.GetFromJsonAsync<JsonElement>("/api/health");
        Assert.True(health.GetProperty("ok").GetBoolean());

        var rooms = (await http.GetFromJsonAsync<JsonElement>("/api/rooms")).GetProperty("rooms");
        Assert.Equal(3, rooms.GetArrayLength()); // los 3 tipos de appsettings.json
        var first = rooms[0];
        Assert.Equal("1.00 USDC", first.GetProperty("entry").GetString());
        Assert.Equal("Open", first.GetProperty("state").GetString());
        var matchId = first.GetProperty("matchId").GetUInt64();

        var notReady = await http.GetAsync($"/api/matches/{matchId}/server");
        Assert.Equal(HttpStatusCode.NotFound, notReady.StatusCode);
        var error = (await notReady.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
        Assert.Contains("se abre cuando hayan pagado 2 jugadores", error);

        _factory.Chain.Join(matchId, 2);
        await Orchestrator.TickAsync();
        var server = await http.GetFromJsonAsync<JsonElement>($"/api/matches/{matchId}/server");
        Assert.Equal("127.0.0.1", server.GetProperty("host").GetString());
        Assert.Equal(7777, server.GetProperty("port").GetInt32());
        Assert.Equal("Running", server.GetProperty("status").GetString());

        var missing = await http.GetAsync("/api/matches/999999/server");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Tipos_de_sala()
    {
        var tiers = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/tiers");
        Assert.Equal(3, tiers.GetArrayLength());
        Assert.Equal(5 * Usdc.OneUsdc, tiers[1].GetProperty("entryFee").GetUInt64());
    }
}
