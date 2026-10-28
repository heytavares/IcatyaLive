using System;
using System.IO;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace MoabLive.Services.Video;

public static class NativeVideoRuntime
{
    private static readonly object Gate = new();
    private static bool _loaded;
    public static string Version { get; private set; } = "";
    public static void EnsureLoaded()
    {
        lock (Gate)
        {
            if (_loaded) return;
            string? path = null;
            foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
            {
                for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                {
                    var candidate = Path.Combine(directory.FullName, "native", "ffmpeg");
                    if (File.Exists(Path.Combine(candidate, "avcodec-62.dll"))) { path = candidate; break; }
                }
                if (path != null) break;
            }
            if (path == null) throw new InvalidOperationException("Componentes de vídeo ausentes. Execute Preparar video.cmd na pasta do MoabLive.");
            // Load in dependency order, using explicit paths instead of altering Windows settings.
            foreach (var file in new[] { "avutil-60.dll", "swresample-6.dll", "swscale-9.dll", "avcodec-62.dll" })
                NativeLibrary.Load(Path.Combine(path, file));
            ffmpeg.RootPath = path;
            ffmpeg.av_log_set_level(ffmpeg.AV_LOG_ERROR);
            Version = ffmpeg.av_version_info();
            _loaded = true;
        }
    }
}

