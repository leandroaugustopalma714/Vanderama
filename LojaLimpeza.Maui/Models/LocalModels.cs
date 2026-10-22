using SQLite;

namespace LojaLimpeza.Maui.Models;

public sealed class ClienteLocal
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string ExternalId { get; set; } = Guid.NewGuid().ToString("D");
    public int ApiId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
    public string TipoPessoa { get; set; } = "PF";
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public string Endereco { get; set; } = string.Empty;
    public string CriadoEmUtc { get; set; } = DateTime.UtcNow.ToString("O");
    public bool Sincronizado { get; set; }
}

public sealed class CategoriaLocal
{
    [PrimaryKey] public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
}

public sealed class ProdutoLocal
{
    [PrimaryKey] public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string UnidadeMedida { get; set; } = string.Empty;
    public long PrecoCentavos { get; set; }
    public int Estoque { get; set; }
    public int CategoriaId { get; set; }
    public string CategoriaNome { get; set; } = string.Empty;
    public bool Ativo { get; set; }
    public string AtualizadoEmUtc { get; set; } = string.Empty;
    [Ignore] public decimal PrecoUnitario => PrecoCentavos / 100m;
}

public sealed class PedidoLocal
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string ExternalId { get; set; } = Guid.NewGuid().ToString("D");
    public int ClienteLocalId { get; set; }
    public int ClienteApiId { get; set; }
    public string ClienteNome { get; set; } = string.Empty;
    public string CriadoEmUtc { get; set; } = DateTime.UtcNow.ToString("O");
    public string Status { get; set; } = "Pendente de sincronização";
    public long ValorTotalCentavos { get; set; }
    public bool Sincronizado { get; set; }
    public int ApiId { get; set; }
    [Ignore] public decimal ValorTotal => ValorTotalCentavos / 100m;
}

public sealed class ItemPedidoLocal
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public int PedidoLocalId { get; set; }
    public int ProdutoId { get; set; }
    public string CodigoProduto { get; set; } = string.Empty;
    public string NomeProduto { get; set; } = string.Empty;
    public string UnidadeMedida { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public long PrecoCentavos { get; set; }
    [Ignore] public decimal Subtotal => Quantidade * PrecoCentavos / 100m;
}
