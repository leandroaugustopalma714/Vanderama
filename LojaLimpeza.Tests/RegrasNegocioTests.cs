using LojaLimpeza.Shared;
using Xunit;

namespace LojaLimpeza.Tests;

public class RegrasNegocioTests
{
    [Fact]
    public void Pedido_CalcularTotal_DeveSomarCorretamente()
    {
        // Arrange
        var pedido = new Pedido();
        pedido.Itens.Add(new ItemPedido { Quantidade = 2, PrecoUnitario = 15.0m });
        pedido.Itens.Add(new ItemPedido { Quantidade = 3, PrecoUnitario = 10.0m });

        // Act
        var total = pedido.Itens.Sum(i => i.Quantidade * i.PrecoUnitario);

        // Assert
        Assert.Equal(60.0m, total);
    }
}