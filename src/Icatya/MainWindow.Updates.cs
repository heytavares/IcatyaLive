using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Icatya.Services;

namespace Icatya;

public partial class MainWindow
{
    private AvailableUpdate? _availableUpdate;
    private bool _checkingUpdate;
    private static Version InstalledVersion => new(typeof(MainWindow).Assembly.GetName().Version!.ToString(3));
    private async void CheckForUpdate(object sender, RoutedEventArgs args)
    {
        if (_checkingUpdate || _closing) return;
        _checkingUpdate = true;
        UpdateButton.IsEnabled = false;
        try
        {
            if (_availableUpdate == null)
            {
                UpdateStatus.Text = "Consultando o GitHub…";
                _availableUpdate = await new UpdateService().CheckAsync(InstalledVersion, _lifetime.Token);
                if (_closing) return;
                if (_availableUpdate == null) UpdateStatus.Text = $"Você está atualizado: {InstalledVersion}.";
                else
                {
                    UpdateStatus.Text = $"Versão {_availableUpdate.Version} disponível. Ao instalar, o app será fechado e reaberto. Sua sala e seu perfil serão preservados.";
                    UpdateButton.Content = $"Instalar {_availableUpdate.Version}";
                }
                return;
            }
            string script = Path.Combine(AppContext.BaseDirectory, "Atualizar-Icatya.ps1");
            if (!File.Exists(script)) throw new InvalidOperationException("Abra a versão da pasta app para usar o atualizador. O script não acompanha a execução pelos fontes.");
            string resultFile = Path.Combine(LocalDataDirectory.Path, "update-result-" + Guid.NewGuid().ToString("N") + ".json");
            Directory.CreateDirectory(LocalDataDirectory.Path);
            var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory
            };
            foreach (string value in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-ExpectedVersion", _availableUpdate.Version.ToString(), "-StatusFile", resultFile })
                start.ArgumentList.Add(value);
            UpdateStatus.Text = "Baixando e verificando o pacote. Você pode continuar usando o app; ele será reaberto quando a instalação começar.";
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Não foi possível iniciar o atualizador.");
            await process.WaitForExitAsync(_lifetime.Token);
            if (_closing) return;
            if (process.ExitCode != 0)
            {
                string message = "Não foi possível instalar. A versão anterior foi preservada.";
                if (File.Exists(resultFile))
                {
                    using var result = JsonDocument.Parse(await File.ReadAllTextAsync(resultFile, _lifetime.Token));
                    if (result.RootElement.TryGetProperty("message", out var detail)) message = detail.GetString() ?? message;
                }
                throw new InvalidOperationException(message);
            }
            UpdateStatus.Text = "Atualização concluída.";
        }
        catch (OperationCanceledException)
        {
            if (!_closing) UpdateStatus.Text = "A consulta demorou demais. Confira a internet e tente novamente.";
        }
        catch (Exception ex)
        {
            _availableUpdate = null;
            UpdateButton.Content = "Verificar atualizações";
            if (!_closing) UpdateStatus.Text = ex is System.Net.Http.HttpRequestException
                ? "Não foi possível acessar o GitHub. Confira a internet e tente novamente." : ex.Message;
        }
        finally
        {
            _checkingUpdate = false;
            if (!_closing) UpdateButton.IsEnabled = true;
        }
    }
}
