//! Pruebas de integración del escrow sobre LiteSVM.
//!
//! Requieren el programa compilado: `cargo build-sbf` antes de `cargo test`.

use anchor_lang::{AccountDeserialize, InstructionData, ToAccountMetas};
use litesvm::LiteSVM;
use paga_para_morir::state::{Config, Match, MatchState};
use paga_para_morir::{accounts, instruction, ConfigUpdate};
use solana_account::Account;
use solana_address::Address;
use solana_clock::Clock;
use solana_instruction::{AccountMeta, Instruction};
use solana_keypair::Keypair;
use solana_message::Message;
use solana_signer::Signer;
use solana_transaction::Transaction;

type Pubkey = anchor_lang::prelude::Pubkey;

const USDC: u64 = 1_000_000; // 6 decimales
const ENTRY: u64 = 5 * USDC;
const FEE_BPS: u16 = 2_000; // 20%
const TIMEOUT: i64 = 3_600;
const START_TIME: i64 = 1_800_000_000;

fn addr(p: &Pubkey) -> Address {
    Address::new_from_array(p.to_bytes())
}

fn pk(a: &Address) -> Pubkey {
    Pubkey::new_from_array(a.to_bytes())
}

fn program_id() -> Pubkey {
    paga_para_morir::ID
}

fn token_program() -> Pubkey {
    anchor_spl::token::ID
}

fn system_program() -> Pubkey {
    anchor_lang::system_program::ID
}

fn config_pda() -> Pubkey {
    Pubkey::find_program_address(&[b"config"], &program_id()).0
}

fn match_pda(match_id: u64) -> Pubkey {
    Pubkey::find_program_address(&[b"match", &match_id.to_le_bytes()], &program_id()).0
}

fn vault_pda(match_account: &Pubkey) -> Pubkey {
    Pubkey::find_program_address(&[b"vault", match_account.as_ref()], &program_id()).0
}

struct Game {
    svm: LiteSVM,
    admin: Keypair,
    server: Keypair,
    mint: Pubkey,
    treasury: Pubkey,
}

struct Player {
    kp: Keypair,
    token: Pubkey,
}

impl Player {
    fn key(&self) -> Pubkey {
        pk(&self.kp.pubkey())
    }
}

impl Game {
    fn new() -> Self {
        let mut svm = LiteSVM::new();
        let so = concat!(
            env!("CARGO_MANIFEST_DIR"),
            "/../../target/deploy/paga_para_morir.so"
        );
        svm.add_program_from_file(addr(&program_id()), so)
            .expect("compila primero con `cargo build-sbf`");
        set_time(&mut svm, START_TIME);

        let admin = Keypair::new();
        let server = Keypair::new();
        svm.airdrop(&admin.pubkey(), 10_000_000_000).unwrap();
        svm.airdrop(&server.pubkey(), 10_000_000_000).unwrap();
        set_upgrade_authority(&mut svm, &pk(&admin.pubkey()));

        let mint = Pubkey::new_unique();
        set_mint(&mut svm, &mint);
        let treasury = Pubkey::new_unique();
        set_token_account(&mut svm, &treasury, &mint, &pk(&admin.pubkey()), 0);

        Game {
            svm,
            admin,
            server,
            mint,
            treasury,
        }
    }

    fn with_config() -> Self {
        let mut g = Self::new();
        let admin = g.admin.insecure_clone();
        g.initialize_config(&admin, FEE_BPS).unwrap();
        g
    }

    fn send(&mut self, ix: Instruction, signers: &[&Keypair]) -> Result<(), String> {
        self.svm.expire_blockhash();
        let payer = signers[0].pubkey();
        let msg = Message::new(&[ix], Some(&payer));
        let tx = Transaction::new(signers, msg, self.svm.latest_blockhash());
        self.svm
            .send_transaction(tx)
            .map(|_| ())
            .map_err(|e| format!("{:?}\n{}", e.err, e.meta.logs.join("\n")))
    }

    fn ix(&self, metas: Vec<anchor_lang::prelude::AccountMeta>, data: Vec<u8>) -> Instruction {
        Instruction {
            program_id: addr(&program_id()),
            accounts: metas
                .into_iter()
                .map(|m| AccountMeta {
                    pubkey: addr(&m.pubkey),
                    is_signer: m.is_signer,
                    is_writable: m.is_writable,
                })
                .collect(),
            data,
        }
    }

    fn initialize_config(&mut self, signer: &Keypair, fee_bps: u16) -> Result<(), String> {
        let metas = accounts::InitializeConfig {
            admin: pk(&signer.pubkey()),
            config: config_pda(),
            usdc_mint: self.mint,
            treasury: self.treasury,
            program: program_id(),
            program_data: programdata_address(),
            system_program: system_program(),
        }
        .to_account_metas(None);
        let data = instruction::InitializeConfig {
            authority: pk(&self.server.pubkey()),
            fee_bps,
            settle_timeout_secs: TIMEOUT,
        }
        .data();
        let ix = self.ix(metas, data);
        self.send(ix, &[signer])
    }

    fn update_config(&mut self, update: ConfigUpdate) -> Result<(), String> {
        let metas = accounts::UpdateConfig {
            admin: pk(&self.admin.pubkey()),
            config: config_pda(),
            new_treasury: None,
        }
        .to_account_metas(None);
        let ix = self.ix(metas, instruction::UpdateConfig { update }.data());
        let admin = self.admin.insecure_clone();
        self.send(ix, &[&admin])
    }

    fn create_match(&mut self, match_id: u64, max_players: u8) -> Result<(), String> {
        let match_account = match_pda(match_id);
        let metas = accounts::CreateMatch {
            authority: pk(&self.server.pubkey()),
            config: config_pda(),
            match_account,
            vault: vault_pda(&match_account),
            usdc_mint: self.mint,
            token_program: token_program(),
            system_program: system_program(),
        }
        .to_account_metas(None);
        let data = instruction::CreateMatch {
            match_id,
            entry_fee: ENTRY,
            max_players,
        }
        .data();
        let ix = self.ix(metas, data);
        let server = self.server.insecure_clone();
        self.send(ix, &[&server])
    }

    fn new_player(&mut self, balance: u64) -> Player {
        let kp = Keypair::new();
        self.svm.airdrop(&kp.pubkey(), 1_000_000_000).unwrap();
        let token = Pubkey::new_unique();
        set_token_account(
            &mut self.svm,
            &token,
            &self.mint,
            &pk(&kp.pubkey()),
            balance,
        );
        Player { kp, token }
    }

    fn player_ix(&self, player: &Player, match_id: u64, data: Vec<u8>) -> Instruction {
        let match_account = match_pda(match_id);
        let metas = accounts::PlayerEscrow {
            player: player.key(),
            config: config_pda(),
            match_account,
            vault: vault_pda(&match_account),
            player_token: player.token,
            usdc_mint: self.mint,
            token_program: token_program(),
        }
        .to_account_metas(None);
        self.ix(metas, data)
    }

    fn join(&mut self, player: &Player, match_id: u64) -> Result<(), String> {
        let ix = self.player_ix(player, match_id, instruction::JoinMatch {}.data());
        self.send(ix, &[&player.kp])
    }

    fn leave(&mut self, player: &Player, match_id: u64) -> Result<(), String> {
        let ix = self.player_ix(player, match_id, instruction::LeaveMatch {}.data());
        self.send(ix, &[&player.kp])
    }

    fn claim_refund(&mut self, player: &Player, match_id: u64) -> Result<(), String> {
        let ix = self.player_ix(player, match_id, instruction::ClaimRefund {}.data());
        self.send(ix, &[&player.kp])
    }

    fn start(&mut self, match_id: u64) -> Result<(), String> {
        let metas = accounts::AuthorityMatchAction {
            authority: pk(&self.server.pubkey()),
            config: config_pda(),
            match_account: match_pda(match_id),
        }
        .to_account_metas(None);
        let ix = self.ix(metas, instruction::StartMatch {}.data());
        let server = self.server.insecure_clone();
        self.send(ix, &[&server])
    }

    fn settle_with(
        &mut self,
        signer: &Keypair,
        match_id: u64,
        winner: Pubkey,
        winner_token: Pubkey,
    ) -> Result<(), String> {
        let match_account = match_pda(match_id);
        let metas = accounts::SettleMatch {
            authority: pk(&signer.pubkey()),
            config: config_pda(),
            match_account,
            vault: vault_pda(&match_account),
            winner_token,
            treasury: self.treasury,
            usdc_mint: self.mint,
            token_program: token_program(),
        }
        .to_account_metas(None);
        let ix = self.ix(metas, instruction::SettleMatch { winner }.data());
        self.send(ix, &[signer])
    }

    fn settle(&mut self, match_id: u64, winner: &Player) -> Result<(), String> {
        let server = self.server.insecure_clone();
        self.settle_with(&server, match_id, winner.key(), winner.token)
    }

    fn cancel(&mut self, caller: &Keypair, match_id: u64) -> Result<(), String> {
        let metas = accounts::CancelMatch {
            caller: pk(&caller.pubkey()),
            config: config_pda(),
            match_account: match_pda(match_id),
        }
        .to_account_metas(None);
        let ix = self.ix(metas, instruction::CancelMatch {}.data());
        self.send(ix, &[caller])
    }

    fn close(&mut self, match_id: u64) -> Result<(), String> {
        let match_account = match_pda(match_id);
        let metas = accounts::CloseMatch {
            authority: pk(&self.server.pubkey()),
            config: config_pda(),
            match_account,
            vault: vault_pda(&match_account),
            treasury: self.treasury,
            usdc_mint: self.mint,
            token_program: token_program(),
        }
        .to_account_metas(None);
        let ix = self.ix(metas, instruction::CloseMatch {}.data());
        let server = self.server.insecure_clone();
        self.send(ix, &[&server])
    }

    fn balance(&self, token_account: &Pubkey) -> u64 {
        let acc = self
            .svm
            .get_account(&addr(token_account))
            .expect("token account");
        u64::from_le_bytes(acc.data[64..72].try_into().unwrap())
    }

    fn match_state(&self, match_id: u64) -> Match {
        let acc = self
            .svm
            .get_account(&addr(&match_pda(match_id)))
            .expect("match");
        Match::try_deserialize(&mut acc.data.as_slice()).unwrap()
    }

    fn config(&self) -> Config {
        let acc = self.svm.get_account(&addr(&config_pda())).expect("config");
        Config::try_deserialize(&mut acc.data.as_slice()).unwrap()
    }

    fn exists(&self, key: &Pubkey) -> bool {
        self.svm
            .get_account(&addr(key))
            .is_some_and(|a| a.lamports > 0)
    }
}

fn set_time(svm: &mut LiteSVM, unix_timestamp: i64) {
    let mut clock: Clock = svm.get_sysvar();
    clock.unix_timestamp = unix_timestamp;
    svm.set_sysvar(&clock);
}

fn programdata_address() -> Pubkey {
    let loader = anchor_lang::solana_program::bpf_loader_upgradeable::ID;
    Pubkey::find_program_address(&[program_id().as_ref()], &loader).0
}

/// LiteSVM carga el programa sin upgrade authority; la fijamos a mano.
fn set_upgrade_authority(svm: &mut LiteSVM, authority: &Pubkey) {
    let key = addr(&programdata_address());
    let mut acc = svm.get_account(&key).unwrap();
    // Layout de ProgramData: tag u32 | slot u64 | Option<Pubkey>
    acc.data[12] = 1;
    acc.data[13..45].copy_from_slice(authority.as_ref());
    svm.set_account(key, acc).unwrap();
}

fn rent_exempt(svm: &LiteSVM, len: usize) -> u64 {
    svm.minimum_balance_for_rent_exemption(len)
}

fn set_mint(svm: &mut LiteSVM, mint: &Pubkey) {
    let mut data = vec![0u8; 82];
    // mint_authority: None; supply: 0; decimals: 6; is_initialized: true; freeze: None
    data[44] = 6;
    data[45] = 1;
    let lamports = rent_exempt(svm, data.len());
    svm.set_account(
        addr(mint),
        Account {
            lamports,
            data,
            owner: addr(&token_program()),
            executable: false,
            rent_epoch: 0,
        },
    )
    .unwrap();
}

fn set_token_account(svm: &mut LiteSVM, key: &Pubkey, mint: &Pubkey, owner: &Pubkey, amount: u64) {
    let mut data = vec![0u8; 165];
    data[0..32].copy_from_slice(mint.as_ref());
    data[32..64].copy_from_slice(owner.as_ref());
    data[64..72].copy_from_slice(&amount.to_le_bytes());
    data[108] = 1; // AccountState::Initialized
    let lamports = rent_exempt(svm, data.len());
    svm.set_account(
        addr(key),
        Account {
            lamports,
            data,
            owner: addr(&token_program()),
            executable: false,
            rent_epoch: 0,
        },
    )
    .unwrap();
}

fn assert_err(result: Result<(), String>, expected: &str) {
    let err = result.expect_err("se esperaba un error");
    assert!(
        err.contains(expected),
        "se esperaba `{expected}`, se obtuvo:\n{err}"
    );
}

#[test]
fn partida_completa_el_ganador_se_lleva_80_por_ciento() {
    let mut g = Game::with_config();
    let players: Vec<Player> = (0..4).map(|_| g.new_player(10 * USDC)).collect();

    g.create_match(1, 4).unwrap();
    for p in &players {
        g.join(p, 1).unwrap();
    }
    let vault = vault_pda(&match_pda(1));
    assert_eq!(g.balance(&vault), 4 * ENTRY);
    assert_eq!(g.match_state(1).players.len(), 4);

    g.start(1).unwrap();
    assert_eq!(g.match_state(1).state, MatchState::InProgress);

    let server_lamports_before = g.svm.get_balance(&g.server.pubkey()).unwrap();
    g.settle(1, &players[2]).unwrap();

    // Pozo 20 USDC → 16 al ganador, 4 a la casa.
    assert_eq!(g.balance(&players[2].token), 10 * USDC - ENTRY + 16 * USDC);
    assert_eq!(g.balance(&g.treasury), 4 * USDC);
    assert_eq!(g.balance(&players[0].token), 10 * USDC - ENTRY);
    assert_eq!(g.balance(&vault), 0);
    let m = g.match_state(1);
    assert_eq!(m.state, MatchState::Settled);
    assert_eq!(m.winner, players[2].key());

    g.close(1).unwrap();
    assert!(!g.exists(&match_pda(1)));
    assert!(!g.exists(&vault));
    assert!(g.svm.get_balance(&g.server.pubkey()).unwrap() > server_lamports_before);
}

#[test]
fn solo_la_upgrade_authority_inicializa() {
    let mut g = Game::new();
    let impostor = Keypair::new();
    g.svm.airdrop(&impostor.pubkey(), 1_000_000_000).unwrap();
    assert_err(g.initialize_config(&impostor, FEE_BPS), "ConstraintRaw");

    let admin = g.admin.insecure_clone();
    assert_err(g.initialize_config(&admin, 3_000), "FeeTooHigh");
    g.initialize_config(&admin, FEE_BPS).unwrap();

    let c = g.config();
    assert_eq!(c.fee_bps, FEE_BPS);
    assert_eq!(c.authority, pk(&g.server.pubkey()));
    assert_eq!(c.treasury, g.treasury);
}

#[test]
fn reglas_para_entrar_a_la_sala() {
    let mut g = Game::with_config();
    let a = g.new_player(10 * USDC);
    let b = g.new_player(10 * USDC);
    let c = g.new_player(10 * USDC);
    let pobre = g.new_player(USDC);

    g.create_match(7, 2).unwrap();
    assert_err(g.join(&pobre, 7), "insufficient funds");
    g.join(&a, 7).unwrap();
    assert_err(g.join(&a, 7), "AlreadyJoined");
    g.join(&b, 7).unwrap();
    assert_err(g.join(&c, 7), "MatchFull");

    g.start(7).unwrap();
    assert_err(g.leave(&a, 7), "InvalidState");
}

#[test]
fn salir_de_la_sala_devuelve_la_entrada() {
    let mut g = Game::with_config();
    let a = g.new_player(10 * USDC);
    let b = g.new_player(10 * USDC);

    g.create_match(3, 10).unwrap();
    g.join(&a, 3).unwrap();
    g.join(&b, 3).unwrap();
    g.leave(&a, 3).unwrap();

    assert_eq!(g.balance(&a.token), 10 * USDC);
    assert_eq!(g.match_state(3).players, vec![b.key()]);
    assert_err(g.leave(&a, 3), "NotAPlayer");
    assert_err(g.start(3), "NotEnoughPlayers");
}

#[test]
fn liquidar_solo_por_el_servidor_y_a_un_jugador() {
    let mut g = Game::with_config();
    let a = g.new_player(10 * USDC);
    let b = g.new_player(10 * USDC);
    let espectador = g.new_player(0);

    g.create_match(9, 2).unwrap();
    g.join(&a, 9).unwrap();
    g.join(&b, 9).unwrap();
    assert_err(g.settle(9, &a), "InvalidState"); // aún no empieza
    g.start(9).unwrap();

    assert_err(g.settle(9, &espectador), "NotAPlayer");
    // La cuenta de tokens debe pertenecer al ganador.
    let server = g.server.insecure_clone();
    assert_err(
        g.settle_with(&server, 9, a.key(), b.token),
        "ConstraintTokenOwner",
    );
    // Un jugador no puede declararse ganador.
    let a_kp = a.kp.insecure_clone();
    assert_err(
        g.settle_with(&a_kp, 9, a.key(), a.token),
        "ConstraintHasOne",
    );

    g.settle(9, &a).unwrap();
    assert_err(g.settle(9, &b), "InvalidState"); // no se cobra dos veces
}

#[test]
fn si_el_servidor_no_liquida_cualquiera_cancela_y_hay_reembolso() {
    let mut g = Game::with_config();
    let a = g.new_player(10 * USDC);
    let b = g.new_player(10 * USDC);
    let extraño = Keypair::new();
    g.svm.airdrop(&extraño.pubkey(), 1_000_000_000).unwrap();

    g.create_match(5, 4).unwrap();
    g.join(&a, 5).unwrap();
    g.join(&b, 5).unwrap();
    assert_err(g.cancel(&extraño, 5), "CancelNotAllowed"); // sala abierta
    g.start(5).unwrap();
    assert_err(g.claim_refund(&a, 5), "InvalidState");

    set_time(&mut g.svm, START_TIME + TIMEOUT - 1);
    assert_err(g.cancel(&extraño, 5), "CancelNotAllowed");
    set_time(&mut g.svm, START_TIME + TIMEOUT);
    g.cancel(&extraño, 5).unwrap();
    assert_eq!(g.match_state(5).state, MatchState::Cancelled);
    assert_err(g.settle(5, &a), "InvalidState");

    g.claim_refund(&a, 5).unwrap();
    assert_eq!(g.balance(&a.token), 10 * USDC);
    assert_err(g.claim_refund(&a, 5), "NotAPlayer");
    assert_err(g.close(5), "RefundsPending");

    g.claim_refund(&b, 5).unwrap();
    assert_eq!(g.balance(&b.token), 10 * USDC);
    g.close(5).unwrap();
    assert!(!g.exists(&match_pda(5)));
}

#[test]
fn el_servidor_puede_cancelar_y_lo_sobrante_va_a_la_tesoreria() {
    let mut g = Game::with_config();
    let a = g.new_player(10 * USDC);

    g.create_match(11, 4).unwrap();
    g.join(&a, 11).unwrap();
    let server = g.server.insecure_clone();
    g.cancel(&server, 11).unwrap();
    g.claim_refund(&a, 11).unwrap();

    // Alguien manda USDC al vault por error: no debe bloquear el cierre.
    let vault = vault_pda(&match_pda(11));
    let match_key = match_pda(11);
    let mint = g.mint;
    set_token_account(&mut g.svm, &vault, &mint, &match_key, 3 * USDC);
    g.close(11).unwrap();
    assert_eq!(g.balance(&g.treasury), 3 * USDC);
    assert!(!g.exists(&vault));
}

#[test]
fn pausa_y_limites_de_configuracion() {
    let mut g = Game::with_config();
    g.update_config(ConfigUpdate {
        paused: Some(true),
        ..Default::default()
    })
    .unwrap();
    assert_err(g.create_match(1, 4), "Paused");
    g.update_config(ConfigUpdate {
        paused: Some(false),
        ..Default::default()
    })
    .unwrap();
    assert_err(g.create_match(1, 1), "InvalidMaxPlayers");
    assert_err(g.create_match(1, 101), "InvalidMaxPlayers");
    g.create_match(1, 100).unwrap();

    assert_err(
        g.update_config(ConfigUpdate {
            fee_bps: Some(2_501),
            ..Default::default()
        }),
        "FeeTooHigh",
    );
}
