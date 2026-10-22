using LojaLimpeza.Maui.Views;

namespace LojaLimpeza.Maui;

public partial class AppShell : Shell
{
    public AppShell(MainPage mainPage)
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(CadastroClientePage), typeof(CadastroClientePage));
        Routing.RegisterRoute(nameof(NovoPedidoPage), typeof(NovoPedidoPage));
        Items.Add(new ShellContent { Title = "Loja Limpeza", Route = "home", Content = mainPage });
    }
}
