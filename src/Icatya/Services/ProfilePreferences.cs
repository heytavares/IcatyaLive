using System;
using System.IO;
using System.Text.Json;

namespace Icatya.Services;

public sealed class ProfilePreferencesStore
{
    private sealed record Profile(string Avatar);
    private readonly string _path;
    public ProfilePreferencesStore(string? path = null) => _path = path ?? Path.Combine(LocalDataDirectory.Path, "profile.json");
    public string LoadAvatar()
    {
        try { return AvatarCatalog.Normalize(JsonSerializer.Deserialize<Profile>(File.ReadAllText(_path))?.Avatar); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return AvatarCatalog.Default; }
    }
    public bool SaveAvatar(string avatar)
    {
        if (!AvatarCatalog.IsValid(avatar)) return false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(new Profile(avatar)));
            File.Move(_path + ".tmp", _path, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}
