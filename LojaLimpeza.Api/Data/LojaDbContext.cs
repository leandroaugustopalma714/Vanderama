using LojaLimpeza.Shared;
using Microsoft.EntityFrameworkCore;

namespace LojaLimpeza.Api.Data;

public class LojaDbContext : DbContext
{
    public LojaDbContext(DbContextOptions<LojaDbContext> options) : base(options) { }

    public DbSet<Produto> Produtos { get; set; }
    public DbSet<Categoria> Categorias { get; set; }
    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Pedido> Pedidos { get; set; }
    public DbSet<ItemPedido> ItensPedido { get; set; }
}