use anchor_lang::prelude::*;

#[error_code]
pub enum GameError {
    #[msg("La comisión supera el máximo permitido")]
    FeeTooHigh,
    #[msg("El tiempo para liquidar debe ser mayor a cero")]
    InvalidTimeout,
    #[msg("La entrada debe ser mayor a cero")]
    InvalidEntryFee,
    #[msg("Número de jugadores inválido")]
    InvalidMaxPlayers,
    #[msg("El juego está en pausa")]
    Paused,
    #[msg("La partida no está en el estado requerido")]
    InvalidState,
    #[msg("La sala está llena")]
    MatchFull,
    #[msg("El jugador ya está en la partida")]
    AlreadyJoined,
    #[msg("El jugador no está en la partida")]
    NotAPlayer,
    #[msg("No hay suficientes jugadores para empezar")]
    NotEnoughPlayers,
    #[msg("Solo el servidor puede cancelar antes del tiempo límite")]
    CancelNotAllowed,
    #[msg("Aún hay jugadores con reembolso pendiente")]
    RefundsPending,
}
