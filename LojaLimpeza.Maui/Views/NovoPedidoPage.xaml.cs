using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LojaLimpeza.Maui.Models;
using LojaLimpeza.Maui.Services;

namespace LojaLimpeza.Maui.Views;

public partial class NovoPedidoPage : ContentPage
{
    private readonly LocalDatabaseService _database;
    private readonly PedidoSyncService _sync;
    private readonly ObservableCollection<ProdutoLinha> _products = [];
    private readonly ObservableCollection<ItemCarrinho> _cart = [];
    private ClienteLocal? _customer;
    private bool _loading;

    public NovoPedidoPage(LocalDatabaseService database, PedidoSyncService sync)
    {
        InitializeComponent();
        _database = database;
        _sync = sync;
        ListaProdutos.ItemsSource = _products;
        ListaCarrinho.ItemsSource = _cart;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        _loading = true;
        await _database.InitializeAsync();
        _customer = await _database.ObterClienteAtualAsync();
        if (_customer is null)
        {
            await DisplayAlertAsync("Cadastro necessário", "Cadastre seus dados antes de fazer um pedido.", "OK");
            await Shell.Current.GoToAsync(nameof(CadastroClientePage));
            _loading = false;
            return;
        }
        LblCliente.Text = $"Pedido para {_customer.Nome}";

        var sync = await _sync.SincronizarAsync();
        LblStatusCatalogo.Text = sync.Erros.Count == 0
            ? "Catálogo atualizado. Preço e disponibilidade serão confirmados pela loja ao enviar."
            : "Exibindo o último catálogo salvo. O pedido será sincronizado quando houver conexão.";

        var categories = await _database.ObterCategoriasAsync();
        var categoryOptions = new[] { new CategoriaOpcao(0, "Todas as categorias") }
            .Concat(categories.Select(item => new CategoriaOpcao(item.Id, item.Nome))).ToArray();
        PickerCategoria.ItemsSource = categoryOptions;
        PickerCategoria.SelectedIndex = 0;
        await FiltrarProdutosAsync();
        _loading = false;
    }

    private async Task FiltrarProdutosAsync()
    {
        var categoryId = PickerCategoria.SelectedItem is CategoriaOpcao category && category.Id > 0 ? category.Id : (int?)null;
        var products = await _database.ObterProdutosAsync(categoryId);
        _products.Clear();
        foreach (var product in products) _products.Add(new ProdutoLinha(product));
    }

    private async void OnCategoriaChanged(object? sender, EventArgs e)
    {
        if (!_loading) await FiltrarProdutosAsync();
    }

    private async void OnAdicionarItemClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: ProdutoLinha line }) return;
        if (!int.TryParse(line.QuantidadeTexto, out var quantity) || quantity is < 1 or > 9999)
        {
            await DisplayAlertAsync("Quantidade inválida", "Informe uma quantidade inteira entre 1 e 9.999.", "OK");
            return;
        }
        var item = _cart.FirstOrDefault(current => current.Produto.Id == line.Produto.Id);
        if (item is null) _cart.Add(new ItemCarrinho(line.Produto, quantity));
        else item.Adicionar(quantity);
        line.QuantidadeTexto = string.Empty;
        AtualizarTotal();
    }

    private void OnRemoverItemClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: ItemCarrinho item }) _cart.Remove(item);
        AtualizarTotal();
    }

    private void AtualizarTotal() =>
        LblTotal.Text = $"Total: {_cart.Sum(item => item.Subtotal).ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))}";

    private async void OnSalvarPedidoClicked(object? sender, EventArgs e)
    {
        if (_customer is null)
        {
            await DisplayAlertAsync("Cliente", "Cadastre seus dados antes de pedir.", "OK");
            return;
        }
        if (_cart.Count == 0)
        {
            await DisplayAlertAsync("Carrinho vazio", "Adicione ao menos um produto ao pedido.", "OK");
            return;
        }

        var total = decimal.Round(_cart.Sum(item => item.Subtotal), 2, MidpointRounding.AwayFromZero);
        var order = new PedidoLocal
        {
            ExternalId = Guid.NewGuid().ToString("D"),
            ClienteLocalId = _customer.Id,
            ClienteApiId = _customer.ApiId,
            ClienteNome = _customer.Nome,
            CriadoEmUtc = DateTime.UtcNow.ToString("O"),
            Status = "Pendente de sincronização",
            ValorTotalCentavos = (long)decimal.Round(total * 100m, 0, MidpointRounding.AwayFromZero)
        };
        var lines = _cart.Select(item => new ItemPedidoLocal
        {
            ProdutoId = item.Produto.Id,
            CodigoProduto = item.Produto.Codigo,
            NomeProduto = item.Produto.Nome,
            UnidadeMedida = item.Produto.UnidadeMedida,
            Quantidade = item.Quantidade,
            PrecoCentavos = item.Produto.PrecoCentavos
        }).ToArray();

        try
        {
            await _database.SalvarPedidoAsync(order, lines);
            await _sync.SincronizarAsync();
            var saved = (await _database.ObterPedidosAsync()).First(item => item.Id == order.Id);
            var message = saved.Sincronizado
                ? "Pedido enviado para a loja. O preço e o estoque foram confirmados pela API."
                : "Pedido salvo neste aparelho e aguardando conexão para ser enviado à loja.";
            await DisplayAlertAsync("Pedido salvo", message, "Concluir");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception exception) when (exception is InvalidOperationException or SQLite.SQLiteException)
        {
            await DisplayAlertAsync("Não foi possível salvar o pedido", exception.Message, "OK");
        }
    }

    private sealed record CategoriaOpcao(int Id, string Nome);

    private sealed class ProdutoLinha(ProdutoLocal product)
    {
        public ProdutoLocal Produto { get; } = product;
        public string Nome => Produto.Nome;
        public string Detalhe => $"{Produto.Codigo} • {Produto.UnidadeMedida} • estoque {Produto.Estoque}";
        public string PrecoTexto => Produto.PrecoUnitario.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        public string QuantidadeTexto { get; set; } = string.Empty;
    }

    private sealed class ItemCarrinho(ProdutoLocal product, int quantity) : INotifyPropertyChanged
    {
        public ProdutoLocal Produto { get; } = product;
        private int _quantidade = quantity;
        public int Quantidade
        {
            get => _quantidade;
            private set
            {
                _quantidade = value;
                OnPropertyChanged(nameof(Quantidade));
                OnPropertyChanged(nameof(Nome));
                OnPropertyChanged(nameof(Subtotal));
                OnPropertyChanged(nameof(SubtotalTexto));
            }
        }
        public string Nome => $"{Quantidade} × {Produto.Nome}";
        public string Unidade => Produto.UnidadeMedida;
        public decimal Subtotal => decimal.Round(Quantidade * Produto.PrecoUnitario, 2, MidpointRounding.AwayFromZero);
        public string SubtotalTexto => Subtotal.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        public void Adicionar(int quantity) => Quantidade = checked(Quantidade + quantity);

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
