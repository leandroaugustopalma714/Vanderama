using LojaLimpeza.Api.Data;
using LojaLimpeza.Shared;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection para conectar ao MySQL.");

builder.Services.AddSingleton(new LojaRepository(connectionString));
builder.Services.AddRazorPages();

var app = builder.Build();
await app.Services.GetRequiredService<LojaRepository>().InitializeAsync();

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", timestampUtc = DateTime.UtcNow }));

app.MapGet("/api/categorias", async (LojaRepository repository, CancellationToken ct) =>
    Results.Ok(await repository.GetCategoriasAsync(ct)));

app.MapGet("/api/produtos", async (bool incluirInativos, LojaRepository repository, CancellationToken ct) =>
    Results.Ok(await repository.GetProdutosAsync(incluirInativos, ct)));

app.MapPut("/api/produtos/{id:int}", async (int id, AtualizarProdutoRequest request, LojaRepository repository, CancellationToken ct) =>
{
    try { return Results.Ok(await repository.AtualizarProdutoAsync(id, request, ct)); }
    catch (ArgumentException exception) { return Results.BadRequest(new { erro = exception.Message }); }
    catch (KeyNotFoundException exception) { return Results.NotFound(new { erro = exception.Message }); }
});

app.MapGet("/api/clientes", async (LojaRepository repository, CancellationToken ct) =>
    Results.Ok(await repository.GetClientesAsync(ct)));

app.MapPost("/api/clientes", async (CriarClienteRequest request, LojaRepository repository, CancellationToken ct) =>
{
    try
    {
        var customer = await repository.CriarClienteAsync(request, ct);
        return Results.Created($"/api/clientes/{customer.Id}", customer);
    }
    catch (ArgumentException exception) { return Results.BadRequest(new { erro = exception.Message }); }
    catch (InvalidOperationException exception) { return Results.Conflict(new { erro = exception.Message }); }
});

app.MapGet("/api/pedidos", async (LojaRepository repository, CancellationToken ct) =>
    Results.Ok(await repository.GetPedidosAsync(ct)));

app.MapPost("/api/pedidos", async (CriarPedidoRequest request, LojaRepository repository, CancellationToken ct) =>
{
    try
    {
        var order = await repository.CriarPedidoAsync(request, ct);
        return Results.Created($"/api/pedidos/{order.Id}", order);
    }
    catch (ArgumentException exception) { return Results.BadRequest(new { erro = exception.Message }); }
    catch (InvalidOperationException exception) { return Results.Conflict(new { erro = exception.Message }); }
});

app.Run();

public partial class Program { }
