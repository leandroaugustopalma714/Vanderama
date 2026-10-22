param(
    [string] $ApiBaseUrl = "http://localhost:5256"
)

$ErrorActionPreference = "Stop"
$ApiBaseUrl = $ApiBaseUrl.TrimEnd('/')

$health = Invoke-RestMethod -Method Get -Uri "$ApiBaseUrl/api/health"
if ($health.status -ne "ok") { throw "API não retornou status ok." }

# Identificadores fixos deixam o smoke test idempotente em seu banco de desenvolvimento.
$customerBody = @{
    externalId = "a10f22aa-0000-4000-8000-000000000001"
    nome = "Cliente de teste da API"
    documento = "52998224725"
    tipoPessoa = "PF"
    telefone = "11999990000"
    email = "teste@example.local"
    endereco = "Rua de Teste, 100"
} | ConvertTo-Json

$customer = Invoke-RestMethod -Method Post -Uri "$ApiBaseUrl/api/clientes" -ContentType "application/json" -Body $customerBody
$products = Invoke-RestMethod -Method Get -Uri "$ApiBaseUrl/api/produtos"
if (-not $products -or $products.Count -eq 0) { throw "A API não retornou produtos ativos." }

$orderBody = @{
    externalId = "a10f22aa-0000-4000-8000-000000000002"
    clienteId = $customer.id
    itens = @(@{ produtoId = $products[0].id; quantidade = 1 })
} | ConvertTo-Json -Depth 5

$order = Invoke-RestMethod -Method Post -Uri "$ApiBaseUrl/api/pedidos" -ContentType "application/json" -Body $orderBody
$orders = Invoke-RestMethod -Method Get -Uri "$ApiBaseUrl/api/pedidos"
if (-not ($orders | Where-Object { $_.id -eq $order.id })) { throw "O pedido criado não apareceu na listagem." }
if ($order.valorTotal -le 0) { throw "O total do pedido deve ser maior que zero." }

Write-Output "Smoke test da API concluído: cliente $($customer.id), pedido $($order.id), total $($order.valorTotal)."
