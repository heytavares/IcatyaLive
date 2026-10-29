using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Icatya.Services.Rooms;

public sealed record RoomSummary(string Code, string Name, int Count, string[] Avatars);

// A short-lived snapshot, requested only while choosing a room. Never joins or creates video peers.
public sealed class RoomDirectory
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(12) };
    public async Task<RoomSummary[]> QueryAsync(Uri service, IEnumerable<string> codes, CancellationToken cancellation)
    {
        var requested = codes.Select(RoomIdentity.NormalizeCode).Distinct().Take(RoomLibrary.MaximumRooms).ToArray();
        var result = new List<RoomSummary>();
        foreach (var batch in requested.Chunk(20))
        {
            using var response = await Client.PostAsJsonAsync(new Uri(service, "v1/rooms/summaries"),
                new { codes = batch.Select(code => code[7..].ToLowerInvariant()).ToArray() }, cancellation);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Não foi possível consultar as salas agora.");
            var json = await response.Content.ReadAsStringAsync(cancellation);
            if (json.Length > 64_000) throw new InvalidOperationException("Resposta de salas inválida.");
            using var document = JsonDocument.Parse(json);
            foreach (var item in document.RootElement.GetProperty("rooms").EnumerateArray())
            {
                string code = RoomIdentity.NormalizeCode(item.GetProperty("code").GetString());
                int count = item.GetProperty("count").GetInt32();
                if (!batch.Contains(code) || count is < 0 or > 12 || result.Any(room => room.Code == code))
                    throw new InvalidOperationException("Resposta de salas inválida.");
                var avatars = item.GetProperty("avatars").EnumerateArray().Take(3).Select(avatar => AvatarCatalog.Normalize(avatar.GetString())).ToArray();
                result.Add(new(code, RoomIdentity.NormalizeName(item.GetProperty("name").GetString()), count, avatars));
            }
        }
        return result.ToArray();
    }
}
