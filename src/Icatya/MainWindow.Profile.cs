using System.Windows;
using System.Windows.Controls;
using Icatya.Controls;
using Icatya.Services;

namespace Icatya;

public partial class MainWindow
{
    private readonly ProfilePreferencesStore _profilePreferences = new();
    private string _avatarId = AvatarCatalog.Default;
    private void InitializeProfile()
    {
        _avatarId = _profilePreferences.LoadAvatar();
        UpdateLocalAvatar();
        RenderAvatarChoices();
    }
    private void UpdateLocalAvatar()
    {
        FooterAvatar.Avatar = SelfAvatar.Avatar = _avatarId;
        AvatarSelectionStatus.Text = "Seu avatar fica salvo e aparece para os amigos da sala.";
    }
    private void RenderAvatarChoices()
    {
        AvatarChoices.Children.Clear();
        foreach (var option in AvatarCatalog.Available)
        {
            var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            content.Children.Add(new AvatarView { Avatar = option.Id, Width = 42, Height = 42, HorizontalAlignment = HorizontalAlignment.Center });
            content.Children.Add(new TextBlock { Text = option.Name, FontSize = 11, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 0) });
            var button = new Button { Tag = option.Id, Content = content, Width = 76, Height = 76, Padding = new Thickness(4), Margin = new Thickness(0, 0, 6, 6),
                Style = (Style)FindResource("Secondary"), ToolTip = "Escolher " + option.Name,
                BorderThickness = new Thickness(option.Id == _avatarId ? 2 : 1),
                BorderBrush = (System.Windows.Media.Brush)FindResource(option.Id == _avatarId ? "AccentBrush" : "BorderBrush") };
            System.Windows.Automation.AutomationProperties.SetName(button, "Avatar " + option.Name);
            button.Click += SelectAvatar;
            AvatarChoices.Children.Add(button);
        }
    }
    private void SelectAvatar(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id } || !AvatarCatalog.IsValid(id)) return;
        if (!_profilePreferences.SaveAvatar(id)) { AvatarSelectionStatus.Text = "Não foi possível salvar o avatar. Tente novamente."; return; }
        _avatarId = id;
        UpdateLocalAvatar();
        RenderAvatarChoices();
        _room?.SetAvatar(id);
        foreach (Button button in AvatarChoices.Children) if ((string)button.Tag == id) { button.Focus(); break; }
    }
}
