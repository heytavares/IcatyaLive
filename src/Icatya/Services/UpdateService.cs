using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Icatya.Services;

public sealed record AvailableUpdate(Version Version, string PackageName, Uri Download, string Sha256);

public sealed class UpdateService
{
    public const string Endpoint = "https://api.github.com/repos/heytavares/IcatyaLive/releases/latest";
    private static readonly HttpClient Client = CreateClient();
    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Icatya-Updater/" + typeof(UpdateService).Assembly.GetName().Version!.ToString(3));
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }
    public async Task<AvailableUpdate?> CheckAsync(Version installed, CancellationToken stop)
    {
        using var response = await Client.GetAsync(Endpoint, stop);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new InvalidOperationException("Ainda não há uma Release pública disponível no GitHub.");
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || (int)response.StatusCode == 429)
            throw new InvalidOperationException("O GitHub limitou as consultas. Tente novamente mais tarde.");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(stop);
        if (json.Length > 2_000_000) throw new InvalidOperationException("Resposta do GitHub inválida.");
        return Parse(json, installed);
    }
    public static AvailableUpdate? Parse(string json, Version installed)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean())
                throw new InvalidOperationException("A versão encontrada ainda é de teste.");
            string tag = root.GetProperty("tag_name").GetString() ?? "";
            if (tag.StartsWith("v", StringComparison.OrdinalIgnoreCase)) tag = tag[1..];
            if (!Version.TryParse(tag, out var version) || version.Build < 0 || version.Revision >= 0)
                throw new InvalidOperationException("A Release tem um número de versão inválido.");
            if (version <= installed) return null;
            string name = $"Icatya-{version}-Windows-x64.zip";
            var assets = root.GetProperty("assets").EnumerateArray().Where(a =>
                a.GetProperty("name").GetString() == name && a.GetProperty("state").GetString() == "uploaded").ToArray();
            if (assets.Length != 1)
                throw new InvalidOperationException($"A Release {version} está incompleta: falta {name}. Avise o autor.");
            var asset = assets[0];
            long size = asset.GetProperty("size").GetInt64();
            if (size <= 0 || size > 512L * 1024 * 1024) throw new InvalidOperationException("O pacote tem um tamanho inválido.");
            var url = new Uri(asset.GetProperty("browser_download_url").GetString() ?? "");
            if (url.Scheme != "https" || url.Host != "github.com" || !url.IsDefaultPort || url.UserInfo.Length != 0 ||
                !url.AbsolutePath.StartsWith("/heytavares/IcatyaLive/releases/download/", StringComparison.OrdinalIgnoreCase) ||
                !Uri.UnescapeDataString(url.AbsolutePath).EndsWith("/" + name, StringComparison.Ordinal))
                throw new InvalidOperationException("O pacote não pertence ao repositório oficial do iCatya.");
            string digest = asset.TryGetProperty("digest", out var value) ? value.GetString() ?? "" : "";
            if (!System.Text.RegularExpressions.Regex.IsMatch(digest, "^sha256:[0-9a-fA-F]{64}$"))
                throw new InvalidOperationException("O GitHub ainda não disponibilizou a verificação do pacote. Tente novamente mais tarde.");
            return new(version, name, url, digest[7..]);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException)
        {
            throw new InvalidOperationException("Não foi possível interpretar a Release do GitHub.", ex);
        }
    }
}
