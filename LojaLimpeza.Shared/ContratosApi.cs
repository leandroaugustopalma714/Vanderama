namespace LojaLimpeza.Shared;

public sealed record ClienteDto(
    int Id,
    string ExternalId,
    string Nome,
    string Documento,
    string TipoPessoa,
    string? Telefone,
    string? Email,
    string Endereco,
    DateTime CriadoEmUtc);

public sealed record CriarClienteRequest(
    string ExternalId,
    string Nome,
    string Documento,
    string TipoPessoa,
    string? Telefone,
    string? Email,
    string Endereco);

public sealed record CriarItemPedidoRequest(int ProdutoId, int Quantidade);

public sealed record CriarPedidoRequest(
    string ExternalId,
    int ClienteId,
    IReadOnlyList<CriarItemPedidoRequest> Itens);

public sealed record ItemPedidoDto(
    int ProdutoId,
    string CodigoProduto,
    string NomeProduto,
    string UnidadeMedida,
    int Quantidade,
    decimal PrecoUnitario,
    decimal Subtotal);

public sealed record PedidoDto(
    int Id,
    string ExternalId,
    int ClienteId,
    string ClienteNome,
    DateTime CriadoEmUtc,
    string Status,
    decimal ValorTotal,
    IReadOnlyList<ItemPedidoDto> Itens);
