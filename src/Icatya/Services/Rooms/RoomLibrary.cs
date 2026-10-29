using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Icatya.Services.Rooms;

public sealed record SavedRoom(RoomIdentity Identity, DateTimeOffset LastUsed);

// Saved invitations stay in the local profile. Old room.json remains compatible with previous builds.
public sealed class RoomLibrary
{
    public const int MaximumRooms = 50;
    private readonly string _path;
    private readonly RoomIdentityStore _legacy;
    private readonly object _gate = new();
    public RoomLibrary(string? path = null, string? legacyPath = null)
    {
        _path = path ?? Path.Combine(LocalDataDirectory.Path, "rooms.json");
        _legacy = new(legacyPath ?? Path.Combine(Path.GetDirectoryName(_path)!, "room.json"));
    }
    private static RoomIdentity Valid(RoomIdentity value) => new(RoomIdentity.NormalizeCode(value.Code),
        RoomIdentity.NormalizeNick(value.Nick), RoomIdentity.NormalizeName(value.Name ?? "Sala de amigos"));
    public SavedRoom[] Load()
    {
        lock (_gate)
        {
            try
            {
                if (new FileInfo(_path).Length > 256_000) throw new InvalidDataException();
                var rooms = JsonSerializer.Deserialize<SavedRoom[]>(File.ReadAllText(_path)) ?? [];
                if (rooms.Length > MaximumRooms || rooms.Any(room => room == null || room.Identity == null)) throw new InvalidDataException();
                return rooms.Select(room => new SavedRoom(Valid(room.Identity), room.LastUsed))
                    .OrderByDescending(room => room.LastUsed).DistinctBy(room => room.Identity.Code).ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException)
            {
                var previous = _legacy.Load();
                return previous == null ? [] : [new(previous, DateTimeOffset.MinValue)];
            }
        }
    }
    public bool Remember(RoomIdentity identity)
    {
        identity = Valid(identity);
        lock (_gate)
        {
            var rooms = Load().Where(room => room.Identity.Code != identity.Code).ToList();
            if (rooms.Count >= MaximumRooms) throw new InvalidOperationException("O limite é de 50 salas salvas neste PC.");
            rooms.Insert(0, new(identity, DateTimeOffset.UtcNow));
            return Save(rooms);
        }
    }
    public bool UpdateNames(IEnumerable<(string Code, string Name)> names)
    {
        lock (_gate)
        {
            var updates = names.ToDictionary(item => RoomIdentity.NormalizeCode(item.Code), item => RoomIdentity.NormalizeName(item.Name));
            var rooms = Load(); bool changed = false;
            for (int i = 0; i < rooms.Length; i++)
                if (updates.TryGetValue(rooms[i].Identity.Code, out var name) && name != rooms[i].Identity.Name)
                { rooms[i] = rooms[i] with { Identity = rooms[i].Identity with { Name = name } }; changed = true; }
            return !changed || Save(rooms);
        }
    }
    private bool Save(IEnumerable<SavedRoom> rooms)
    {
        string temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(rooms));
            File.Move(temporary, _path, true); return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
        finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
}
