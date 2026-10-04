using System;
using System.Collections.Generic;

namespace PagaParaMorir.Rules
{
    public enum WeaponId : byte
    {
        Pistola = 0,
        Rifle = 1,
        Escopeta = 2,
        Francotirador = 3,
    }

    /// <summary>Estadísticas de un arma. Todo el daño se calcula en el servidor.</summary>
    public sealed class WeaponDef
    {
        public WeaponId Id { get; }
        public string Name { get; }
        /// <summary>Daño por proyectil (la escopeta dispara varios).</summary>
        public int Damage { get; }
        public int Pellets { get; }
        /// <summary>Desviación máxima de cada proyectil, en grados.</summary>
        public float SpreadDegrees { get; }
        /// <summary>Segundos mínimos entre disparos.</summary>
        public double FireInterval { get; }
        public int MagazineSize { get; }
        public double ReloadSeconds { get; }
        /// <summary>Alcance en metros.</summary>
        public float Range { get; }
        /// <summary>Si es automática, mantener el gatillo sigue disparando.</summary>
        public bool Automatic { get; }

        public WeaponDef(WeaponId id, string name, int damage, int pellets, float spreadDegrees,
            double fireInterval, int magazineSize, double reloadSeconds, float range, bool automatic)
        {
            Id = id;
            Name = name;
            Damage = damage;
            Pellets = pellets;
            SpreadDegrees = spreadDegrees;
            FireInterval = fireInterval;
            MagazineSize = magazineSize;
            ReloadSeconds = reloadSeconds;
            Range = range;
            Automatic = automatic;
        }

        /// <summary>Daño máximo de un disparo si pegan todos los proyectiles.</summary>
        public int MaxDamagePerShot => Damage * Pellets;
    }

    public static class Weapons
    {
        public static readonly WeaponDef Pistola =
            new WeaponDef(WeaponId.Pistola, "Pistola", 20, 1, 1.0f, 0.25, 12, 1.2, 60f, false);
        public static readonly WeaponDef Rifle =
            new WeaponDef(WeaponId.Rifle, "Rifle", 14, 1, 2.0f, 0.1, 30, 2.0, 80f, true);
        public static readonly WeaponDef Escopeta =
            new WeaponDef(WeaponId.Escopeta, "Escopeta", 10, 8, 7.0f, 0.9, 6, 2.5, 20f, false);
        public static readonly WeaponDef Francotirador =
            new WeaponDef(WeaponId.Francotirador, "Francotirador", 85, 1, 0.1f, 1.5, 4, 3.0, 200f, false);

        public static readonly IReadOnlyList<WeaponDef> All = new[] { Pistola, Rifle, Escopeta, Francotirador };

        public static WeaponDef Get(WeaponId id)
        {
            foreach (var w in All)
                if (w.Id == id) return w;
            throw new ArgumentOutOfRangeException(nameof(id));
        }
    }

    /// <summary>Munición, cadencia y recarga de un arma de un jugador. Munición de reserva infinita.</summary>
    public sealed class WeaponState
    {
        public WeaponDef Def { get; }
        public int Ammo { get; private set; }
        public bool Reloading => _reloadEndsAt.HasValue;

        private double _nextFireAt;
        private double? _reloadEndsAt;

        public WeaponState(WeaponDef def)
        {
            Def = def;
            Ammo = def.MagazineSize;
        }

        /// <summary>Termina la recarga si ya pasó su tiempo. Llamar antes de leer el estado.</summary>
        public void Update(double now)
        {
            if (_reloadEndsAt.HasValue && now >= _reloadEndsAt.Value)
            {
                Ammo = Def.MagazineSize;
                _reloadEndsAt = null;
            }
        }

        /// <summary>Intenta disparar. Sin balas inicia la recarga sola.</summary>
        public bool TryFire(double now)
        {
            Update(now);
            if (Reloading || now < _nextFireAt) return false;
            if (Ammo <= 0)
            {
                StartReload(now);
                return false;
            }
            Ammo--;
            _nextFireAt = now + Def.FireInterval;
            if (Ammo == 0) StartReload(now);
            return true;
        }

        public void StartReload(double now)
        {
            Update(now);
            if (Reloading || Ammo == Def.MagazineSize) return;
            _reloadEndsAt = now + Def.ReloadSeconds;
        }

        /// <summary>Cambiar de arma cancela la recarga en curso.</summary>
        public void CancelReload() => _reloadEndsAt = null;
    }
}
