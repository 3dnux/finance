using PagaParaMorir.Rules;
using Xunit;

namespace PagaParaMorir.Rules.Tests
{
    public class WeaponAndZoneTests
    {
        [Fact]
        public void Respeta_la_cadencia_de_disparo()
        {
            var w = new WeaponState(Weapons.Pistola);
            Assert.True(w.TryFire(0));
            Assert.False(w.TryFire(0.2));
            Assert.True(w.TryFire(0.25));
            Assert.Equal(10, w.Ammo);
        }

        [Fact]
        public void Recarga_sola_al_vaciar_el_cargador()
        {
            var w = new WeaponState(Weapons.Francotirador);
            var t = 0.0;
            for (var i = 0; i < 4; i++, t += 1.5) Assert.True(w.TryFire(t));
            Assert.Equal(0, w.Ammo);
            Assert.True(w.Reloading);
            Assert.False(w.TryFire(t));
            // La recarga empezó con el último disparo (t = 4.5) y dura 3 s.
            w.Update(4.5 + 3.0);
            Assert.False(w.Reloading);
            Assert.Equal(4, w.Ammo);
        }

        [Fact]
        public void No_recarga_con_el_cargador_lleno()
        {
            var w = new WeaponState(Weapons.Rifle);
            w.StartReload(0);
            Assert.False(w.Reloading);
        }

        [Fact]
        public void Escopeta_hace_mas_dano_de_cerca_que_la_pistola()
        {
            Assert.Equal(80, Weapons.Escopeta.MaxDamagePerShot);
            Assert.True(Weapons.Escopeta.MaxDamagePerShot > Weapons.Pistola.MaxDamagePerShot);
            Assert.True(Weapons.Escopeta.Range < Weapons.Pistola.Range);
        }

        [Fact]
        public void Francotirador_no_mata_de_un_tiro()
        {
            // 100 de vida: hacen falta dos impactos de cualquier arma.
            foreach (var w in Weapons.All) Assert.True(w.MaxDamagePerShot < 100, w.Name);
        }

        [Fact]
        public void La_zona_se_cierra_por_fases()
        {
            var z = ZoneSchedule.Default();
            Assert.Equal(60f, z.RadiusAt(0));
            Assert.Equal(60f, z.RadiusAt(29.9));
            Assert.Equal(50f, z.RadiusAt(45), 3);      // mitad de la primera contracción
            Assert.Equal(40f, z.RadiusAt(60));
            Assert.Equal(0f, z.RadiusAt(z.TotalSeconds));
            Assert.Equal(0f, z.RadiusAt(10_000));
            Assert.Equal(2f, z.DamagePerSecondAt(10));
            Assert.Equal(12f, z.DamagePerSecondAt(10_000));
        }

        [Fact]
        public void La_zona_termina_antes_del_limite_de_tiempo()
        {
            Assert.True(ZoneSchedule.Default().TotalSeconds < new RefereeSettings().TimeLimitSeconds);
        }
    }
}
