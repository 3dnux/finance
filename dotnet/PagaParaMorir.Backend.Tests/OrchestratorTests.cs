using PagaParaMorir.Escrow;
using Xunit;

namespace PagaParaMorir.Backend.Tests;

public class OrchestratorTests
{
    [Fact]
    public async Task Abre_una_sala_por_tipo_y_no_duplica()
    {
        using var h = new Harness();
        await h.Tick();
        await h.Tick();
        Assert.Equal(2, h.Chain.Matches.Count);
        var cheap = h.OpenOf(Harness.Cheap);
        Assert.Equal(1000UL, cheap.MatchId);
        Assert.Equal(Usdc.OneUsdc, cheap.EntryFee);
        Assert.Equal(4, cheap.MaxPlayers);
        Assert.Equal(new[] { 1000UL, 1001UL }, h.Orchestrator.Rooms.Select(r => r.MatchId));
        Assert.Equal("1 USDC", h.Orchestrator.Rooms[0].Tier);
    }

    [Fact]
    public async Task Abre_otra_sala_cuando_una_se_llena()
    {
        using var h = new Harness();
        await h.Tick();
        var full = h.OpenOf(Harness.Pricey);
        h.Chain.Join(full.MatchId, 2);
        await h.Tick();
        var next = h.OpenOf(Harness.Pricey);
        Assert.NotEqual(full.MatchId, next.MatchId);
        Assert.Equal(3, h.Chain.Matches.Count);
    }

    [Fact]
    public async Task Levanta_el_servidor_al_juntar_dos_jugadores_una_sola_vez()
    {
        using var h = new Harness();
        await h.Tick();
        var room = h.OpenOf(Harness.Cheap);
        h.Chain.Join(room.MatchId, 1);
        await h.Tick();
        Assert.Empty(h.Launcher.Launched);
        Assert.Null(h.Orchestrator.ServerFor(room.MatchId));

        h.Chain.Join(room.MatchId, 1);
        await h.Tick();
        await h.Tick();
        var launched = Assert.Single(h.Launcher.Launched);
        Assert.Equal(room.MatchId, launched.MatchId);
        Assert.Equal(7777, launched.Port);

        var endpoint = h.Orchestrator.ServerFor(room.MatchId);
        Assert.NotNull(endpoint);
        Assert.Equal("127.0.0.1", endpoint!.Host);
        Assert.Equal(ServerStatus.Running, endpoint.Status);
    }

    [Fact]
    public async Task Cada_servidor_usa_otro_puerto()
    {
        using var h = new Harness();
        await h.Tick();
        h.Chain.Join(h.OpenOf(Harness.Cheap).MatchId, 2);
        h.Chain.Join(h.OpenOf(Harness.Pricey).MatchId, 2);
        await h.Tick();
        Assert.Equal(new ushort[] { 7777, 7778 }, h.Launcher.Launched.Select(l => l.Port).OrderBy(p => p));
    }

    [Fact]
    public async Task Si_el_servidor_muere_en_juego_se_cancela_para_reembolsar()
    {
        using var h = new Harness();
        await h.Tick();
        var room = h.OpenOf(Harness.Pricey);
        h.Chain.Join(room.MatchId, 2);
        await h.Tick();
        h.Chain.Start(room.MatchId);
        h.Launcher.Last(room.MatchId).HasExited = true;
        await h.Tick();
        Assert.Equal(MatchState.Cancelled, h.Chain.Get(room.MatchId).State);
        Assert.Null(h.Orchestrator.ServerFor(room.MatchId));
    }

    [Fact]
    public async Task Si_el_servidor_no_arranca_reintenta_y_despues_cancela()
    {
        using var h = new Harness();
        await h.Tick();
        var room = h.OpenOf(Harness.Cheap);
        h.Chain.Join(room.MatchId, 2);
        for (var i = 0; i < 3; i++)
        {
            await h.Tick();
            h.Launcher.Last(room.MatchId).HasExited = true; // se cae antes de empezar
        }
        Assert.Equal(3, h.Launcher.Launched.Count);
        Assert.Equal(MatchState.Open, h.Chain.Get(room.MatchId).State);

        await h.Tick();
        Assert.Equal(3, h.Launcher.Launched.Count);
        Assert.Equal(MatchState.Cancelled, h.Chain.Get(room.MatchId).State);
    }

    [Fact]
    public async Task Partida_atascada_se_cancela_y_se_detiene_su_servidor()
    {
        using var h = new Harness();
        await h.Tick();
        var room = h.OpenOf(Harness.Pricey);
        h.Chain.Join(room.MatchId, 2);
        await h.Tick();
        h.Chain.Start(room.MatchId);
        h.Clock.Advance(TimeSpan.FromMinutes(14));
        await h.Tick();
        Assert.Equal(MatchState.InProgress, h.Chain.Get(room.MatchId).State);

        h.Clock.Advance(TimeSpan.FromMinutes(2));
        await h.Tick();
        Assert.Equal(MatchState.Cancelled, h.Chain.Get(room.MatchId).State);
        Assert.True(h.Launcher.Last(room.MatchId).Stopped);
    }

    [Fact]
    public async Task Cierra_pagadas_y_canceladas_ya_reembolsadas()
    {
        using var h = new Harness();
        await h.Tick();
        var paid = h.OpenOf(Harness.Pricey);
        h.Chain.Join(paid.MatchId, 2);
        h.Chain.Start(paid.MatchId);
        h.Chain.Settle(paid.MatchId);

        var cancelled = h.OpenOf(Harness.Cheap);
        h.Chain.Join(cancelled.MatchId, 1);
        await h.Chain.CancelMatchAsync(cancelled.MatchId);

        await h.Tick();
        Assert.Contains($"close {paid.MatchId}", h.Chain.Calls);
        // Con un reembolso pendiente no se puede cerrar.
        Assert.DoesNotContain($"close {cancelled.MatchId}", h.Chain.Calls);

        h.Chain.Get(cancelled.MatchId).Players.Clear();
        await h.Tick();
        Assert.Contains($"close {cancelled.MatchId}", h.Chain.Calls);
    }

    [Fact]
    public async Task Sala_con_un_solo_jugador_mucho_tiempo_se_cancela()
    {
        using var h = new Harness();
        await h.Tick();
        var room = h.OpenOf(Harness.Cheap);
        h.Chain.Join(room.MatchId, 1);
        h.Clock.Advance(TimeSpan.FromMinutes(29));
        await h.Tick();
        Assert.Equal(MatchState.Open, h.Chain.Get(room.MatchId).State);

        h.Clock.Advance(TimeSpan.FromMinutes(2));
        await h.Tick();
        Assert.Equal(MatchState.Cancelled, h.Chain.Get(room.MatchId).State);
        // Y se abre otra sala de ese tipo en su lugar.
        Assert.NotEqual(room.MatchId, h.OpenOf(Harness.Cheap).MatchId);
    }

    [Fact]
    public async Task Sala_vacia_no_se_cancela_por_vieja()
    {
        using var h = new Harness();
        await h.Tick();
        h.Clock.Advance(TimeSpan.FromHours(5));
        await h.Tick();
        Assert.Equal(2, h.Chain.Matches.Count(m => m.State == MatchState.Open));
        Assert.DoesNotContain(h.Chain.Calls, c => c.StartsWith("cancel"));
    }

    [Fact]
    public async Task En_pausa_no_abre_salas()
    {
        using var h = new Harness();
        h.Chain.Paused = true;
        await h.Tick();
        Assert.Empty(h.Chain.Matches);
    }

    [Fact]
    public async Task Respeta_el_maximo_de_servidores()
    {
        using var h = new Harness(o => o.Launcher.MaxServers = 1);
        await h.Tick();
        h.Chain.Join(h.OpenOf(Harness.Cheap).MatchId, 2);
        h.Chain.Join(h.OpenOf(Harness.Pricey).MatchId, 2);
        await h.Tick();
        Assert.Single(h.Launcher.Launched);
    }

    [Fact]
    public async Task Servidor_de_una_sala_cerrada_se_detiene_si_no_sale_solo()
    {
        using var h = new Harness();
        await h.Tick();
        var room = h.OpenOf(Harness.Pricey);
        h.Chain.Join(room.MatchId, 2);
        await h.Tick();
        // El servidor pagó y cerró la sala, pero el proceso no termina.
        h.Chain.Matches.Remove(h.Chain.Get(room.MatchId));
        await h.Tick();
        Assert.False(h.Launcher.Last(room.MatchId).Stopped);
        h.Clock.Advance(TimeSpan.FromMinutes(3));
        await h.Tick();
        Assert.True(h.Launcher.Last(room.MatchId).Stopped);
    }

    [Fact]
    public async Task Los_ids_no_se_reutilizan_aunque_la_sala_se_cierre()
    {
        using var h = new Harness(o => o.Tiers.RemoveAt(1));
        await h.Tick();
        var first = h.OpenOf(Harness.Cheap).MatchId;
        h.Chain.Matches.Clear(); // se cerró y su cuenta ya no existe
        await h.Tick();
        Assert.Equal(first + 1, h.OpenOf(Harness.Cheap).MatchId);
    }

    [Fact]
    public async Task Un_fallo_al_abrir_salas_no_frena_el_resto()
    {
        using var h = new Harness();
        await h.Tick();
        var room = h.OpenOf(Harness.Pricey);
        h.Chain.Join(room.MatchId, 2); // se llena: habría que abrir otra
        h.Chain.FailCreates = true;
        await h.Tick();
        Assert.Single(h.Launcher.Launched);
        Assert.NotNull(h.Orchestrator.LastTickAt);
    }
}
