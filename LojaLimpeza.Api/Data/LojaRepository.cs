using LojaLimpeza.Shared;
using MySqlConnector;

namespace LojaLimpeza.Api.Data;

public sealed class LojaRepository(string connectionString)
{
    private readonly string _connectionString = connectionString;
    private readonly SemaphoreSlim _initializeLock = new(1, 1);
    private bool _initialized;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;
        await _initializeLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;
            await using var connection = await OpenAsync(ct);
            string[] statements =
            [
                """CREATE TABLE IF NOT EXISTS Categorias (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, nome VARCHAR(100) NOT NULL UNIQUE) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4""",
                """CREATE TABLE IF NOT EXISTS Clientes (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, external_id CHAR(36) NOT NULL UNIQUE, nome VARCHAR(160) NOT NULL, documento VARCHAR(14) NOT NULL UNIQUE, tipo_pessoa CHAR(2) NOT NULL, telefone VARCHAR(30) NULL, email VARCHAR(160) NULL, endereco VARCHAR(240) NOT NULL, criado_em_utc DATETIME(6) NOT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4""",
                """CREATE TABLE IF NOT EXISTS Produtos (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, codigo VARCHAR(30) NOT NULL UNIQUE, nome VARCHAR(160) NOT NULL, unidade_medida VARCHAR(40) NOT NULL, preco_unitario DECIMAL(10,2) NOT NULL, estoque INT NOT NULL DEFAULT 0, categoria_id INT NOT NULL, ativo BOOLEAN NOT NULL DEFAULT TRUE, atualizado_em_utc DATETIME(6) NOT NULL, CONSTRAINT fk_produtos_categoria FOREIGN KEY (categoria_id) REFERENCES Categorias(id)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4""",
                """CREATE TABLE IF NOT EXISTS Pedidos (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, external_id CHAR(36) NOT NULL UNIQUE, cliente_id INT NOT NULL, criado_em_utc DATETIME(6) NOT NULL, status VARCHAR(30) NOT NULL, valor_total DECIMAL(12,2) NOT NULL, CONSTRAINT fk_pedidos_cliente FOREIGN KEY (cliente_id) REFERENCES Clientes(id), INDEX ix_pedidos_criado (criado_em_utc)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4""",
                """CREATE TABLE IF NOT EXISTS ItensPedido (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, pedido_id INT NOT NULL, produto_id INT NOT NULL, codigo_produto VARCHAR(30) NOT NULL, nome_produto VARCHAR(160) NOT NULL, unidade_medida VARCHAR(40) NOT NULL, quantidade INT NOT NULL, preco_unitario DECIMAL(10,2) NOT NULL, CONSTRAINT fk_itens_pedido FOREIGN KEY (pedido_id) REFERENCES Pedidos(id) ON DELETE CASCADE, CONSTRAINT fk_itens_produto FOREIGN KEY (produto_id) REFERENCES Produtos(id), INDEX ix_itens_pedido (pedido_id)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4"""
            ];

            foreach (var sql in statements)
            {
                await using var command = new MySqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync(ct);
            }
            await SeedAsync(connection, ct);
            _initialized = true;
        }
        finally { _initializeLock.Release(); }
    }

    public async Task<IReadOnlyList<CategoriaDto>> GetCategoriasAsync(CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = new MySqlCommand("SELECT id, nome FROM Categorias ORDER BY nome", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<CategoriaDto>();
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetInt32(0), reader.GetString(1)));
        return result;
    }

    public async Task<IReadOnlyList<ProdutoDto>> GetProdutosAsync(bool incluirInativos, CancellationToken ct)
    {
        const string sql = """
            SELECT p.id, p.codigo, p.nome, p.unidade_medida, p.preco_unitario, p.estoque,
                   p.categoria_id, c.nome, p.ativo, p.atualizado_em_utc
            FROM Produtos p INNER JOIN Categorias c ON c.id = p.categoria_id
            WHERE (@includeInactive = TRUE OR p.ativo = TRUE)
            ORDER BY c.nome, p.nome
            """;
        await using var connection = await OpenAsync(ct);
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@includeInactive", incluirInativos);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<ProdutoDto>();
        while (await reader.ReadAsync(ct)) result.Add(ReadProduto(reader));
        return result;
    }

    public async Task<IReadOnlyList<ClienteDto>> GetClientesAsync(CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = new MySqlCommand("SELECT id, external_id, nome, documento, tipo_pessoa, telefone, email, endereco, criado_em_utc FROM Clientes ORDER BY nome", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<ClienteDto>();
        while (await reader.ReadAsync(ct)) result.Add(ReadCliente(reader));
        return result;
    }

    public async Task<IReadOnlyList<PedidoDto>> GetPedidosAsync(CancellationToken ct)
    {
        var orders = new List<(int Id, string ExternalId, int ClientId, string ClientName, DateTime Created, string Status, decimal Total)>();
        await using (var connection = await OpenAsync(ct))
        await using (var command = new MySqlCommand("""
            SELECT p.id, p.external_id, p.cliente_id, c.nome, p.criado_em_utc, p.status, p.valor_total
            FROM Pedidos p INNER JOIN Clientes c ON c.id = p.cliente_id
            ORDER BY p.criado_em_utc DESC, p.id DESC
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                orders.Add((reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3), AsUtc(reader.GetDateTime(4)), reader.GetString(5), reader.GetDecimal(6)));
        }

        var result = new List<PedidoDto>(orders.Count);
        foreach (var order in orders)
        {
            var items = await GetItensPedidoAsync(order.Id, ct);
            result.Add(new(order.Id, order.ExternalId, order.ClientId, order.ClientName, order.Created, order.Status, order.Total, items));
        }
        return result;
    }

    public async Task<ClienteDto> CriarClienteAsync(CriarClienteRequest request, CancellationToken ct)
    {
        var validation = RegrasPedido.ValidarCliente(request);
        if (validation is not null) throw new ArgumentException(validation);
        var externalId = Guid.Parse(request.ExternalId).ToString("D");
        var document = DocumentoValidator.Normalizar(request.Documento);
        await using var connection = await OpenAsync(ct);
        try
        {
            await using var command = new MySqlCommand("""
                INSERT INTO Clientes (external_id, nome, documento, tipo_pessoa, telefone, email, endereco, criado_em_utc)
                VALUES (@externalId, @nome, @documento, @tipo, @telefone, @email, @endereco, @created)
                """, connection);
            command.Parameters.AddWithValue("@externalId", externalId);
            command.Parameters.AddWithValue("@nome", request.Nome.Trim());
            command.Parameters.AddWithValue("@documento", document);
            command.Parameters.AddWithValue("@tipo", request.TipoPessoa.Trim().ToUpperInvariant());
            command.Parameters.AddWithValue("@telefone", (object?)request.Telefone?.Trim() ?? DBNull.Value);
            command.Parameters.AddWithValue("@email", (object?)request.Email?.Trim() ?? DBNull.Value);
            command.Parameters.AddWithValue("@endereco", request.Endereco.Trim());
            command.Parameters.AddWithValue("@created", DateTime.UtcNow);
            await command.ExecuteNonQueryAsync(ct);
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            var existing = await GetClientePorExternalIdAsync(connection, externalId, ct);
            if (existing is not null) return existing;
            throw new InvalidOperationException("Já existe um cliente cadastrado com este CPF/CNPJ.");
        }
        return (await GetClientePorExternalIdAsync(connection, externalId, ct))!;
    }

    public async Task<PedidoDto> CriarPedidoAsync(CriarPedidoRequest request, CancellationToken ct)
    {
        var validation = RegrasPedido.ValidarPedido(request);
        if (validation is not null) throw new ArgumentException(validation);
        var externalId = Guid.Parse(request.ExternalId).ToString("D");

        var existing = await GetPedidoPorExternalIdAsync(externalId, ct);
        if (existing is not null) return existing;

        await using var connection = await OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            await using (var clientCommand = new MySqlCommand("SELECT COUNT(*) FROM Clientes WHERE id = @id", connection, transaction))
            {
                clientCommand.Parameters.AddWithValue("@id", request.ClienteId);
                if (Convert.ToInt32(await clientCommand.ExecuteScalarAsync(ct)) == 0)
                    throw new ArgumentException("Cliente não encontrado. Sincronize o cadastro antes de enviar o pedido.");
            }

            var orderItems = new List<ItemPedidoDto>();
            foreach (var requested in request.Itens.OrderBy(item => item.ProdutoId))
            {
                await using var productCommand = new MySqlCommand("""
                    SELECT id, codigo, nome, unidade_medida, preco_unitario, estoque, ativo
                    FROM Produtos WHERE id = @id FOR UPDATE
                    """, connection, transaction);
                productCommand.Parameters.AddWithValue("@id", requested.ProdutoId);
                await using var reader = await productCommand.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct)) throw new ArgumentException($"Produto {requested.ProdutoId} não encontrado.");
                var productId = reader.GetInt32(0);
                var code = reader.GetString(1);
                var name = reader.GetString(2);
                var unit = reader.GetString(3);
                var price = reader.GetDecimal(4);
                var stock = reader.GetInt32(5);
                var active = reader.GetBoolean(6);
                await reader.CloseAsync();

                if (!active) throw new InvalidOperationException($"O produto {name} está inativo e não pode ser comprado.");
                if (stock < requested.Quantidade) throw new InvalidOperationException($"Estoque insuficiente para {name}. Disponível: {stock} {unit}.");
                orderItems.Add(new(productId, code, name, unit, requested.Quantidade, price, RegrasPedido.CalcularSubtotal(requested.Quantidade, price)));
            }

            var total = decimal.Round(orderItems.Sum(item => item.Subtotal), 2, MidpointRounding.AwayFromZero);
            var created = DateTime.UtcNow;
            int orderId;
            await using (var orderCommand = new MySqlCommand("""
                INSERT INTO Pedidos (external_id, cliente_id, criado_em_utc, status, valor_total)
                VALUES (@externalId, @clientId, @created, 'Recebido', @total)
                """, connection, transaction))
            {
                orderCommand.Parameters.AddWithValue("@externalId", externalId);
                orderCommand.Parameters.AddWithValue("@clientId", request.ClienteId);
                orderCommand.Parameters.AddWithValue("@created", created);
                orderCommand.Parameters.AddWithValue("@total", total);
                await orderCommand.ExecuteNonQueryAsync(ct);
                orderId = (int)orderCommand.LastInsertedId;
            }

            foreach (var item in orderItems)
            {
                await using var itemCommand = new MySqlCommand("""
                    INSERT INTO ItensPedido (pedido_id, produto_id, codigo_produto, nome_produto, unidade_medida, quantidade, preco_unitario)
                    VALUES (@orderId, @productId, @code, @name, @unit, @quantity, @price)
                    """, connection, transaction);
                itemCommand.Parameters.AddWithValue("@orderId", orderId);
                itemCommand.Parameters.AddWithValue("@productId", item.ProdutoId);
                itemCommand.Parameters.AddWithValue("@code", item.CodigoProduto);
                itemCommand.Parameters.AddWithValue("@name", item.NomeProduto);
                itemCommand.Parameters.AddWithValue("@unit", item.UnidadeMedida);
                itemCommand.Parameters.AddWithValue("@quantity", item.Quantidade);
                itemCommand.Parameters.AddWithValue("@price", item.PrecoUnitario);
                await itemCommand.ExecuteNonQueryAsync(ct);

                await using var stockCommand = new MySqlCommand("UPDATE Produtos SET estoque = estoque - @quantity, atualizado_em_utc = @updated WHERE id = @id", connection, transaction);
                stockCommand.Parameters.AddWithValue("@quantity", item.Quantidade);
                stockCommand.Parameters.AddWithValue("@updated", created);
                stockCommand.Parameters.AddWithValue("@id", item.ProdutoId);
                await stockCommand.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
            return (await GetPedidoAsync(orderId, ct))!;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            var duplicate = await GetPedidoPorExternalIdAsync(externalId, ct);
            if (duplicate is not null) return duplicate;
            throw;
        }
    }

    public async Task<ProdutoDto> AtualizarProdutoAsync(int id, AtualizarProdutoRequest request, CancellationToken ct)
    {
        if (request.PrecoUnitario < 0 || request.PrecoUnitario > 99999999m)
            throw new ArgumentException("O preço deve ser maior ou igual a zero.");
        if (request.Estoque < 0) throw new ArgumentException("O estoque não pode ser negativo.");

        await using var connection = await OpenAsync(ct);
        await using var command = new MySqlCommand("UPDATE Produtos SET preco_unitario = @price, estoque = @stock, ativo = @active, atualizado_em_utc = @updated WHERE id = @id", connection);
        command.Parameters.AddWithValue("@price", request.PrecoUnitario);
        command.Parameters.AddWithValue("@stock", request.Estoque);
        command.Parameters.AddWithValue("@active", request.Ativo);
        command.Parameters.AddWithValue("@updated", DateTime.UtcNow);
        command.Parameters.AddWithValue("@id", id);
        if (await command.ExecuteNonQueryAsync(ct) == 0) throw new KeyNotFoundException("Produto não encontrado.");
        return (await GetProdutoAsync(id, ct))!;
    }

    private async Task<PedidoDto?> GetPedidoAsync(int id, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = new MySqlCommand("""
            SELECT p.id, p.external_id, p.cliente_id, c.nome, p.criado_em_utc, p.status, p.valor_total
            FROM Pedidos p INNER JOIN Clientes c ON c.id = p.cliente_id WHERE p.id = @id
            """, connection);
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var result = (reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3), AsUtc(reader.GetDateTime(4)), reader.GetString(5), reader.GetDecimal(6));
        await reader.CloseAsync();
        var items = await GetItensPedidoAsync(result.Item1, ct);
        return new(result.Item1, result.Item2, result.Item3, result.Item4, result.Item5, result.Item6, result.Item7, items);
    }

    private async Task<PedidoDto?> GetPedidoPorExternalIdAsync(string externalId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = new MySqlCommand("SELECT id FROM Pedidos WHERE external_id = @id", connection);
        command.Parameters.AddWithValue("@id", externalId);
        var id = await command.ExecuteScalarAsync(ct);
        return id is null ? null : await GetPedidoAsync(Convert.ToInt32(id), ct);
    }

    private async Task<IReadOnlyList<ItemPedidoDto>> GetItensPedidoAsync(int orderId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = new MySqlCommand("""
            SELECT produto_id, codigo_produto, nome_produto, unidade_medida, quantidade, preco_unitario
            FROM ItensPedido WHERE pedido_id = @id ORDER BY id
            """, connection);
        command.Parameters.AddWithValue("@id", orderId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<ItemPedidoDto>();
        while (await reader.ReadAsync(ct))
        {
            var quantity = reader.GetInt32(4);
            var price = reader.GetDecimal(5);
            result.Add(new(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), quantity, price, RegrasPedido.CalcularSubtotal(quantity, price)));
        }
        return result;
    }

    private async Task<ClienteDto?> GetClientePorExternalIdAsync(MySqlConnection connection, string externalId, CancellationToken ct)
    {
        await using var command = new MySqlCommand("SELECT id, external_id, nome, documento, tipo_pessoa, telefone, email, endereco, criado_em_utc FROM Clientes WHERE external_id = @id", connection);
        command.Parameters.AddWithValue("@id", externalId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadCliente(reader) : null;
    }

    private async Task<ProdutoDto?> GetProdutoAsync(int id, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = new MySqlCommand("""
            SELECT p.id, p.codigo, p.nome, p.unidade_medida, p.preco_unitario, p.estoque, p.categoria_id, c.nome, p.ativo, p.atualizado_em_utc
            FROM Produtos p INNER JOIN Categorias c ON c.id = p.categoria_id WHERE p.id = @id
            """, connection);
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadProduto(reader) : null;
    }

    private async Task SeedAsync(MySqlConnection connection, CancellationToken ct)
    {
        string[] categories = ["Detergentes", "Desinfetantes", "Lavanderia", "Acessórios"];
        foreach (var category in categories)
        {
            await using var command = new MySqlCommand("INSERT IGNORE INTO Categorias (nome) VALUES (@name)", connection);
            command.Parameters.AddWithValue("@name", category);
            await command.ExecuteNonQueryAsync(ct);
        }

        (string Code, string Name, string Unit, decimal Price, int Stock, string Category)[] products =
        [
            ("DET-500", "Detergente neutro 500 ml", "unidade", 2.49m, 120, "Detergentes"),
            ("DET-5L", "Detergente neutro 5 L", "galão", 18.90m, 35, "Detergentes"),
            ("DES-2L", "Desinfetante floral 2 L", "unidade", 8.75m, 60, "Desinfetantes"),
            ("AGUA-5L", "Água sanitária 5 L", "galão", 12.50m, 45, "Desinfetantes"),
            ("SAB-1K", "Sabão em pó 1 kg", "pacote", 14.99m, 50, "Lavanderia"),
            ("AMAC-2L", "Amaciante 2 L", "unidade", 11.90m, 40, "Lavanderia"),
            ("ESP-01", "Esponja multiuso", "unidade", 1.75m, 100, "Acessórios")
        ];
        foreach (var product in products)
        {
            await using var command = new MySqlCommand("""
                INSERT IGNORE INTO Produtos (codigo, nome, unidade_medida, preco_unitario, estoque, categoria_id, ativo, atualizado_em_utc)
                SELECT @code, @name, @unit, @price, @stock, id, TRUE, UTC_TIMESTAMP(6) FROM Categorias WHERE nome = @category
                """, connection);
            command.Parameters.AddWithValue("@code", product.Code);
            command.Parameters.AddWithValue("@name", product.Name);
            command.Parameters.AddWithValue("@unit", product.Unit);
            command.Parameters.AddWithValue("@price", product.Price);
            command.Parameters.AddWithValue("@stock", product.Stock);
            command.Parameters.AddWithValue("@category", product.Category);
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    private async Task<MySqlConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    private static ProdutoDto ReadProduto(MySqlDataReader reader) => new(
        reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetDecimal(4),
        reader.GetInt32(5), reader.GetInt32(6), reader.GetString(7), reader.GetBoolean(8), AsUtc(reader.GetDateTime(9)));

    private static ClienteDto ReadCliente(MySqlDataReader reader) => new(
        reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6),
        reader.GetString(7), AsUtc(reader.GetDateTime(8)));

    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
