using System.Linq;
using PagaParaMorir.Rules;
using Xunit;

namespace PagaParaMorir.Rules.Tests
{
    /// <summary>Quién gana decide a quién se le paga: estas reglas tienen que ser exactas.</summary>
    public class RefereeTests
    {
        private static RefereeSettings Settings(int maxPlayers = 4) => new RefereeSettings
        {
            MinPlayers = 2,
            MaxPlayers = maxPlayers,
            LobbySeconds = 60,
            CountdownSeconds = 5,
            TimeLimitSeconds = 300,
        };

        /// <summary>Partida en juego (t = 5) con los jugadores dados.</summary>
        private static MatchReferee Playing(params string[] ids)
        {
            var r = new MatchReferee(Settings(ids.Length), 0, ids);
            foreach (var id in ids) r.Join(id);
            Assert.Equal(LobbyDecision.Start, r.EvaluateLobby(0));
            r.BeginCountdown(0);
            r.Update(5);
            Assert.Equal(MatchPhase.Playing, r.Phase);
            return r;
        }

        private static void Kill(MatchReferee r, string attacker, string victim, double at)
        {
            Assert.NotNull(r.ApplyDamage(attacker, victim, 1000, at));
        }

        [Fact]
        public void Solo_entran_quienes_pagaron()
        {
            var r = new MatchReferee(Settings(), 0, new[] { "ana", "beto" });
            Assert.False(r.CanJoin("intruso", out var reason));
            Assert.Equal("No pagaste la entrada de esta partida.", reason);
            r.Join("ana");
            Assert.False(r.CanJoin("ana", out reason));
            Assert.Equal("Ya estás conectado desde otro equipo.", reason);
        }

        [Fact]
        public void Empieza_cuando_la_sala_esta_llena_y_todos_conectados()
        {
            var r = new MatchReferee(Settings(maxPlayers: 3), 0, new[] { "a", "b", "c" });
            r.Join("a");
            r.Join("b");
            Assert.Equal(LobbyDecision.Wait, r.EvaluateLobby(10));
            r.Join("c");
            Assert.Equal(LobbyDecision.Start, r.EvaluateLobby(10));
        }

        [Fact]
        public void Al_vencer_la_espera_empieza_con_los_conectados_o_se_cancela()
        {
            var r = new MatchReferee(Settings(), 0, new[] { "a", "b", "c" });
            r.Join("a");
            Assert.Equal(LobbyDecision.Wait, r.EvaluateLobby(59));
            Assert.Equal(LobbyDecision.Cancel, r.EvaluateLobby(60));

            r.Join("b");
            Assert.Equal(LobbyDecision.Start, r.EvaluateLobby(60));
            r.BeginCountdown(60);
            Assert.Equal(2, r.StartedWith);
            Assert.False(r.CanJoin("c", out _));
        }

        [Fact]
        public void Salir_de_la_sala_de_espera_no_elimina()
        {
            var r = new MatchReferee(Settings(), 0, new[] { "a", "b" });
            r.Join("a");
            Assert.Null(r.Leave("a", 1));
            Assert.Equal(0, r.ConnectedCount);
            Assert.True(r.CanJoin("a", out _));
        }

        [Fact]
        public void No_hay_dano_antes_de_empezar()
        {
            var r = new MatchReferee(Settings(2), 0, new[] { "a", "b" });
            r.Join("a");
            r.Join("b");
            Assert.Null(r.ApplyDamage("a", "b", 50, 1));
            r.BeginCountdown(1);
            Assert.Null(r.ApplyDamage("a", "b", 50, 2));
            Assert.Equal(100, r.Get("b").Health);
        }

        [Fact]
        public void El_ultimo_en_pie_gana()
        {
            var r = Playing("a", "b", "c");
            Kill(r, "a", "b", 10);
            Assert.Equal(MatchPhase.Playing, r.Phase);
            Kill(r, "c", "a", 20);
            Assert.Equal(MatchPhase.Finished, r.Phase);
            Assert.Equal("c", r.Result.WinnerId);
            Assert.Equal(WinReason.LastAlive, r.Result.Reason);
            Assert.Equal(1, r.Get("c").Kills);
        }

        [Fact]
        public void Dano_se_acumula_y_no_hay_fuego_amigo_contra_uno_mismo()
        {
            var r = Playing("a", "b");
            Assert.Null(r.ApplyDamage("a", "a", 50, 6));
            Assert.Null(r.ApplyDamage("a", "b", 60, 6));
            Assert.Equal(40, r.Get("b").Health);
            Assert.Equal(60, r.Get("a").DamageDealt);
            var e = r.ApplyDamage("a", "b", 60, 7);
            Assert.Equal(EliminationCause.Weapon, e.Cause);
            Assert.Equal("a", e.KillerId);
            Assert.Equal(100, r.Get("a").DamageDealt); // solo cuenta el daño real
            Assert.Null(r.ApplyDamage("a", "b", 10, 8)); // ya no hay partida
        }

        [Fact]
        public void Desconectarse_en_juego_es_perder()
        {
            var r = Playing("a", "b");
            var e = r.Leave("b", 30);
            Assert.Equal(EliminationCause.Disconnected, e.Cause);
            Assert.Equal("a", r.Result.WinnerId);
        }

        [Fact]
        public void Si_mueren_juntos_en_la_zona_gana_quien_tenga_mas_bajas()
        {
            var r = Playing("a", "b", "c");
            Kill(r, "b", "c", 10);
            // a y b se quedan fuera de la zona hasta morir en el mismo instante.
            var eliminations = r.ApplyZoneDamage(new[] { "a", "b" }, 1000, 50);
            Assert.Equal(2, eliminations.Count);
            Assert.Equal("b", r.Result.WinnerId);
            Assert.Equal(WinReason.LastEliminated, r.Result.Reason);
        }

        [Fact]
        public void Empate_total_lo_gana_quien_entro_primero()
        {
            var r = Playing("a", "b");
            r.ApplyZoneDamage(new[] { "a", "b" }, 1000, 50);
            Assert.Equal("a", r.Result.WinnerId);
        }

        [Fact]
        public void Si_todos_se_desconectan_a_la_vez_se_cancela_y_hay_reembolso()
        {
            // Ambos se desconectan en el mismo instante durante la cuenta regresiva.
            var r = new MatchReferee(Settings(2), 0, new[] { "a", "b" });
            r.Join("a");
            r.Join("b");
            r.BeginCountdown(0);
            r.Leave("a", 2);
            r.Leave("b", 2);
            r.Update(5);
            Assert.Equal(MatchPhase.Cancelled, r.Phase);
            Assert.Null(r.Result);
        }

        [Fact]
        public void Al_acabarse_el_tiempo_gana_el_vivo_con_mas_vida()
        {
            var r = Playing("a", "b", "c");
            r.ApplyDamage("a", "b", 30, 10);
            r.ApplyDamage("b", "c", 50, 11);
            r.Update(5 + 299);
            Assert.Equal(MatchPhase.Playing, r.Phase);
            r.Update(5 + 300);
            Assert.Equal(MatchPhase.Finished, r.Phase);
            Assert.Equal("a", r.Result.WinnerId);
            Assert.Equal(WinReason.TimeLimit, r.Result.Reason);
        }

        [Fact]
        public void La_zona_hace_dano_fraccionado_por_segundo()
        {
            var r = Playing("a", "b");
            // Primera fase: 2 de daño por segundo.
            for (var i = 0; i < 10; i++) r.ApplyZoneDamage(new[] { "a" }, 0.1, 5 + i * 0.1);
            Assert.Equal(98, r.Get("a").Health);
            Assert.Equal(100, r.Get("b").Health);
        }

        [Fact]
        public void Practica_se_puede_jugar_solo()
        {
            var r = new MatchReferee(RefereeSettings.Practice(), 0);
            r.Join("yo");
            Assert.Equal(LobbyDecision.Wait, r.EvaluateLobby(5));
            Assert.Equal(LobbyDecision.Start, r.EvaluateLobby(10));
            r.BeginCountdown(10);
            r.Update(15);
            Assert.Equal(MatchPhase.Playing, r.Phase);
            Assert.False(r.CanJoin("otro", out _));
        }

        [Fact]
        public void Nadie_conectado_al_empezar_queda_fuera()
        {
            var r = new MatchReferee(Settings(), 0, new[] { "a", "b", "c" });
            r.Join("a");
            r.Join("b");
            r.Join("c");
            r.Leave("c", 1);
            r.BeginCountdown(60);
            Assert.Equal(new[] { "a", "b" }, r.Players.Select(p => p.Id));
        }
    }
}
