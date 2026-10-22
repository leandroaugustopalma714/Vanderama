using LojaLimpeza.Shared;

namespace LojaLimpeza.Tests;

public sealed class RegrasNegocioTests
{
    [Fact]
    public void CalcularTotal_SomaSubtotaisComDuasCasas()
    {
        var itens = new[]
        {
            new ItemPedido { ProdutoId = 1, Quantidade = 2, PrecoUnitario = 15.00m },
            new ItemPedido { ProdutoId = 2, Quantidade = 3, PrecoUnitario = 10.00m }
        };

        Assert.Equal(60.00m, RegrasPedido.CalcularTotal(itens));
    }

    [Fact]
    public void CalcularSubtotal_ArredondaMeioCentavoParaCima()
    {
        Assert.Equal(1.01m, RegrasPedido.CalcularSubtotal(1, 1.005m));
    }

    [Theory]
    [InlineData(-1, 2.00)]
    [InlineData(1, -2.00)]
    public void CalcularSubtotal_RejeitaValoresNegativos(int quantidade, decimal preco)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RegrasPedido.CalcularSubtotal(quantidade, preco));
    }

    [Theory]
    [InlineData("529.982.247-25", "PF", true)]
    [InlineData("11.222.333/0001-81", "PJ", true)]
    [InlineData("111.111.111-11", "PF", false)]
    [InlineData("52998224725", "PJ", false)]
    public void DocumentoValidator_ValidaCpfECnpj(string documento, string tipo, bool esperado)
    {
        Assert.Equal(esperado, DocumentoValidator.EhValido(documento, tipo));
    }

    [Fact]
    public void DocumentoValidator_NormalizaApenasDigitosAscii()
    {
        Assert.Equal("52998224725", DocumentoValidator.Normalizar("CPF 529.982.247-25"));
        Assert.Equal(string.Empty, DocumentoValidator.Normalizar("١٢٣٤٥"));
    }

    [Fact]
    public void ValidarCliente_AceitaPessoaFisicaComCpfValido()
    {
        var request = new CriarClienteRequest(Guid.NewGuid().ToString(), "Maria da Silva", "52998224725", "PF", null, null, "Rua A, 10");

        Assert.Null(RegrasPedido.ValidarCliente(request));
    }

    [Fact]
    public void ValidarCliente_ExigeEndereco()
    {
        var request = new CriarClienteRequest(Guid.NewGuid().ToString(), "Maria da Silva", "52998224725", "PF", null, null, " ");

        Assert.Equal("Informe um endereço válido (até 240 caracteres).", RegrasPedido.ValidarCliente(request));
    }

    [Fact]
    public void ValidarPedido_RejeitaCarrinhoVazio()
    {
        var request = new CriarPedidoRequest(Guid.NewGuid().ToString(), 1, []);

        Assert.Equal("O pedido deve conter ao menos um produto.", RegrasPedido.ValidarPedido(request));
    }

    [Fact]
    public void ValidarPedido_RejeitaProdutoDuplicado()
    {
        var request = new CriarPedidoRequest(Guid.NewGuid().ToString(), 1,
        [
            new(10, 1),
            new(10, 2)
        ]);

        Assert.Equal("Remova produtos repetidos do pedido antes de enviar.", RegrasPedido.ValidarPedido(request));
    }

    [Fact]
    public void ValidarPedido_AceitaPedidoComClienteEItensValidos()
    {
        var request = new CriarPedidoRequest(Guid.NewGuid().ToString(), 1,
        [
            new(10, 2),
            new(11, 1)
        ]);

        Assert.Null(RegrasPedido.ValidarPedido(request));
    }
}
