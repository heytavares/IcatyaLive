using System;
using System.Windows;
using Icatya.Services.Rooms;

namespace Icatya;
public partial class RoomJoinWindow : Window
{
    public RoomIdentity? Identity { get; private set; }
    private bool _creating;
    private readonly RoomIdentity? _previous;
    public RoomJoinWindow(RoomIdentity? previous, bool create = false, bool preserveCode = false)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Services.Windowing.DarkTitleBar.Attach(this);
        _previous = previous;
        Nick.Text = previous?.Nick ?? "";
        Code.Text = preserveCode ? previous?.Code ?? "" : "";
        if (create) CreateRoom(this, new RoutedEventArgs());
        Loaded += (_, _) => { if (Nick.Text.Length == 0) Nick.Focus(); else if (_creating) RoomName.Focus(); else Code.Focus(); };
    }
    private void CreateRoom(object sender, RoutedEventArgs e)
    {
        Code.Text = RoomIdentity.CreateCode();
        _creating = true;
        NamePanel.Visibility = Visibility.Visible;
        Code.IsReadOnly = true;
        Title = "Criar sala · iCatya";
        Error.Text = "Novo convite criado. Entre e copie o convite na barra lateral.";
    }
    private void Join(object sender, RoutedEventArgs e)
    {
        try
        {
            var code = RoomIdentity.NormalizeCode(Code.Text);
            Identity = new(code, RoomIdentity.NormalizeNick(Nick.Text), _creating ? RoomIdentity.NormalizeName(RoomName.Text) :
                _previous?.Code == code ? _previous.Name : "Sala de amigos");
            DialogResult = true;
        }
        catch (InvalidOperationException ex) { Error.Text = ex.Message; }
    }
}
