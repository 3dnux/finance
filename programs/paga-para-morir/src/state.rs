use anchor_lang::prelude::*;

/// Máximo de jugadores por partida.
pub const MAX_PLAYERS: u8 = 100;
/// Mínimo de jugadores para que una partida pueda empezar.
pub const MIN_PLAYERS: u8 = 2;
/// Tope de comisión de la casa (25%), en puntos básicos.
pub const MAX_FEE_BPS: u16 = 2_500;
pub const BPS_DENOMINATOR: u64 = 10_000;

pub const CONFIG_SEED: &[u8] = b"config";
pub const MATCH_SEED: &[u8] = b"match";
pub const VAULT_SEED: &[u8] = b"vault";

/// Configuración global del juego. Una sola cuenta (PDA `["config"]`).
#[account]
#[derive(InitSpace)]
pub struct Config {
    /// Puede cambiar la configuración. Debe ser la upgrade authority del programa.
    pub admin: Pubkey,
    /// Clave del servidor del juego: crea, inicia, liquida y cancela partidas.
    pub authority: Pubkey,
    /// Cuenta de tokens USDC que recibe la comisión de la casa.
    pub treasury: Pubkey,
    /// Mint de USDC aceptado. No se puede cambiar después de inicializar.
    pub usdc_mint: Pubkey,
    /// Comisión de la casa en puntos básicos (2_000 = 20%).
    pub fee_bps: u16,
    /// Segundos que tiene el servidor para liquidar una partida iniciada.
    /// Pasado ese tiempo, cualquiera puede cancelarla y los jugadores recuperan su dinero.
    pub settle_timeout_secs: i64,
    /// Si es `true` no se pueden crear partidas nuevas.
    pub paused: bool,
    pub bump: u8,
}

#[derive(AnchorSerialize, AnchorDeserialize, Clone, Copy, PartialEq, Eq, Debug, InitSpace)]
pub enum MatchState {
    /// Sala abierta: los jugadores pueden entrar y salir.
    Open,
    /// Partida en juego: nadie entra ni sale, el pozo está bloqueado.
    InProgress,
    /// El ganador cobró.
    Settled,
    /// Partida cancelada: cada jugador puede reclamar su entrada.
    Cancelled,
}

/// Una partida. PDA `["match", match_id (u64 LE)]`.
#[account]
pub struct Match {
    pub match_id: u64,
    /// Entrada por jugador en unidades mínimas de USDC (6 decimales: 1 USDC = 1_000_000).
    pub entry_fee: u64,
    pub max_players: u8,
    pub state: MatchState,
    pub created_at: i64,
    pub started_at: i64,
    /// `Pubkey::default()` hasta que se liquida.
    pub winner: Pubkey,
    pub bump: u8,
    pub vault_bump: u8,
    /// Jugadores que pagaron su entrada. En una partida cancelada,
    /// cada jugador sale de la lista al reclamar su reembolso.
    pub players: Vec<Pubkey>,
}

impl Match {
    pub fn space(max_players: u8) -> usize {
        8 // discriminador
            + 8 // match_id
            + 8 // entry_fee
            + 1 // max_players
            + MatchState::INIT_SPACE
            + 8 // created_at
            + 8 // started_at
            + 32 // winner
            + 1 // bump
            + 1 // vault_bump
            + 4 + 32 * max_players as usize // players
    }

    pub fn player_index(&self, player: &Pubkey) -> Option<usize> {
        self.players.iter().position(|p| p == player)
    }
}

/// Divide el pozo en (premio, comisión). La comisión se redondea hacia abajo.
pub fn split_pot(pot: u64, fee_bps: u16) -> (u64, u64) {
    let fee = (pot as u128 * fee_bps as u128 / BPS_DENOMINATOR as u128) as u64;
    (pot - fee, fee)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn split_pot_20_percent() {
        // 10 jugadores x 5 USDC = 50 USDC → 40 al ganador, 10 a la casa.
        assert_eq!(split_pot(50_000_000, 2_000), (40_000_000, 10_000_000));
    }

    #[test]
    fn split_pot_rounds_fee_down() {
        assert_eq!(split_pot(7, 2_000), (6, 1));
        assert_eq!(split_pot(4, 2_000), (4, 0));
    }

    #[test]
    fn split_pot_no_overflow() {
        let (prize, fee) = split_pot(u64::MAX, MAX_FEE_BPS);
        assert_eq!(prize + fee, u64::MAX);
    }
}
