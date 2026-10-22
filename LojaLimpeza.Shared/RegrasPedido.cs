namespace LojaLimpeza.Shared;

public sealed class ItemPedido
{
    public int ProdutoId { get; set; }
    public string NomeProduto { get; set; } = string.Empty;
    public string UnidadeMedida { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
}

public static class RegrasPedido
{
    public static decimal CalcularSubtotal(int quantidade, decimal precoUnitario)
    {
        if (quantidade < 0)
            throw new ArgumentOutOfRangeException(nameof(quantidade), "A quantidade não pode ser negativa.");
        if (precoUnitario < 0)
            throw new ArgumentOutOfRangeException(nameof(precoUnitario), "O preço não pode ser negativo.");

        return decimal.Round(quantidade * precoUnitario, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal CalcularTotal(IEnumerable<ItemPedido> itens)
    {
        ArgumentNullException.ThrowIfNull(itens);
        return decimal.Round(itens.Sum(item => CalcularSubtotal(item.Quantidade, item.PrecoUnitario)), 2, MidpointRounding.AwayFromZero);
    }

    public static string? ValidarCliente(CriarClienteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ExternalId) || !Guid.TryParse(request.ExternalId, out _))
            return "Identificador local do cliente inválido.";
        if (string.IsNullOrWhiteSpace(request.Nome) || request.Nome.Trim().Length > 160)
            return "Informe o nome do cliente (até 160 caracteres).";
        if (!DocumentoValidator.EhValido(request.Documento, request.TipoPessoa))
            return "CPF ou CNPJ inválido para o tipo de pessoa informado.";
        if (string.IsNullOrWhiteSpace(request.Endereco) || request.Endereco.Trim().Length > 240)
            return "Informe um endereço válido (até 240 caracteres).";
        if (request.Telefone?.Length > 30)
            return "O telefone deve ter no máximo 30 caracteres.";
        if (request.Email?.Length > 160)
            return "O e-mail deve ter no máximo 160 caracteres.";
        return null;
    }

    public static string? ValidarPedido(CriarPedidoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ExternalId) || !Guid.TryParse(request.ExternalId, out _))
            return "Identificador local do pedido inválido.";
        if (request.ClienteId <= 0)
            return "O pedido deve estar vinculado a um cliente cadastrado.";
        if (request.Itens is null || request.Itens.Count == 0)
            return "O pedido deve conter ao menos um produto.";
        if (request.Itens.Any(item => item.ProdutoId <= 0 || item.Quantidade is < 1 or > 9999))
            return "Cada item deve ter um produto válido e quantidade entre 1 e 9.999.";
        if (request.Itens.Select(item => item.ProdutoId).Distinct().Count() != request.Itens.Count)
            return "Remova produtos repetidos do pedido antes de enviar.";
        return null;
    }
}
