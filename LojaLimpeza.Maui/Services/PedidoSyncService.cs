using LojaLimpeza.Maui.Models;
using LojaLimpeza.Shared;

namespace LojaLimpeza.Maui.Services;

public sealed record ResultadoSincronizacao(int ClientesEnviados, int PedidosEnviados, bool CatalogoAtualizado, IReadOnlyList<string> Erros);

public sealed class PedidoSyncService(LocalDatabaseService database, LojaApiClient api)
{
    private static readonly TimeSpan IntervaloCatalogo = TimeSpan.FromMinutes(5);
    private readonly LocalDatabaseService _database = database;
    private readonly LojaApiClient _api = api;

    public async Task<ResultadoSincronizacao> SincronizarAsync(bool forcarCatalogo = false, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var clientsSynced = 0;
        var ordersSynced = 0;
        var catalogUpdated = false;

        try { catalogUpdated = await AtualizarCatalogoAsync(forcarCatalogo, ct); }
        catch (Exception exception) when (IsConnectionOrApiError(exception)) { errors.Add($"Catálogo: {exception.Message}"); }

        foreach (var client in await _database.ObterClientesPendentesAsync())
        {
            try
            {
                var saved = await _api.CriarClienteAsync(new CriarClienteRequest(
                    client.ExternalId, client.Nome, client.Documento, client.TipoPessoa,
                    client.Telefone, client.Email, client.Endereco), ct);
                if (saved is null) continue;
                await _database.MarcarClienteSincronizadoAsync(client.Id, saved.Id);
                clientsSynced++;
            }
            catch (Exception exception) when (IsConnectionOrApiError(exception)) { errors.Add($"Cliente: {exception.Message}"); }
        }

        foreach (var order in await _database.ObterPedidosPendentesAsync())
        {
            try
            {
                var customer = await _database.ObterClienteAsync(order.ClienteLocalId);
                if (customer is null || customer.ApiId <= 0) continue;
                var items = await _database.ObterItensPedidoAsync(order.Id);
                var request = new CriarPedidoRequest(order.ExternalId, customer.ApiId,
                    items.Select(item => new CriarItemPedidoRequest(item.ProdutoId, item.Quantidade)).ToArray());
                var saved = await _api.CriarPedidoAsync(request, ct);
                if (saved is null) continue;
                await _database.MarcarPedidoSincronizadoAsync(order.Id, saved.Id, saved.Status, saved.ValorTotal);
                ordersSynced++;
            }
            catch (Exception exception) when (IsConnectionOrApiError(exception)) { errors.Add($"Pedido {order.Id}: {exception.Message}"); }
        }

        return new(clientsSynced, ordersSynced, catalogUpdated, errors);
    }

    public async Task<bool> AtualizarCatalogoAsync(bool forcar, CancellationToken ct = default)
    {
        var lastUpdate = Preferences.Get("CatalogoAtualizadoUtc", string.Empty);
        if (!forcar && DateTime.TryParse(lastUpdate, null, System.Globalization.DateTimeStyles.RoundtripKind, out var last)
            && DateTime.UtcNow - last.ToUniversalTime() < IntervaloCatalogo)
            return false;

        var categories = await _api.GetCategoriasAsync(ct);
        var products = await _api.GetProdutosAsync(ct);
        if (categories is null || products is null) return false;

        await _database.SalvarCatalogoAsync(
            categories.Select(item => new CategoriaLocal { Id = item.Id, Nome = item.Nome }),
            products.Select(item => new ProdutoLocal
            {
                Id = item.Id,
                Codigo = item.Codigo,
                Nome = item.Nome,
                UnidadeMedida = item.UnidadeMedida,
                PrecoCentavos = (long)decimal.Round(item.PrecoUnitario * 100m, 0, MidpointRounding.AwayFromZero),
                Estoque = item.Estoque,
                CategoriaId = item.CategoriaId,
                CategoriaNome = item.CategoriaNome,
                Ativo = item.Ativo,
                AtualizadoEmUtc = item.AtualizadoEmUtc.ToString("O")
            }));
        Preferences.Set("CatalogoAtualizadoUtc", DateTime.UtcNow.ToString("O"));
        return true;
    }

    private static bool IsConnectionOrApiError(Exception exception) =>
        exception is HttpRequestException or TaskCanceledException or InvalidOperationException;
}
