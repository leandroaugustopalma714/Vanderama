using LojaLimpeza.Maui.Models;
using LojaLimpeza.Maui.Services;
using LojaLimpeza.Shared;

namespace LojaLimpeza.Maui.Views;

public partial class CadastroClientePage : ContentPage
{
    private readonly LocalDatabaseService _database;
    private readonly PedidoSyncService _sync;
    private bool _saving;

    public CadastroClientePage(LocalDatabaseService database, PedidoSyncService sync)
    {
        InitializeComponent();
        _database = database;
        _sync = sync;
    }

    private async void OnSalvarClicked(object? sender, EventArgs e)
    {
        if (_saving) return;
        _saving = true;
        try
        {
            var tipo = PickerTipoPessoa.SelectedIndex == 1 ? "PJ" : "PF";
            var customer = new ClienteLocal
            {
                ExternalId = Guid.NewGuid().ToString("D"),
                Nome = EntryNome.Text?.Trim() ?? string.Empty,
                Documento = DocumentoValidator.Normalizar(EntryDocumento.Text),
                TipoPessoa = tipo,
                Telefone = EmptyToNull(EntryTelefone.Text),
                Email = EmptyToNull(EntryEmail.Text),
                Endereco = EntryEndereco.Text?.Trim() ?? string.Empty,
                CriadoEmUtc = DateTime.UtcNow.ToString("O")
            };
            var request = new CriarClienteRequest(customer.ExternalId, customer.Nome, customer.Documento,
                customer.TipoPessoa, customer.Telefone, customer.Email, customer.Endereco);
            var error = RegrasPedido.ValidarCliente(request);
            if (error is not null)
            {
                await DisplayAlertAsync("Confira o cadastro", error, "OK");
                return;
            }

            await _database.SalvarClienteAsync(customer);
            var result = await _sync.SincronizarAsync();
            var message = result.Erros.Count == 0
                ? "Cadastro salvo e sincronizado. Agora você já pode fazer seu pedido."
                : "Cadastro salvo no aparelho. Ele será sincronizado quando houver conexão com a loja.";
            await DisplayAlertAsync("Cadastro salvo", message, "Continuar");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception exception) when (exception is InvalidOperationException or SQLite.SQLiteException or HttpRequestException or TaskCanceledException)
        {
            await DisplayAlertAsync("Não foi possível salvar", exception.Message, "OK");
        }
        finally { _saving = false; }
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
