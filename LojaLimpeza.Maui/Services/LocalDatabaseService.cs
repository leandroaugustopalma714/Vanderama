using LojaLimpeza.Maui.Models;
using SQLite;

namespace LojaLimpeza.Maui.Services;

public sealed class LocalDatabaseService(string databasePath)
{
    private readonly SQLiteAsyncConnection _database = new(databasePath);
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        await _initializationLock.WaitAsync();
        try
        {
            if (_initialized) return;
            await _database.CreateTableAsync<ClienteLocal>();
            await _database.CreateTableAsync<CategoriaLocal>();
            await _database.CreateTableAsync<ProdutoLocal>();
            await _database.CreateTableAsync<PedidoLocal>();
            await _database.CreateTableAsync<ItemPedidoLocal>();
            _initialized = true;
        }
        finally { _initializationLock.Release(); }
    }

    public async Task<ClienteLocal?> ObterClienteAsync(int id)
    {
        await InitializeAsync();
        return await _database.Table<ClienteLocal>().Where(item => item.Id == id).FirstOrDefaultAsync();
    }

    public async Task<ClienteLocal?> ObterClienteAtualAsync()
    {
        await InitializeAsync();
        var preferredId = Preferences.Get("ClienteLocalId", 0);
        if (preferredId > 0)
        {
            var selected = await _database.Table<ClienteLocal>().Where(item => item.Id == preferredId).FirstOrDefaultAsync();
            if (selected is not null) return selected;
        }
        return await _database.Table<ClienteLocal>().OrderBy(item => item.Id).FirstOrDefaultAsync();
    }

    public async Task SalvarClienteAsync(ClienteLocal cliente)
    {
        await InitializeAsync();
        if (cliente.Id == 0) await _database.InsertAsync(cliente);
        else await _database.UpdateAsync(cliente);
        Preferences.Set("ClienteLocalId", cliente.Id);
    }

    public async Task<IReadOnlyList<ClienteLocal>> ObterClientesPendentesAsync()
    {
        await InitializeAsync();
        return await _database.Table<ClienteLocal>().Where(item => !item.Sincronizado).ToListAsync();
    }

    public async Task MarcarClienteSincronizadoAsync(int localId, int apiId)
    {
        var cliente = await ObterClienteAsync(localId);
        if (cliente is null) return;
        cliente.ApiId = apiId;
        cliente.Sincronizado = true;
        await _database.UpdateAsync(cliente);
    }

    public async Task SalvarCatalogoAsync(IEnumerable<CategoriaLocal> categorias, IEnumerable<ProdutoLocal> produtos)
    {
        await InitializeAsync();
        var categoryList = categorias.ToArray();
        var productList = produtos.ToArray();
        await _database.RunInTransactionAsync(connection =>
        {
            foreach (var category in categoryList) connection.InsertOrReplace(category);
            foreach (var product in productList) connection.InsertOrReplace(product);
        });
    }

    public async Task<IReadOnlyList<CategoriaLocal>> ObterCategoriasAsync()
    {
        await InitializeAsync();
        return await _database.Table<CategoriaLocal>().OrderBy(item => item.Nome).ToListAsync();
    }

    public async Task<IReadOnlyList<ProdutoLocal>> ObterProdutosAsync(int? categoriaId = null)
    {
        await InitializeAsync();
        var produtos = await _database.Table<ProdutoLocal>().Where(item => item.Ativo).OrderBy(item => item.Nome).ToListAsync();
        return categoriaId.HasValue ? produtos.Where(item => item.CategoriaId == categoriaId.Value).ToArray() : produtos;
    }

    public async Task SalvarPedidoAsync(PedidoLocal pedido, IEnumerable<ItemPedidoLocal> itens)
    {
        await InitializeAsync();
        var itemList = itens.ToArray();
        if (itemList.Length == 0) throw new InvalidOperationException("Adicione pelo menos um produto ao pedido.");
        await _database.RunInTransactionAsync(connection =>
        {
            connection.Insert(pedido);
            foreach (var item in itemList)
            {
                item.PedidoLocalId = pedido.Id;
                connection.Insert(item);
            }
        });
    }

    public async Task<IReadOnlyList<PedidoLocal>> ObterPedidosAsync()
    {
        await InitializeAsync();
        return await _database.Table<PedidoLocal>().OrderByDescending(item => item.CriadoEmUtc).ToListAsync();
    }

    public async Task<IReadOnlyList<PedidoLocal>> ObterPedidosPendentesAsync()
    {
        await InitializeAsync();
        return await _database.Table<PedidoLocal>().Where(item => !item.Sincronizado).OrderBy(item => item.CriadoEmUtc).ToListAsync();
    }

    public async Task<IReadOnlyList<ItemPedidoLocal>> ObterItensPedidoAsync(int pedidoLocalId)
    {
        await InitializeAsync();
        return await _database.Table<ItemPedidoLocal>().Where(item => item.PedidoLocalId == pedidoLocalId).ToListAsync();
    }

    public async Task MarcarPedidoSincronizadoAsync(int localId, int apiId, string status, decimal total)
    {
        await InitializeAsync();
        var pedido = await _database.Table<PedidoLocal>().Where(item => item.Id == localId).FirstOrDefaultAsync();
        if (pedido is null) return;
        pedido.ApiId = apiId;
        pedido.Sincronizado = true;
        pedido.Status = status;
        pedido.ValorTotalCentavos = (long)decimal.Round(total * 100m, 0, MidpointRounding.AwayFromZero);
        await _database.UpdateAsync(pedido);
    }
}
