//! Paga para Morir — escrow de USDC para partidas donde el ganador se lleva el pozo.
//!
//! Flujo de una partida:
//! 1. El servidor (`config.authority`) crea la partida con `create_match`.
//! 2. Los jugadores depositan su entrada con `join_match` (y pueden salir con
//!    `leave_match` mientras la sala siga abierta).
//! 3. El servidor bloquea la sala con `start_match`.
//! 4. El servidor reporta al ganador con `settle_match`: el ganador recibe el pozo
//!    menos la comisión de la casa, que va a `config.treasury`.
//! 5. Si algo falla, `cancel_match` y cada jugador recupera su entrada con
//!    `claim_refund`. Si el servidor no liquida a tiempo, cualquiera puede cancelar.
//! 6. El servidor recupera la renta con `close_match`.

use anchor_lang::prelude::*;
use anchor_spl::token::{self, CloseAccount, Mint, Token, TokenAccount, TransferChecked};

pub mod error;
pub mod state;

use error::GameError;
use state::*;

declare_id!("2NB9Xwtj7BRFkZEvDWWBhuqTWK18ASTogRt1SCGKZGhg");

#[program]
pub mod paga_para_morir {
    use super::*;

    /// Inicializa la configuración global. Solo la upgrade authority del programa
    /// puede llamarla, para que nadie se adelante después del deploy.
    pub fn initialize_config(
        ctx: Context<InitializeConfig>,
        authority: Pubkey,
        fee_bps: u16,
        settle_timeout_secs: i64,
    ) -> Result<()> {
        require!(fee_bps <= MAX_FEE_BPS, GameError::FeeTooHigh);
        require!(settle_timeout_secs > 0, GameError::InvalidTimeout);

        let config = &mut ctx.accounts.config;
        config.admin = ctx.accounts.admin.key();
        config.authority = authority;
        config.treasury = ctx.accounts.treasury.key();
        config.usdc_mint = ctx.accounts.usdc_mint.key();
        config.fee_bps = fee_bps;
        config.settle_timeout_secs = settle_timeout_secs;
        config.paused = false;
        config.bump = ctx.bumps.config;
        Ok(())
    }

    /// Cambia la configuración. Los campos en `None` no se modifican.
    pub fn update_config(ctx: Context<UpdateConfig>, update: ConfigUpdate) -> Result<()> {
        let config = &mut ctx.accounts.config;
        if let Some(admin) = update.admin {
            config.admin = admin;
        }
        if let Some(authority) = update.authority {
            config.authority = authority;
        }
        if let Some(fee_bps) = update.fee_bps {
            require!(fee_bps <= MAX_FEE_BPS, GameError::FeeTooHigh);
            config.fee_bps = fee_bps;
        }
        if let Some(timeout) = update.settle_timeout_secs {
            require!(timeout > 0, GameError::InvalidTimeout);
            config.settle_timeout_secs = timeout;
        }
        if let Some(paused) = update.paused {
            config.paused = paused;
        }
        if let Some(treasury) = &ctx.accounts.new_treasury {
            config.treasury = treasury.key();
        }
        Ok(())
    }

    /// El servidor abre una sala nueva con su entrada y cupo.
    pub fn create_match(
        ctx: Context<CreateMatch>,
        match_id: u64,
        entry_fee: u64,
        max_players: u8,
    ) -> Result<()> {
        require!(!ctx.accounts.config.paused, GameError::Paused);
        require!(entry_fee > 0, GameError::InvalidEntryFee);
        require!(
            (MIN_PLAYERS..=MAX_PLAYERS).contains(&max_players),
            GameError::InvalidMaxPlayers
        );

        let now = Clock::get()?.unix_timestamp;
        let m = &mut ctx.accounts.match_account;
        m.match_id = match_id;
        m.entry_fee = entry_fee;
        m.max_players = max_players;
        m.state = MatchState::Open;
        m.created_at = now;
        m.started_at = 0;
        m.winner = Pubkey::default();
        m.bump = ctx.bumps.match_account;
        m.vault_bump = ctx.bumps.vault;
        m.players = Vec::with_capacity(max_players as usize);

        emit!(MatchCreated {
            match_id,
            entry_fee,
            max_players
        });
        Ok(())
    }

    /// El jugador paga su entrada y entra a la sala.
    pub fn join_match(ctx: Context<PlayerEscrow>) -> Result<()> {
        let m = &ctx.accounts.match_account;
        let player = ctx.accounts.player.key();
        require!(m.state == MatchState::Open, GameError::InvalidState);
        require!(
            m.players.len() < m.max_players as usize,
            GameError::MatchFull
        );
        require!(m.player_index(&player).is_none(), GameError::AlreadyJoined);

        token::transfer_checked(
            CpiContext::new(
                ctx.accounts.token_program.key(),
                TransferChecked {
                    from: ctx.accounts.player_token.to_account_info(),
                    mint: ctx.accounts.usdc_mint.to_account_info(),
                    to: ctx.accounts.vault.to_account_info(),
                    authority: ctx.accounts.player.to_account_info(),
                },
            ),
            m.entry_fee,
            ctx.accounts.usdc_mint.decimals,
        )?;

        let m = &mut ctx.accounts.match_account;
        m.players.push(player);
        emit!(PlayerJoined {
            match_id: m.match_id,
            player,
            players: m.players.len() as u8,
        });
        Ok(())
    }

    /// El jugador sale de una sala que aún no empezó y recupera su entrada.
    pub fn leave_match(ctx: Context<PlayerEscrow>) -> Result<()> {
        require!(
            ctx.accounts.match_account.state == MatchState::Open,
            GameError::InvalidState
        );
        refund_player(ctx)
    }

    /// El servidor bloquea la sala y empieza la partida.
    pub fn start_match(ctx: Context<AuthorityMatchAction>) -> Result<()> {
        let m = &mut ctx.accounts.match_account;
        require!(m.state == MatchState::Open, GameError::InvalidState);
        require!(
            m.players.len() >= MIN_PLAYERS as usize,
            GameError::NotEnoughPlayers
        );
        m.state = MatchState::InProgress;
        m.started_at = Clock::get()?.unix_timestamp;
        emit!(MatchStarted {
            match_id: m.match_id,
            players: m.players.len() as u8,
        });
        Ok(())
    }

    /// El servidor reporta al ganador. El ganador recibe el pozo menos la comisión.
    pub fn settle_match(ctx: Context<SettleMatch>, winner: Pubkey) -> Result<()> {
        let m = &ctx.accounts.match_account;
        require!(m.state == MatchState::InProgress, GameError::InvalidState);
        require!(m.player_index(&winner).is_some(), GameError::NotAPlayer);

        let pot = ctx.accounts.vault.amount;
        let (prize, fee) = split_pot(pot, ctx.accounts.config.fee_bps);

        vault_transfer(
            &ctx.accounts.token_program,
            &ctx.accounts.vault,
            &ctx.accounts.usdc_mint,
            &ctx.accounts.match_account,
            ctx.accounts.winner_token.to_account_info(),
            prize,
        )?;
        vault_transfer(
            &ctx.accounts.token_program,
            &ctx.accounts.vault,
            &ctx.accounts.usdc_mint,
            &ctx.accounts.match_account,
            ctx.accounts.treasury.to_account_info(),
            fee,
        )?;

        let m = &mut ctx.accounts.match_account;
        m.state = MatchState::Settled;
        m.winner = winner;
        emit!(MatchSettled {
            match_id: m.match_id,
            winner,
            prize,
            fee,
        });
        Ok(())
    }

    /// Cancela la partida para que cada jugador recupere su entrada.
    /// El servidor puede cancelar en cualquier momento antes de liquidar;
    /// cualquier otra persona solo si el servidor no liquidó a tiempo.
    pub fn cancel_match(ctx: Context<CancelMatch>) -> Result<()> {
        let config = &ctx.accounts.config;
        let m = &mut ctx.accounts.match_account;
        let is_authority = ctx.accounts.caller.key() == config.authority;

        match m.state {
            MatchState::Open | MatchState::InProgress if is_authority => {}
            MatchState::InProgress => {
                let now = Clock::get()?.unix_timestamp;
                let deadline = m.started_at.saturating_add(config.settle_timeout_secs);
                require!(now >= deadline, GameError::CancelNotAllowed);
            }
            MatchState::Open => return err!(GameError::CancelNotAllowed),
            _ => return err!(GameError::InvalidState),
        }

        m.state = MatchState::Cancelled;
        emit!(MatchCancelled {
            match_id: m.match_id,
            by: ctx.accounts.caller.key(),
        });
        Ok(())
    }

    /// El jugador recupera su entrada de una partida cancelada.
    pub fn claim_refund(ctx: Context<PlayerEscrow>) -> Result<()> {
        require!(
            ctx.accounts.match_account.state == MatchState::Cancelled,
            GameError::InvalidState
        );
        refund_player(ctx)
    }

    /// El servidor cierra una partida terminada y recupera la renta.
    /// Cualquier saldo sobrante en el vault (por ejemplo, tokens enviados por
    /// error) se manda a la tesorería.
    pub fn close_match(ctx: Context<CloseMatch>) -> Result<()> {
        let m = &ctx.accounts.match_account;
        match m.state {
            MatchState::Settled => {}
            MatchState::Cancelled => require!(m.players.is_empty(), GameError::RefundsPending),
            _ => return err!(GameError::InvalidState),
        }

        let leftover = ctx.accounts.vault.amount;
        if leftover > 0 {
            vault_transfer(
                &ctx.accounts.token_program,
                &ctx.accounts.vault,
                &ctx.accounts.usdc_mint,
                &ctx.accounts.match_account,
                ctx.accounts.treasury.to_account_info(),
                leftover,
            )?;
        }

        let match_id_bytes = m.match_id.to_le_bytes();
        let seeds: &[&[u8]] = &[MATCH_SEED, &match_id_bytes, &[m.bump]];
        token::close_account(CpiContext::new_with_signer(
            ctx.accounts.token_program.key(),
            CloseAccount {
                account: ctx.accounts.vault.to_account_info(),
                destination: ctx.accounts.authority.to_account_info(),
                authority: ctx.accounts.match_account.to_account_info(),
            },
            &[seeds],
        ))?;
        Ok(())
    }
}

/// Devuelve la entrada al jugador y lo saca de la lista.
fn refund_player(ctx: Context<PlayerEscrow>) -> Result<()> {
    let player = ctx.accounts.player.key();
    let index = ctx
        .accounts
        .match_account
        .player_index(&player)
        .ok_or(GameError::NotAPlayer)?;

    vault_transfer(
        &ctx.accounts.token_program,
        &ctx.accounts.vault,
        &ctx.accounts.usdc_mint,
        &ctx.accounts.match_account,
        ctx.accounts.player_token.to_account_info(),
        ctx.accounts.match_account.entry_fee,
    )?;

    let m = &mut ctx.accounts.match_account;
    m.players.swap_remove(index);
    emit!(PlayerRefunded {
        match_id: m.match_id,
        player,
        amount: m.entry_fee,
    });
    Ok(())
}

/// Transfiere USDC desde el vault de la partida, firmando con la PDA de la partida.
fn vault_transfer<'info>(
    token_program: &Program<'info, Token>,
    vault: &Account<'info, TokenAccount>,
    mint: &Account<'info, Mint>,
    match_account: &Account<'info, Match>,
    to: AccountInfo<'info>,
    amount: u64,
) -> Result<()> {
    if amount == 0 {
        return Ok(());
    }
    let match_id_bytes = match_account.match_id.to_le_bytes();
    let seeds: &[&[u8]] = &[MATCH_SEED, &match_id_bytes, &[match_account.bump]];
    token::transfer_checked(
        CpiContext::new_with_signer(
            token_program.key(),
            TransferChecked {
                from: vault.to_account_info(),
                mint: mint.to_account_info(),
                to,
                authority: match_account.to_account_info(),
            },
            &[seeds],
        ),
        amount,
        mint.decimals,
    )
}

#[derive(AnchorSerialize, AnchorDeserialize, Clone, Default)]
pub struct ConfigUpdate {
    pub admin: Option<Pubkey>,
    pub authority: Option<Pubkey>,
    pub fee_bps: Option<u16>,
    pub settle_timeout_secs: Option<i64>,
    pub paused: Option<bool>,
}

#[derive(Accounts)]
pub struct InitializeConfig<'info> {
    #[account(mut)]
    pub admin: Signer<'info>,
    #[account(
        init,
        payer = admin,
        space = 8 + Config::INIT_SPACE,
        seeds = [CONFIG_SEED],
        bump,
    )]
    pub config: Account<'info, Config>,
    pub usdc_mint: Account<'info, Mint>,
    #[account(token::mint = usdc_mint)]
    pub treasury: Account<'info, TokenAccount>,
    #[account(constraint = program.programdata_address()? == Some(program_data.key()))]
    pub program: Program<'info, crate::program::PagaParaMorir>,
    #[account(constraint = program_data.upgrade_authority_address == Some(admin.key()))]
    pub program_data: Account<'info, ProgramData>,
    pub system_program: Program<'info, System>,
}

#[derive(Accounts)]
pub struct UpdateConfig<'info> {
    pub admin: Signer<'info>,
    #[account(mut, seeds = [CONFIG_SEED], bump = config.bump, has_one = admin)]
    pub config: Account<'info, Config>,
    #[account(constraint = new_treasury.mint == config.usdc_mint)]
    pub new_treasury: Option<Account<'info, TokenAccount>>,
}

#[derive(Accounts)]
#[instruction(match_id: u64, entry_fee: u64, max_players: u8)]
pub struct CreateMatch<'info> {
    #[account(mut)]
    pub authority: Signer<'info>,
    #[account(seeds = [CONFIG_SEED], bump = config.bump, has_one = authority, has_one = usdc_mint)]
    pub config: Account<'info, Config>,
    #[account(
        init,
        payer = authority,
        space = Match::space(max_players),
        seeds = [MATCH_SEED, match_id.to_le_bytes().as_ref()],
        bump,
    )]
    pub match_account: Account<'info, Match>,
    #[account(
        init,
        payer = authority,
        seeds = [VAULT_SEED, match_account.key().as_ref()],
        bump,
        token::mint = usdc_mint,
        token::authority = match_account,
    )]
    pub vault: Account<'info, TokenAccount>,
    pub usdc_mint: Account<'info, Mint>,
    pub token_program: Program<'info, Token>,
    pub system_program: Program<'info, System>,
}

/// Cuentas para que un jugador entre, salga o reclame su reembolso.
#[derive(Accounts)]
pub struct PlayerEscrow<'info> {
    pub player: Signer<'info>,
    #[account(seeds = [CONFIG_SEED], bump = config.bump, has_one = usdc_mint)]
    pub config: Account<'info, Config>,
    #[account(
        mut,
        seeds = [MATCH_SEED, match_account.match_id.to_le_bytes().as_ref()],
        bump = match_account.bump,
    )]
    pub match_account: Account<'info, Match>,
    #[account(
        mut,
        seeds = [VAULT_SEED, match_account.key().as_ref()],
        bump = match_account.vault_bump,
    )]
    pub vault: Account<'info, TokenAccount>,
    #[account(mut, token::mint = usdc_mint, token::authority = player)]
    pub player_token: Account<'info, TokenAccount>,
    pub usdc_mint: Account<'info, Mint>,
    pub token_program: Program<'info, Token>,
}

#[derive(Accounts)]
pub struct AuthorityMatchAction<'info> {
    pub authority: Signer<'info>,
    #[account(seeds = [CONFIG_SEED], bump = config.bump, has_one = authority)]
    pub config: Account<'info, Config>,
    #[account(
        mut,
        seeds = [MATCH_SEED, match_account.match_id.to_le_bytes().as_ref()],
        bump = match_account.bump,
    )]
    pub match_account: Account<'info, Match>,
}

#[derive(Accounts)]
#[instruction(winner: Pubkey)]
pub struct SettleMatch<'info> {
    pub authority: Signer<'info>,
    #[account(
        seeds = [CONFIG_SEED],
        bump = config.bump,
        has_one = authority,
        has_one = treasury,
        has_one = usdc_mint,
    )]
    pub config: Account<'info, Config>,
    #[account(
        mut,
        seeds = [MATCH_SEED, match_account.match_id.to_le_bytes().as_ref()],
        bump = match_account.bump,
    )]
    pub match_account: Account<'info, Match>,
    #[account(
        mut,
        seeds = [VAULT_SEED, match_account.key().as_ref()],
        bump = match_account.vault_bump,
    )]
    pub vault: Account<'info, TokenAccount>,
    #[account(mut, token::mint = usdc_mint, token::authority = winner)]
    pub winner_token: Account<'info, TokenAccount>,
    #[account(mut)]
    pub treasury: Account<'info, TokenAccount>,
    pub usdc_mint: Account<'info, Mint>,
    pub token_program: Program<'info, Token>,
}

#[derive(Accounts)]
pub struct CancelMatch<'info> {
    pub caller: Signer<'info>,
    #[account(seeds = [CONFIG_SEED], bump = config.bump)]
    pub config: Account<'info, Config>,
    #[account(
        mut,
        seeds = [MATCH_SEED, match_account.match_id.to_le_bytes().as_ref()],
        bump = match_account.bump,
    )]
    pub match_account: Account<'info, Match>,
}

#[derive(Accounts)]
pub struct CloseMatch<'info> {
    #[account(mut)]
    pub authority: Signer<'info>,
    #[account(
        seeds = [CONFIG_SEED],
        bump = config.bump,
        has_one = authority,
        has_one = treasury,
        has_one = usdc_mint,
    )]
    pub config: Account<'info, Config>,
    #[account(
        mut,
        close = authority,
        seeds = [MATCH_SEED, match_account.match_id.to_le_bytes().as_ref()],
        bump = match_account.bump,
    )]
    pub match_account: Account<'info, Match>,
    #[account(
        mut,
        seeds = [VAULT_SEED, match_account.key().as_ref()],
        bump = match_account.vault_bump,
    )]
    pub vault: Account<'info, TokenAccount>,
    #[account(mut)]
    pub treasury: Account<'info, TokenAccount>,
    pub usdc_mint: Account<'info, Mint>,
    pub token_program: Program<'info, Token>,
}

#[event]
pub struct MatchCreated {
    pub match_id: u64,
    pub entry_fee: u64,
    pub max_players: u8,
}

#[event]
pub struct PlayerJoined {
    pub match_id: u64,
    pub player: Pubkey,
    pub players: u8,
}

#[event]
pub struct PlayerRefunded {
    pub match_id: u64,
    pub player: Pubkey,
    pub amount: u64,
}

#[event]
pub struct MatchStarted {
    pub match_id: u64,
    pub players: u8,
}

#[event]
pub struct MatchSettled {
    pub match_id: u64,
    pub winner: Pubkey,
    pub prize: u64,
    pub fee: u64,
}

#[event]
pub struct MatchCancelled {
    pub match_id: u64,
    pub by: Pubkey,
}
