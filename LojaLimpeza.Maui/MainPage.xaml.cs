using LojaLimpeza.Maui.Models;
using LojaLimpeza.Maui.Services;
using LojaLimpeza.Maui.Views;

namespace LojaLimpeza.Maui;

public partial class MainPage : ContentPage
{
    private readonly LocalDatabaseService _database;
    private readonly PedidoSyncService _sync;

    public MainPage(LocalDatabaseService database, PedidoSyncService sync)
    {
        InitializeComponent();
        _database = database;
        _sync = sync;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        await _database.InitializeAsync();
        var customer = await _database.ObterClienteAtualAsync();
        if (customer is null)
        {
            await Shell.Current.GoToAsync(nameof(CadastroClientePage));
            return;
        }

        LblCliente.Text = customer.Nome;
        LblDocumento.Text = $"{(customer.TipoPessoa == "PJ" ? "CNPJ" : "CPF")}: {customer.Documento}";

        var result = await _sync.SincronizarAsync();
        LblStatusApi.Text = result.Erros.Count == 0
            ? "Conectado. Cadastro, catálogo e pedidos sincronizados."
            : "Sem conexão com a API. Pedidos pendentes ficam salvos neste aparelho.";

        var orders = await _database.ObterPedidosAsync();
        ListaPedidos.ItemsSource = orders.Select(order => new PedidoResumo(
            $"Pedido #{order.Id:00000} · {DataLocal(order.CriadoEmUtc)}",
            order.Status,
            order.ValorTotal)).ToArray();
    }

    private async void OnNovoPedidoClicked(object? sender, EventArgs e)
    {
        var customer = await _database.ObterClienteAtualAsync();
        if (customer is null)
        {
            await Shell.Current.GoToAsync(nameof(CadastroClientePage));
            return;
        }
        await Shell.Current.GoToAsync(nameof(NovoPedidoPage));
    }

    private async void OnSincronizarClicked(object? sender, EventArgs e)
    {
        var result = await _sync.SincronizarAsync(forcarCatalogo: true);
        await CarregarAsync();
        var message = $"Clientes sincronizados: {result.ClientesEnviados}\nPedidos enviados: {result.PedidosEnviados}";
        if (result.Erros.Count > 0) message += $"\nPendências: {result.Erros.Count}\n{string.Join("\n", result.Erros.Take(2))}";
        await DisplayAlertAsync("Sincronização", message, "OK");
    }

    private async void OnRefresh(object? sender, EventArgs e)
    {
        try { await CarregarAsync(); }
        finally { PageRefresh.IsRefreshing = false; }
    }

    private static string DataLocal(string value) =>
        DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            : value;

    private sealed record PedidoResumo(string Titulo, string Status, decimal Valor);
}
