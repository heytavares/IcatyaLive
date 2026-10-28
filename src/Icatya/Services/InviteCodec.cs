using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace Icatya.Services;

public sealed record SessionInvite(int Version, string Kind, Guid Session, DateTimeOffset Expires, string Sdp);

public static class InviteCodec
{
    private const int MaxJsonBytes = 65536;
    public static string Encode(SessionInvite invite)
    {
        using var output = new MemoryStream();
        using (var compressor = new BrotliStream(output, CompressionLevel.Optimal, true))
            JsonSerializer.Serialize(compressor, invite);
        return "MOAB1." + Convert.ToBase64String(output.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static SessionInvite Decode(string text)
    {
        try
        {
            text = text.Trim();
            if (!text.StartsWith("MOAB1.", StringComparison.Ordinal) || text.Length > 24000)
                throw new FormatException();
            var base64 = text[6..].Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight((base64.Length + 3) / 4 * 4, '=');
            using var input = new MemoryStream(Convert.FromBase64String(base64));
            using var decompressor = new BrotliStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = decompressor.Read(buffer)) != 0)
            {
                if (output.Length + read > MaxJsonBytes) throw new FormatException();
                output.Write(buffer, 0, read);
            }
            var invite = JsonSerializer.Deserialize<SessionInvite>(output.ToArray());
            if (invite is null || invite.Version != 1 || invite.Session == Guid.Empty ||
                (invite.Kind != "offer" && invite.Kind != "answer") || string.IsNullOrWhiteSpace(invite.Sdp) ||
                !invite.Sdp.StartsWith("v=0", StringComparison.Ordinal)) throw new FormatException();
            if (invite.Expires < DateTimeOffset.UtcNow)
                throw new InvalidOperationException("Este convite expirou. Peça um novo convite.");
            return invite;
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or JsonException or ArgumentException)
        {
            throw new InvalidOperationException("Convite inválido ou incompleto. Copie todo o texto que começa com MOAB1.", ex);
        }
    }
}

