using System;
using System.IO;

namespace Icatya.Services;

internal static class LocalDataDirectory
{
    // Allows isolated development/test profiles without replacing the user's saved room.
    public static string Path => Environment.GetEnvironmentVariable("ICATYA_DATA_DIRECTORY") is { Length: > 0 } directory
        ? System.IO.Path.GetFullPath(directory)
        : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Icatya");
}
