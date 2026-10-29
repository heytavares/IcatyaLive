using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Icatya.Services.Rooms;

internal static class RoomLibraryChecks
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    public static int Run()
    {
        try
        {
            string directory = Path.Combine(Directory.GetCurrentDirectory(), "work", "room-library-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "rooms.json"), legacy = Path.Combine(directory, "room.json");
            var library = new RoomLibrary(path, legacy);
            Require(library.Load().Length == 0, "Empty profile contains rooms");
            var first = new RoomIdentity(RoomIdentity.CreateCode(), "jax");
            File.WriteAllText(legacy, JsonSerializer.Serialize(new { first.Code, first.Nick }));
            Require(library.Load().Single().Identity == first, "Legacy invite/nickname migration failed");
            Require(library.Remember(first), "Could not remember migrated room");
            var second = new RoomIdentity(RoomIdentity.CreateCode(), "jax", "Cinema 🎬");
            Require(library.Remember(second) && library.Load().Length == 2, "Does not preserve multiple invitations");
            Require(library.Remember(first with { Name = "Amigos" }) && library.Load().Length == 2, "Visiting same room duplicated entry");
            var order = library.Load().Select(room => room.Identity.Code).ToArray();
            Require(library.UpdateNames([(second.Code, "Filmes e séries")]), "Could not cache shared room name");
            Require(library.Load().Single(room => room.Identity.Code == second.Code).Identity.Name == "Filmes e séries", "Updated name not saved");
            Require(order.SequenceEqual(library.Load().Select(room => room.Identity.Code)), "Metadata lookup changes last-visited ordering");
            var reloaded = new RoomLibrary(path, legacy);
            Require(reloaded.Load().Length == 2 && new RoomIdentityStore(legacy).Load() == first,
                "Saved rooms disappeared across restart or old invite was overwritten");
            foreach (string name in new[] { "", "   ", "line\nbreak", new string('x', 49), "bad\u200Bname" })
            {
                bool rejected = false;
                try { RoomIdentity.NormalizeName(name); } catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "Invalid name accepted");
            }
            Require(RoomIdentity.NormalizeName("  Jogos 🎮  ") == "Jogos 🎮", "Valid Unicode name rejected");
            File.WriteAllText(path, "{broken");
            Require(reloaded.Load().Single().Identity == first, "Corrupt library cannot recover legacy room");
            Console.WriteLine("PASS saved rooms: legacy migration, unique invitations, shared name cache, visit order, restart, Unicode and recovery");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
