namespace LojaLimpeza.Shared;

public sealed record CategoriaDto(int Id, string Nome);

public sealed record ProdutoDto(
    int Id,
    string Codigo,
    string Nome,
    string UnidadeMedida,
    decimal PrecoUnitario,
    int Estoque,
    int CategoriaId,
    string CategoriaNome,
    bool Ativo,
    DateTime AtualizadoEmUtc);

public sealed record AtualizarProdutoRequest(decimal PrecoUnitario, int Estoque, bool Ativo);
