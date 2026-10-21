using SQLite;
using LojaLimpeza.Shared;

namespace LojaLimpeza.Maui.Services;

public class LocalDatabaseService
{
    private readonly SQLiteAsyncConnection _db;

    public LocalDatabaseService(string dbPath)
    {
        _db = new SQLiteAsyncConnection(dbPath);
        _db.CreateTableAsync<Cliente>().Wait(); 
        _db.CreateTableAsync<Pedido>().Wait();
    }

    public async Task SalvarClienteAsync(Cliente cliente)
    {
        await _db.InsertAsync(cliente);
    }
}