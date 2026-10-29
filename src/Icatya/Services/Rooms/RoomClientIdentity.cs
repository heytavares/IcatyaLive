using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Icatya.Services.Rooms;

public static class RoomClientIdentity
{
    public static string Create() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    public static string LoadOrCreate()
    {
        string path = Path.Combine(LocalDataDirectory.Path, "client-id.txt");
        try
        {
            if (File.Exists(path))
            {
                string existing = File.ReadAllText(path).Trim();
                if (existing.Length == 32 && existing.All(Uri.IsHexDigit)) return existing.ToLowerInvariant();
            }
            Directory.CreateDirectory(LocalDataDirectory.Path);
            string created = Create();
            File.WriteAllText(path, created);
            return created;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return Create(); }
    }
}
