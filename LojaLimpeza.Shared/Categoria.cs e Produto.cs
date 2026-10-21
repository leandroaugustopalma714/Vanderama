namespace LojaLimpeza.Shared;

public class Categoria
{
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
}

public class Produto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal PrecoUnitario { get; set; }
    public string UnidadeMedida { get; set; } = string.Empty; // Ex: 5L, PET, Sachê
    public int CategoriaId { get; set; }
    public bool Ativo { get; set; } // Define se será exibido no app
}