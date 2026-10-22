# Plano de testes — Loja de Produtos de Limpeza

## 1. Automatizados (xUnit)

Execute:

```powershell
dotnet test LojaLimpeza.Tests/LojaLimpeza.Tests.csproj
```

O conjunto automatizado cobre:

- soma de subtotais e arredondamento monetário;
- rejeição de quantidade ou preço negativo;
- validação de CPF e CNPJ, inclusive documento repetido/incompatível com PF/PJ;
- validação de cadastro e endereço obrigatório;
- rejeição de carrinho vazio e de produtos duplicados;
- aceitação de pedido com cliente e itens válidos.

As regras ficam no projeto `LojaLimpeza.Shared`, utilizado pela API, pelo aplicativo MAUI e pelos testes.

## 2. API + MySQL (integração)

Pré-condições: criar `loja_limpeza`, iniciar MySQL e configurar `ConnectionStrings__DefaultConnection`. Inicie a API:

```powershell
dotnet run --project LojaLimpeza.Api/LojaLimpeza.Api.csproj --launch-profile http
```

| ID | Ação | Resultado esperado |
|---|---|---|
| API-01 | Consultar `GET /api/health`. | HTTP 200 e `status: ok`. |
| API-02 | Consultar `GET /api/categorias` e `GET /api/produtos`. | Categorias e produtos de demonstração; produtos ativos têm preço, unidade, estoque e categoria. |
| API-03 | Enviar cliente PF com CPF válido e `ExternalId` GUID. | HTTP 201, cliente persistido no MySQL. Repetir o mesmo `ExternalId` não cria novo registro. |
| API-04 | Enviar cadastro com CPF inválido ou endereço vazio. | HTTP 400 com mensagem de validação. |
| API-05 | Enviar pedido com cliente existente, produto ativo e quantidade dentro do estoque. | HTTP 201, total calculado no servidor, item salvo e estoque reduzido. |
| API-06 | Repetir o mesmo `ExternalId` do pedido. | Retorna o pedido já criado; não duplica item nem reduz estoque novamente. |
| API-07 | Enviar pedido vazio, quantidade zero ou produto repetido. | HTTP 400; sem pedido/baixa de estoque. |
| API-08 | Pedir quantidade acima do estoque ou produto inativo. | HTTP 409; estoque e pedidos permanecem inalterados. |
| API-09 | Alterar preço/estoque/ativo via `PUT /api/produtos/{id}` e reler produto. | HTTP 200 e novos valores visíveis na resposta e no painel. |
| API-10 | Atualizar produto inexistente ou enviar estoque/preço negativo. | HTTP 404 ou 400, respectivamente. |

`POST /api/pedidos` precisa de `clienteId` retornado pela API e `produtoId` retornado por `GET /api/produtos`. Execute os casos em banco de desenvolvimento, pois pedidos válidos consomem estoque.

Smoke test opcional repetível (usa IDs fixos para não duplicar o cliente/pedido em cada execução):

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Smoke-Api.ps1
```

## 3. Aplicativo MAUI (Android, Windows e iOS)

| ID | Cenário | Resultado esperado |
|---|---|---|
| APP-01 | Abrir após instalação/limpar dados do app. | Aplicativo abre diretamente o cadastro; não permite criar pedido sem cliente. |
| APP-02 | Tentar salvar CPF/CNPJ inválido, nome ou endereço em branco. | Mensagem de validação; cadastro não é salvo. |
| APP-03 | Salvar cadastro válido com a API ligada. | Cadastro aparece no MySQL/painel; app retorna à lista de pedidos. |
| APP-04 | Salvar cadastro válido sem rede/API. | Cadastro permanece no SQLite e pode ser sincronizado depois. |
| APP-05 | Atualizar catálogo com a API disponível. | Categorias/preços/unidades/estoques aparecem no SQLite e na tela de pedido. |
| APP-06 | No painel, inativar um produto e sincronizar o catálogo. | Produto não aparece para compra no app; continua guardado localmente para referência histórica. |
| APP-07 | Escolher categoria, adicionar dois produtos com quantidades e remover um do carrinho. | Linhas e total atualizam conforme produtos restantes. |
| APP-08 | Tentar adicionar quantidade vazia, zero ou maior que 9.999. | O app informa erro e não altera o carrinho. |
| APP-09 | Salvar um pedido válido com a API ligada. | Pedido é primeiro salvo localmente, enviado uma vez, recebe status central e aparece no painel. |
| APP-10 | Desligar a API/rede, salvar pedido e depois reconectar e sincronizar. | Pedido fica pendente no app e chega uma vez ao MySQL/painel após reconexão. |
| APP-11 | Alterar o preço central antes do envio de um pedido pendente. | API calcula e retorna o total pelo preço atual; app atualiza o total sincronizado. |
| APP-12 | Forçar pedido acima do estoque em outro dispositivo e sincronizar. | API rejeita com conflito; app mantém pedido pendente e apresenta a falha na sincronização. |

Execute APP-01 a APP-12 em Android Emulator ou Windows. No Android Emulator, confirme que a API usa `10.0.2.2:5256`. Um aparelho físico precisa do endereço IP do computador servidor. Para iOS, compile e valide em Mac com Xcode.

## 4. Painel Web Razor

| ID | Cenário | Resultado esperado |
|---|---|---|
| WEB-01 | Abrir `http://localhost:5256/` com API e MySQL ativos. | Painel carrega e indica API conectada. |
| WEB-02 | Selecionar a aba Pedidos. | Aparecem pedido, cliente, itens, status, data e total. |
| WEB-03 | Abrir detalhes de itens de um pedido. | Cada linha mostra quantidade, unidade, nome e subtotal. |
| WEB-04 | Selecionar Clientes. | Cadastro recebido do app aparece com tipo, documento e contato. |
| WEB-05 | Selecionar Produtos e estoque. | Produtos ativos e inativos aparecem; preço, estoque e estado podem ser editados e salvos. |
| WEB-06 | Alterar produto no painel e aguardar até 15 segundos. | Resumo/listas recarregam sem atualizar o navegador manualmente. |
| WEB-07 | Desligar API/MySQL. | Painel indica sem conexão e informa a origem da falha. |

## 5. Verificação de build

No Windows, valide estes comandos:

```powershell
dotnet build LojaLimpeza.Api/LojaLimpeza.Api.csproj
dotnet build LojaLimpeza.Maui/LojaLimpeza.Maui.csproj -f net10.0-windows10.0.19041.0
dotnet build LojaLimpeza.Maui/LojaLimpeza.Maui.csproj -f net10.0-android
```

O SDK .NET 10 e os workloads correspondentes precisam estar instalados. O build iOS deve ser executado em macOS com Xcode.
