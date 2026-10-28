using System;
using System.IO;
using System.Text.Json;

namespace Icatya.Services;

public sealed record UiPreferences(double SidebarWidth = 360, bool SidebarHidden = false);

public sealed class UiPreferencesStore
{
    private readonly string _path;
    public UiPreferencesStore(string? path = null) => _path = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Icatya", "ui.json");

    public UiPreferences Load()
    {
        try
        {
            var value = JsonSerializer.Deserialize<UiPreferences>(File.ReadAllText(_path));
            return value != null && double.IsFinite(value.SidebarWidth) && value.SidebarWidth is >= 220 and <= 4000
                ? value : new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public bool Save(UiPreferences value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            string temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(value));
            File.Move(temporary, _path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}
