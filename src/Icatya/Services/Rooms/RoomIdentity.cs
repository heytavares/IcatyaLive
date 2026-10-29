using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace Icatya.Services.Rooms;

public sealed record RoomIdentity(string Code, string Nick, string Name = "Sala de amigos")
{
    public static string CreateCode() => "ICATYA-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    public static string NormalizeCode(string? text)
    {
        var code = (text ?? "").Trim().ToUpperInvariant();
        if (code.StartsWith("ICATYA-", StringComparison.Ordinal)) code = code[7..];
        if (code.Length != 32 || !code.All(Uri.IsHexDigit))
            throw new InvalidOperationException("Cole o convite completo da sala, começando com ICATYA-.");
        return "ICATYA-" + code;
    }
    public static string NormalizeNick(string? text)
    {
        string nick = (text ?? "").Trim();
        if (nick.Length is < 1 or > 24 || nick.Any(char.IsControl) || nick.Any(c => char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format))
            throw new InvalidOperationException("Escolha um nick de 1 a 24 caracteres, sem quebras de linha.");
        return nick;
    }
    public static string NormalizeName(string? text)
    {
        string name = (text ?? "").Trim();
        if (name.Length is < 1 or > 48 || name.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format))
            throw new InvalidOperationException("O nome da sala deve ter entre 1 e 48 caracteres, sem quebras de linha.");
        for (int i = 0; i < name.Length; i++)
            if (char.IsSurrogate(name[i]) && (!char.IsHighSurrogate(name[i]) || ++i >= name.Length || !char.IsLowSurrogate(name[i])))
                throw new InvalidOperationException("O nome da sala contém um caractere incompleto. Digite-o novamente.");
        return name;
    }
}

public sealed class RoomIdentityStore
{
    private readonly string _path;
    public RoomIdentityStore(string? path = null) => _path = path ?? Path.Combine(LocalDataDirectory.Path, "room.json");
    public RoomIdentity? Load()
    {
        try
        {
            var value = JsonSerializer.Deserialize<RoomIdentity>(File.ReadAllText(_path));
            return value == null ? null : new(RoomIdentity.NormalizeCode(value.Code), RoomIdentity.NormalizeNick(value.Nick), RoomIdentity.NormalizeName(value.Name ?? "Sala de amigos"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException) { return null; }
    }
    public bool Save(RoomIdentity value)
    {
        try
        {
            value = new(RoomIdentity.NormalizeCode(value.Code), RoomIdentity.NormalizeNick(value.Nick), RoomIdentity.NormalizeName(value.Name));
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(value));
            File.Move(_path + ".tmp", _path, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}

public static class RoomServiceAddress
{
    // Deployment is separate from the desktop package; do not silently contact an unconfigured service.
    public static Uri? Load()
    {
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "rooms-service.json")));
            var text = json.RootElement.GetProperty("url").GetString();
            if (Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
                (uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback) &&
                string.IsNullOrEmpty(uri.UserInfo) && uri.AbsolutePath == "/") return uri;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or KeyNotFoundException or InvalidOperationException) { }
        return null;
    }
}
