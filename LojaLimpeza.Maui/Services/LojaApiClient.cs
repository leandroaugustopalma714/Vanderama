using System.Net.Http.Json;
using System.Text.Json;
using LojaLimpeza.Shared;

namespace LojaLimpeza.Maui.Services;

public sealed class LojaApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient = httpClient;

    public Task<IReadOnlyList<CategoriaDto>?> GetCategoriasAsync(CancellationToken ct = default) => GetAsync<IReadOnlyList<CategoriaDto>>("api/categorias", ct);
    public Task<IReadOnlyList<ProdutoDto>?> GetProdutosAsync(CancellationToken ct = default) => GetAsync<IReadOnlyList<ProdutoDto>>("api/produtos?incluirInativos=true", ct);
    public Task<ClienteDto?> CriarClienteAsync(CriarClienteRequest request, CancellationToken ct = default) => PostAsync<CriarClienteRequest, ClienteDto>("api/clientes", request, ct);
    public Task<PedidoDto?> CriarPedidoAsync(CriarPedidoRequest request, CancellationToken ct = default) => PostAsync<CriarPedidoRequest, PedidoDto>("api/pedidos", request, ct);

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(path, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
    }

    private async Task<TResponse?> PostAsync<TRequest, TResponse>(string path, TRequest request, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync(path, request, JsonOptions, ct);
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var content = await response.Content.ReadAsStringAsync(ct);
        string? message = null;
        try { message = JsonDocument.Parse(content).RootElement.TryGetProperty("erro", out var error) ? error.GetString() : null; }
        catch (JsonException) { }
        throw new InvalidOperationException(message ?? $"A API respondeu com HTTP {(int)response.StatusCode}.");
    }
}
