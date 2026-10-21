using LojaLimpeza.Shared;
using System.Net.Http.Json;

namespace LojaLimpeza.Maui.Views;

public partial class NovoPedidoPage : ContentPage
{
    private Pedido _carrinhoAtual = new Pedido();
    private HttpClient _client = new HttpClient { BaseAddress = new Uri("http://10.0.2.2:5000/api/") };

    public NovoPedidoPage()
    {
        InitializeComponent();
        
        // Mock rápido para você ver a tela funcionando antes da API estar 100% conectada
        ListaProdutos.ItemsSource = new List<Produto>
        {
            new Produto { Id = 1, Nome = "Detergente Neutro", PrecoUnitario = 2.50m, UnidadeMedida = "500ml" },
            new Produto { Id = 2, Nome = "Sabão em Pó", PrecoUnitario = 15.90m, UnidadeMedida = "1Kg" }
        };
    }

    // O sinal de interrogação (?) em object? resolve o aviso CS8622
    private void OnAddCarrinhoClicked(object? sender, EventArgs e)
    {
        // Validação de nulos para resolver os avisos CS8602
        if (sender is not Button button || button.CommandParameter is not Produto produto) 
            return;
        
        _carrinhoAtual.Itens.Add(new ItemPedido 
        { 
            ProdutoId = produto.Id, 
            PrecoUnitario = produto.PrecoUnitario, 
            Quantidade = 1 
        });

        _carrinhoAtual.ValorTotal = _carrinhoAtual.Itens.Sum(i => i.PrecoUnitario * i.Quantidade);
        LblTotal.Text = $"Total: R$ {_carrinhoAtual.ValorTotal:F2}";
    }

    private async void OnFinalizarPedidoClicked(object? sender, EventArgs e)
    {
        try
        {
            var response = await _client.PostAsJsonAsync("pedidos", _carrinhoAtual);
            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("Sucesso", "Pedido enviado!", "OK");
                _carrinhoAtual = new Pedido(); // Limpa o carrinho
                LblTotal.Text = "Total: R$ 0,00";
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Aviso", "Não foi possível conectar na API. O servidor está rodando?", "OK");
        }
    }
}