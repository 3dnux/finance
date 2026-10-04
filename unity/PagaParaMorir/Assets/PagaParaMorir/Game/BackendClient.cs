using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace PagaParaMorir.Game
{
    /// <summary>Servidor de una partida, según el backend.</summary>
    [Serializable]
    public class GameServerInfo
    {
        public long matchId;
        public string host;
        public int port;
        public string status;

        /// <summary>JsonUtility crea el objeto aunque el JSON traiga null: revisamos que tenga datos.</summary>
        public bool IsRunning => !string.IsNullOrEmpty(host) && port > 0 && status == "Running";
    }

    public class BackendException : Exception
    {
        public BackendException(string message) : base(message) { }
    }

    /// <summary>Cliente de la API del backend (ver dotnet/PagaParaMorir.Backend).</summary>
    public class BackendClient
    {
        // Los llena JsonUtility.
#pragma warning disable 0649
        [Serializable]
        private class RoomDto
        {
            public long matchId;
            public GameServerInfo server;
        }

        [Serializable]
        private class RoomsResponse
        {
            public RoomDto[] rooms;
        }

        [Serializable]
        private class ErrorResponse
        {
            public string error;
        }
#pragma warning restore 0649

        private readonly string _baseUrl;

        public BackendClient(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');
        }

        /// <summary>Servidores listos, por sala.</summary>
        public async Task<Dictionary<ulong, GameServerInfo>> GetRunningServersAsync()
        {
            var json = await GetAsync("/api/rooms");
            var response = JsonUtility.FromJson<RoomsResponse>(json);
            var servers = new Dictionary<ulong, GameServerInfo>();
            foreach (var room in response?.rooms ?? Array.Empty<RoomDto>())
                if (room.server != null && room.server.IsRunning) servers[(ulong)room.matchId] = room.server;
            return servers;
        }

        /// <summary>Dónde conectarse para jugar una sala. Lanza si su servidor todavía no está listo.</summary>
        public async Task<GameServerInfo> GetServerAsync(ulong matchId)
        {
            var server = JsonUtility.FromJson<GameServerInfo>(await GetAsync($"/api/matches/{matchId}/server"));
            if (server == null || !server.IsRunning)
                throw new BackendException("El servidor de tu partida todavía no está listo.");
            return server;
        }

        private async Task<string> GetAsync(string path)
        {
            using (var request = UnityWebRequest.Get(_baseUrl + path))
            {
                request.timeout = 10;
                var done = new TaskCompletionSource<bool>();
                request.SendWebRequest().completed += _ => done.TrySetResult(true);
                await done.Task;

                var body = request.downloadHandler?.text ?? "";
                if (request.result == UnityWebRequest.Result.Success) return body;
                if (request.result == UnityWebRequest.Result.ProtocolError)
                {
                    var error = TryParseError(body);
                    throw new BackendException(error ?? $"El backend respondió {request.responseCode}.");
                }
                throw new BackendException("No hay conexión con el backend del juego.");
            }
        }

        private static string TryParseError(string body)
        {
            try
            {
                var error = JsonUtility.FromJson<ErrorResponse>(body)?.error;
                return string.IsNullOrEmpty(error) ? null : error;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
