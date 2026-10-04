using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;
using Solana.Unity.Wallet.Utilities;

namespace PagaParaMorir.Escrow
{
    /// <summary>
    /// Firma y envía una transacción. En Unity es <c>Web3.Wallet.SignAndSendTransaction</c>;
    /// en pruebas o herramientas se firma con un <see cref="Account"/> local.
    /// </summary>
    public delegate Task<RequestResult<string>> SignAndSend(Transaction transaction);

    /// <summary>Lecturas y acciones del jugador contra el programa escrow.</summary>
    public class EscrowClient
    {
        public IRpcClient Rpc { get; }
        public EscrowProgram Program { get; }

        /// <summary>Tiempo máximo esperando la confirmación de una transacción.</summary>
        public TimeSpan ConfirmTimeout { get; set; } = TimeSpan.FromSeconds(60);

        private ConfigAccount _config;

        public EscrowClient(IRpcClient rpc, EscrowProgram program = null)
        {
            Rpc = rpc ?? throw new ArgumentNullException(nameof(rpc));
            Program = program ?? new EscrowProgram();
        }

        // ---------- Lecturas ----------

        /// <summary>Configuración global (servidor, comisión, mint de USDC). Se cachea.</summary>
        public async Task<ConfigAccount> GetConfigAsync(bool refresh = false)
        {
            if (_config != null && !refresh) return _config;
            var data = await GetAccountDataAsync(Program.ConfigAddress());
            if (data == null)
                throw new EscrowException("El juego no está configurado en esta red (falta initialize_config).");
            _config = ConfigAccount.Deserialize(data);
            return _config;
        }

        /// <summary>Una partida por id, o <c>null</c> si no existe (o ya se cerró).</summary>
        public async Task<MatchAccount> GetMatchAsync(ulong matchId)
        {
            var address = Program.MatchAddress(matchId);
            var data = await GetAccountDataAsync(address);
            return data == null ? null : MatchAccount.Deserialize(data, address);
        }

        /// <summary>Todas las partidas, opcionalmente filtradas por estado.</summary>
        public async Task<List<MatchAccount>> GetMatchesAsync(MatchState? state = null)
        {
            var filters = new List<MemCmp>
            {
                new MemCmp { Offset = 0, Bytes = Encoders.Base58.EncodeData(EscrowProgram.MatchDiscriminator) },
            };
            if (state.HasValue)
            {
                filters.Add(new MemCmp
                {
                    Offset = MatchAccount.StateOffset,
                    Bytes = Encoders.Base58.EncodeData(new[] { (byte)state.Value }),
                });
            }

            var result = await Rpc.GetProgramAccountsAsync(
                Program.ProgramId.Key, Commitment.Confirmed, memCmpList: filters);
            if (!result.WasSuccessful)
                throw new EscrowException("No se pudieron cargar las salas: " + result.Reason);

            return result.Result
                .Select(a => MatchAccount.Deserialize(
                    Convert.FromBase64String(a.Account.Data[0]), new PublicKey(a.PublicKey)))
                .OrderBy(m => m.MatchId)
                .ToList();
        }

        /// <summary>Partidas donde el jugador tiene dinero adentro (abiertas, en juego o canceladas sin reclamar).</summary>
        public async Task<List<MatchAccount>> GetMatchesOfPlayerAsync(PublicKey player)
        {
            var all = await GetMatchesAsync();
            return all.Where(m => m.HasPlayer(player) && m.State != MatchState.Settled).ToList();
        }

        /// <summary>Saldo de USDC del dueño en unidades mínimas (0 si aún no tiene cuenta de USDC).</summary>
        public async Task<ulong> GetUsdcBalanceAsync(PublicKey owner)
        {
            var config = await GetConfigAsync();
            var ata = EscrowProgram.UsdcAccountOf(owner, config.UsdcMint);
            var result = await Rpc.GetTokenAccountBalanceAsync(ata.Key, Commitment.Confirmed);
            return result.WasSuccessful && result.Result?.Value != null ? result.Result.Value.AmountUlong : 0;
        }

        /// <summary>Saldo de SOL en lamports (se necesita un poco para pagar las comisiones de red).</summary>
        public async Task<ulong> GetSolBalanceAsync(PublicKey owner)
        {
            var result = await Rpc.GetBalanceAsync(owner.Key, Commitment.Confirmed);
            return result.WasSuccessful ? result.Result.Value : 0;
        }

        // ---------- Acciones del jugador ----------

        /// <summary>Paga la entrada y entra a la sala. Devuelve la firma de la transacción.</summary>
        public async Task<string> JoinMatchAsync(PublicKey player, ulong matchId, SignAndSend send)
        {
            var config = await GetConfigAsync();
            var match = await GetMatchAsync(matchId) ?? throw new EscrowException("La sala ya no existe.");
            if (match.State != MatchState.Open) throw new EscrowException(EscrowErrors.Describe(EscrowErrorCode.InvalidState), EscrowErrorCode.InvalidState);
            if (match.HasPlayer(player)) throw new EscrowException(EscrowErrors.Describe(EscrowErrorCode.AlreadyJoined), EscrowErrorCode.AlreadyJoined);
            if (match.IsFull) throw new EscrowException(EscrowErrors.Describe(EscrowErrorCode.MatchFull), EscrowErrorCode.MatchFull);
            if (await GetUsdcBalanceAsync(player) < match.EntryFee)
                throw new EscrowException($"Necesitas {Usdc.Format(match.EntryFee)} para entrar.");

            return await SendAsync(player, send, Program.JoinMatch(player, matchId, config.UsdcMint));
        }

        /// <summary>Sale de una sala que aún no empieza y recupera la entrada.</summary>
        public async Task<string> LeaveMatchAsync(PublicKey player, ulong matchId, SignAndSend send)
        {
            var config = await GetConfigAsync();
            return await SendAsync(player, send, Program.LeaveMatch(player, matchId, config.UsdcMint));
        }

        /// <summary>Recupera la entrada de una partida cancelada.</summary>
        public async Task<string> ClaimRefundAsync(PublicKey player, ulong matchId, SignAndSend send)
        {
            var config = await GetConfigAsync();
            return await SendAsync(player, send, Program.ClaimRefund(player, matchId, config.UsdcMint));
        }

        /// <summary>
        /// Cancela una partida en juego cuyo servidor no reportó ganador a tiempo,
        /// para que todos puedan reclamar su entrada.
        /// </summary>
        public Task<string> CancelExpiredMatchAsync(PublicKey caller, ulong matchId, SignAndSend send) =>
            SendAsync(caller, send, Program.CancelMatch(caller, matchId));

        /// <summary>¿Ya pasó el tiempo límite para que el servidor liquide esta partida?</summary>
        public static bool IsSettleExpired(MatchAccount match, ConfigAccount config, long nowUnix) =>
            match.State == MatchState.InProgress && nowUnix >= match.StartedAt + config.SettleTimeoutSecs;

        // ---------- Envío ----------

        /// <summary>Arma, firma, envía y espera la confirmación de una transacción.</summary>
        public async Task<string> SendAsync(PublicKey feePayer, SignAndSend send, params TransactionInstruction[] instructions)
        {
            var blockhash = await Rpc.GetLatestBlockHashAsync(Commitment.Confirmed);
            if (!blockhash.WasSuccessful)
                throw new EscrowException("No hay conexión con la red de Solana: " + blockhash.Reason);

            var tx = new Transaction
            {
                FeePayer = feePayer,
                RecentBlockHash = blockhash.Result.Value.Blockhash,
                Instructions = instructions.ToList(),
                Signatures = new List<SignaturePubKeyPair>(),
            };

            var sent = await send(tx);
            if (sent == null) throw new EscrowException("La billetera no firmó la transacción.");
            if (!sent.WasSuccessful)
                throw EscrowErrors.FromFailure(sent.Reason, sent.ErrorData?.Logs);

            await ConfirmAsync(sent.Result);
            return sent.Result;
        }

        /// <summary>Espera a que la transacción quede confirmada; lanza si falló en la red.</summary>
        public async Task ConfirmAsync(string signature)
        {
            var deadline = DateTime.UtcNow + ConfirmTimeout;
            while (DateTime.UtcNow < deadline)
            {
                var statuses = await Rpc.GetSignatureStatusesAsync(new List<string> { signature }, true);
                var status = statuses.WasSuccessful ? statuses.Result?.Value?.FirstOrDefault() : null;
                if (status != null)
                {
                    if (status.Error != null)
                    {
                        var tx = await Rpc.GetTransactionAsync(signature, Commitment.Confirmed);
                        throw EscrowErrors.FromFailure("la red rechazó la transacción", tx.Result?.Meta?.LogMessages);
                    }
                    if (status.ConfirmationStatus == "confirmed" || status.ConfirmationStatus == "finalized")
                        return;
                }
                await Task.Delay(500);
            }
            throw new EscrowException("La transacción no se confirmó a tiempo. Revisa tu saldo antes de reintentar.");
        }

        private async Task<byte[]> GetAccountDataAsync(PublicKey address)
        {
            var result = await Rpc.GetAccountInfoAsync(address.Key, Commitment.Confirmed);
            if (!result.WasSuccessful)
                throw new EscrowException("No hay conexión con la red de Solana: " + result.Reason);
            var info = result.Result?.Value;
            return info == null ? null : Convert.FromBase64String(info.Data[0]);
        }
    }
}
