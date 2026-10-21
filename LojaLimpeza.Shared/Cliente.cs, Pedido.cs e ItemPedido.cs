namespace LojaLimpeza.Shared;

public class Cliente
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty; // CPF/CNPJ
    public string TipoPessoa { get; set; } = string.Empty;
}

public class Pedido
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public DateTime DataCriacao { get; set; } = DateTime.Now;
    public decimal ValorTotal { get; set; }
    public List<ItemPedido> Itens { get; set; } = new();
}

public class ItemPedido
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    public int ProdutoId { get; set; }
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
}