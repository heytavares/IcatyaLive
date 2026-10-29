using System;
using System.Collections.Concurrent;
using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Icatya.Models;
using Icatya.Services.Rooms;

internal static class RoomChecks
{
    private sealed class VideoPump(RoomSession[] rooms, VideoFrame image) : IAsyncDisposable
    {
        private readonly CancellationTokenSource _stop = new();
        private Task? _loop;
        public void Start() => _loop = Task.Run(async () =>
        {
            try
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
                while (await timer.WaitForNextTickAsync(_stop.Token)) foreach (var room in rooms) room.SendFrame(image);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        });
        public async ValueTask DisposeAsync() { _stop.Cancel(); if (_loop != null) await _loop; _stop.Dispose(); }
    }
    private static Icatya.Services.PeerSession? Peer(RoomSession room, string direction, string id)
    {
        var gate = typeof(RoomSession).GetField("_gate", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(room)!;
        lock (gate)
        {
            var link = ((IDictionary)typeof(RoomSession).GetField(direction, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(room)!)[id];
            return link == null ? null : (Icatya.Services.PeerSession)link.GetType().GetProperty("Peer")!.GetValue(link)!;
        }
    }
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static async Task Until(Func<bool> condition, string label, int seconds = 45)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (!condition())
        {
            if (DateTime.UtcNow > until) throw new Exception("Timeout: " + label);
            await Task.Delay(50);
        }
        Console.WriteLine("PASS " + label);
    }
    public static async Task<int> RunAsync()
    {
        try
        {
            var code = RoomIdentity.CreateCode();
            Assert(RoomIdentity.NormalizeCode(code.ToLowerInvariant()) == code, "Room normalization");
            try { RoomIdentity.NormalizeCode("AAAAAA"); throw new Exception("Bad room accepted"); } catch (InvalidOperationException) { }
            try { RoomIdentity.NormalizeNick("bad\nnick"); throw new Exception("Bad nick accepted"); } catch (InvalidOperationException) { }
            string path = Path.Combine(Directory.GetCurrentDirectory(), "work", "icatya-room-check-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var store = new RoomIdentityStore(path);
                Assert(store.Load() == null, "Missing preferences");
                Assert(store.Save(new(code, "Ana")), "Save room");
                Assert(store.Load() == new RoomIdentity(code, "Ana"), "Restore room and nickname");
                File.WriteAllText(path, "{corrupt"); Assert(store.Load() == null, "Corrupt preferences");
                File.WriteAllText(path, "{}"); Assert(store.Load() == null, "Incomplete preferences");
            }
            finally { File.Delete(path); }
            Console.WriteLine("PASS invitation validation and saved nickname/room");
            string profilePath = Path.ChangeExtension(path, ".profile.json");
            try
            {
                var profileStore = new Icatya.Services.ProfilePreferencesStore(profilePath);
                Assert(profileStore.LoadAvatar() == "cat", "Default avatar");
                Assert(profileStore.SaveAvatar("tiger") && profileStore.LoadAvatar() == "tiger", "Saved avatar");
                Assert(!profileStore.SaveAvatar("../unknown.png"), "Invalid avatar accepted");
                File.WriteAllText(profilePath, "{invalid");
                Assert(profileStore.LoadAvatar() == "cat", "Corrupt profile recovery");
            }
            finally { File.Delete(profilePath); }
            Console.WriteLine("PASS avatar persistence, validation and recovery");

            var service = new Uri(Environment.GetEnvironmentVariable("ICATYA_ROOMS_TEST_URL") ?? "http://127.0.0.1:8787/");
            bool stun = !service.IsLoopback;
            await using var a = new RoomSession(new(code, "Ana"), useStun: stun, avatar: "dog");
            string bKey = RoomClientIdentity.Create();
            await using var b = new RoomSession(new(code, "Beto"), useStun: stun, clientKey: bKey);
            await using var c = new RoomSession(new(code, "Caio"), useStun: stun);
            var received = new ConcurrentDictionary<string, int>();
            var audioReceived = new ConcurrentDictionary<string, int>();
            foreach (var pair in new[] { ("a", a), ("b", b), ("c", c) })
            {
                pair.Item2.StatusChanged += text => Console.WriteLine(pair.Item1 + ": " + text);
                pair.Item2.FrameReceived += (from, frame) =>
                {
                    Assert(frame.Width == 1280 && frame.Height == 720, "Decoded room dimensions");
                    received.AddOrUpdate(pair.Item1 + from, 1, (_, old) => old + 1);
                };
                pair.Item2.AudioReceived += (from, seq, data) => audioReceived.AddOrUpdate(pair.Item1 + from, 1, (_, old) => old + 1);
                pair.Item2.Start(service);
            }
            await Until(() => new[] { a, b, c }.All(room => room.Members.Length == 3 && room.ConnectedViewers == 2), "three participants and six video directions connected", seconds: 75);
            Assert(a.CanRename && b.CanRename && c.CanRename, "Updated room metadata missing");
            await a.RenameAsync("Jogos da turma");
            await Until(() => a.Name == "Jogos da turma" && b.Name == a.Name && c.Name == a.Name,
                "renaming the room reaches all participants without recreating video");
            var summaries = await new RoomDirectory().QueryAsync(service, new[] { code, RoomIdentity.CreateCode() }, default);
            Assert(summaries.Single(room => room.Code == code).Count == 3 && summaries.Single(room => room.Code == code).Name == a.Name,
                "Directory did not return real room name/presence");
            Assert(a.Members.Length == 3 && summaries.Single(room => room.Code != code).Count == 0, "Directory created ghost members");
            Console.WriteLine("PASS real directory counts do not join rooms or open media peers");
            Assert(b.Members.Single(m => m.Id == a.SelfId).Avatar == "dog", "Join avatar missing");
            a.SetAvatar("tiger");
            await Until(() => b.Members.Single(m => m.Id == a.SelfId).Avatar == "tiger" && c.Members.Single(m => m.Id == a.SelfId).Avatar == "tiger", "avatar changes reach all participants without reconnecting video");
            var profile = CaptureProfile.Available[0];
            a.ConfigureVideo(profile, softwareOnly: true); b.ConfigureVideo(profile, softwareOnly: true); c.ConfigureVideo(profile, softwareOnly: true);
            a.SetSharing(true); b.SetSharing(true); c.SetSharing(true);
            using var image = VideoFrame.Rent(1280, 720);
            Array.Fill(image.Pixels, (byte)100);
            for (int i = 0; i < 40 && received.Count < 6; i++)
            {
                a.SendFrame(image); b.SendFrame(image); c.SendFrame(image);
                await Task.Delay(70);
            }
            Assert(received.Count == 6, "All three must receive both friends: " + received.Count);
            Console.WriteLine("PASS three simultaneous H.264 streams, each received by two viewers");
            using var opus = new Icatya.Services.Audio.OpusAudioEncoder();
            var pcm = new float[1920];
            for(int i=0;i<60 && audioReceived.Count<6;i++) {
                var packet=opus.Encode(pcm);a.SendAudioEncoded(packet);b.SendAudioEncoded(packet);c.SendAudioEncoded(packet);await Task.Delay(20);
            }
            Assert(audioReceived.Count==6,"All three receive both friends' audio: "+audioReceived.Count);
            Console.WriteLine("PASS three simultaneous stereo Opus streams in six directions");
            await using var pump = new VideoPump(new[] { a, b, c }, image);
            pump.Start();
            b.SetWatching(a.SelfId, false);
            await Until(() => Peer(a, "_outgoing", b.SelfId)?.ViewerWantsMedia == false, "closing one live unsubscribes only this viewer");
            await Task.Delay(200);
            int closedFrames = received["b" + a.SelfId], otherFrames = received["c" + a.SelfId];
            int closedAudio = audioReceived["b" + a.SelfId], otherAudio = audioReceived["c" + a.SelfId];
            for (int i = 0; i < 10; i++) { a.SendAudioEncoded(opus.Encode(pcm)); await Task.Delay(35); }
            Assert(received["b" + a.SelfId] == closedFrames && audioReceived["b" + a.SelfId] == closedAudio, "Closed live still decodes video/audio");
            Assert(received["c" + a.SelfId] > otherFrames && audioReceived["c" + a.SelfId] > otherAudio, "Closing a live interrupted another friend");
            Assert(b.Members.Length == 3 && b.Members.Single(m => m.Id == a.SelfId).Sharing, "Closing a live changed room presence/sharing");
            b.SetWatching(a.SelfId, true);
            await Until(() => Peer(a, "_outgoing", b.SelfId)?.ViewerWantsMedia == true && received["b" + a.SelfId] > closedFrames,
                "reopening the same live restores decoded video without rejoining");
            Console.WriteLine("PASS selective live close/reopen preserves other viewers and silences only the closed live");
            Assert(!RoomSession.NeedsReconnect(TimeSpan.FromSeconds(44), null), "Retried a normal ICE negotiation too soon");
            Assert(RoomSession.NeedsReconnect(TimeSpan.FromSeconds(45), null), "Hung negotiation has no recovery deadline");
            Assert(!RoomSession.NeedsReconnect(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2)), "Transient loss has no grace period");
            Assert(RoomSession.NeedsReconnect(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(3)), "Failed connection does not recover");
            var healthy = Peer(a, "_outgoing", c.SelfId)!;
            var broken = Peer(a, "_outgoing", b.SelfId)!;
            var oldSession = broken.SessionId;
            b.SetWatching(a.SelfId, false);
            broken.Dispose(); // Drop only Ana -> Beto, leaving the signaling room and all other lives intact.
            await Until(() => Peer(a, "_outgoing", b.SelfId) is { IsConnected: true, ViewerWantsMedia: false } next && !ReferenceEquals(next, broken) && a.ConnectedViewers == 2,
                "failed video reconnects automatically without leaving the room");
            Assert(!b.IsWatching(a.SelfId), "Reconnection reopened a live that was closed");
            b.SetWatching(a.SelfId, true);
            Assert(ReferenceEquals(healthy, Peer(a, "_outgoing", c.SelfId)) && healthy.IsConnected,
                "Recovery interrupted another viewer");
            int beforeRecovery = received["b" + a.SelfId];
            for (int i = 0; i < 30 && received["b" + a.SelfId] == beforeRecovery; i++) { a.SendFrame(image); await Task.Delay(70); }
            Assert(received["b" + a.SelfId] > beforeRecovery, "Reconnected room has no decoded video");
            Console.WriteLine("PASS restored H.264 reaches the viewer; unaffected live retains its original peer");
            var replacement = Peer(a, "_outgoing", b.SelfId)!;
            var lateAnswer = Icatya.Services.InviteCodec.Encode(new(1, "answer", oldSession, DateTimeOffset.UtcNow.AddMinutes(1), "v=0\r\n"));
            var stale = JsonSerializer.SerializeToElement(new { from = b.SelfId, kind = "answer", code = lateAnswer });
            await (Task)typeof(RoomSession).GetMethod("HandleSignalAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(a, new object[] { stale })!;
            Assert(ReferenceEquals(replacement, Peer(a, "_outgoing", b.SelfId)) && replacement.IsConnected, "Late answer disturbed replacement");
            Console.WriteLine("PASS stale signaling cannot replace/apply to the new video session");
            var incoming = Peer(b, "_incoming", a.SelfId)!;
            Assert(incoming.RequestReconnect(), "Receiver could not ask to restart a stalled live");
            await Until(() => Peer(a, "_outgoing", b.SelfId) is { IsConnected: true } next && !ReferenceEquals(next, replacement) && a.ConnectedViewers == 2,
                "viewer can request recovery over the existing protected control channel");
            Assert(ReferenceEquals(healthy, Peer(a, "_outgoing", c.SelfId)), "Viewer-requested recovery interrupted another stream");
            string oldA = a.SelfId;
            await a.DisposeAsync();
            await Until(() => b.Members.Length == 2 && c.Members.Length == 2, "creator leaves without ending room");
            int before = received["b" + c.SelfId];
            for (int i = 0; i < 6; i++) { b.SendFrame(image); c.SendFrame(image); await Task.Delay(70); }
            await Until(() => received["b" + c.SelfId] > before, "video continues after creator leaves");
            await using var returning = new RoomSession(new(code, "Ana voltou"), useStun: stun);
            returning.Start(service);
            await Until(() => returning.Members.Length == 3 && returning.ConnectedViewers == 2 && b.Members.Length == 3, "same permanent invitation rejoins the existing room", seconds: 75);
            Assert(returning.SelfId != oldA, "Fresh connection identity");
            b.SetSharing(false);
            await Until(() => c.Members.Single(m => m.Id == b.SelfId).Sharing == false, "pause updates participant state");
            string oldB = b.SelfId;
            await using var reconnected = new RoomSession(new(code, "Beto"), useStun: stun, clientKey: bKey);
            reconnected.Start(service);
            await Until(() => reconnected.Connected && c.Members.Length == 3 && !c.Members.Any(m => m.Id == oldB), "reconnection replaces the old presence without duplicating nickname");
            await Task.Delay(1500);
            Assert(!b.Connected && c.Members.Count(m => m.Nick == "Beto") == 1, "Replaced connection retried or duplicated");
            Console.WriteLine("PASS replaced window stops reconnecting instead of fighting with the new connection");
            var fit = Icatya.Controls.VideoGridPanel.Calculate(5, 1100, 650);
            Assert(fit.Columns >= 1 && fit.Width > 0 && Math.Abs(fit.Width / fit.Height - 16d / 9) < 0.001, "Grid aspect ratio");
            Console.WriteLine("PASS responsive cards keep 16:9 geometry");
            return 0;
        }
        catch (Exception ex) { Console.WriteLine("FAIL " + ex); return 1; }
    }
}

