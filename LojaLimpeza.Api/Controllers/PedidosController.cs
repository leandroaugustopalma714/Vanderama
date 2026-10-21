using LojaLimpeza.Api.Data;
using LojaLimpeza.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LojaLimpeza.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PedidosController : ControllerBase
{
    private readonly LojaDbContext _context;

    public PedidosController(LojaDbContext context) => _context = context;

    [HttpGet] 
    public async Task<IActionResult> GetPedidos()
    {
        return Ok(await _context.Pedidos.Include(p => p.Itens).ToListAsync());
    }

    [HttpPost] 
    public async Task<IActionResult> PostPedido([FromBody] Pedido pedido)
    {
        _context.Pedidos.Add(pedido);
        await _context.SaveChangesAsync();
        return Ok(pedido);
    }
}