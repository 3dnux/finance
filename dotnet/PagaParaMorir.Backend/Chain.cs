using PagaParaMorir.Escrow;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;

namespace PagaParaMorir.Backend;

/// <summary>Lo que el backend necesita del contrato. Firma siempre la clave del servidor.</summary>
public interface IChain
{
    Task<ConfigAccount> GetConfigAsync();
    Task<IReadOnlyList<MatchAccount>> GetMatchesAsync();
    Task CreateMatchAsync(ulong matchId, ulong entryFee, byte maxPlayers);
    Task CancelMatchAsync(ulong matchId);
    Task CloseMatchAsync(ulong matchId);
}

public sealed class SolanaChain : IChain
{
    private readonly EscrowClient _client;
    private readonly Account _server;

    public SolanaChain(string rpcUrl, string programId, Account server)
        : this(new EscrowClient(ClientFactory.GetClient(rpcUrl), new EscrowProgram(new PublicKey(programId))), server)
    {
    }

    public SolanaChain(EscrowClient client, Account server)
    {
        _client = client;
        _server = server;
    }

    public PublicKey ServerKey => _server.PublicKey;

    public Task<ConfigAccount> GetConfigAsync() => _client.GetConfigAsync(refresh: true);

    public async Task<IReadOnlyList<MatchAccount>> GetMatchesAsync() => await _client.GetMatchesAsync();

    public async Task CreateMatchAsync(ulong matchId, ulong entryFee, byte maxPlayers)
    {
        var config = await _client.GetConfigAsync();
        await Send(_client.Program.CreateMatch(_server.PublicKey, config.UsdcMint, matchId, entryFee, maxPlayers));
    }

    public Task CancelMatchAsync(ulong matchId) => Send(_client.Program.CancelMatch(_server.PublicKey, matchId));

    public async Task CloseMatchAsync(ulong matchId)
    {
        var config = await _client.GetConfigAsync();
        await Send(_client.Program.CloseMatch(_server.PublicKey, matchId, config.Treasury, config.UsdcMint));
    }

    private Task<string> Send(Solana.Unity.Rpc.Models.TransactionInstruction instruction) =>
        _client.SendAsync(_server.PublicKey, tx =>
        {
            tx.Sign(_server);
            return _client.Rpc.SendTransactionAsync(tx.Serialize(), false, Commitment.Confirmed);
        }, instruction);
}
