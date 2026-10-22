# Loja de Produtos de Limpeza

Projeto acadêmico de pedidos com aplicativo .NET MAUI, API ASP.NET Core, persistência local SQLite, banco central MySQL e painel web Razor.

## Fluxo principal

1. No primeiro acesso, o aplicativo exige o cadastro do cliente (pessoa física ou jurídica). O cadastro é persistido no SQLite e sincronizado com a API quando houver conexão.
2. O aplicativo baixa categorias e produtos do MySQL por meio da API. O catálogo fica em cache local e é atualizado a cada cinco minutos, ao abrir a tela de pedidos, ou pelo botão **Atualizar catálogo**.
3. O cliente escolhe produtos, informa quantidades e revisa o total no carrinho. O pedido é salvo primeiro no SQLite, então pode permanecer pendente quando estiver sem internet.
4. A sincronização envia cadastro e pedidos pendentes. A API valida cliente, produtos ativos e estoque; calcula o total com os preços atuais do servidor e registra pedido, itens e baixa de estoque em uma transação MySQL. O identificador externo do pedido evita duplicação em novas tentativas.
5. O painel web exibe pedidos, clientes, produtos e estoque. Atualiza automaticamente a cada 15 segundos. É possível alterar preço, estoque e situação ativa do produto.

## Tecnologias

- Aplicativo: C# / .NET MAUI, XAML, SQLite (`sqlite-net-pcl`), `HttpClient` e `Preferences`.
- API e site: ASP.NET Core Web API, Razor Pages e MySqlConnector.
- Banco central: MySQL 8 ou compatível. O schema é criado e os dados demonstrativos são inseridos pela API no primeiro início.
- Testes automatizados: xUnit sobre regras compartilhadas de cadastro, documento, quantidade e cálculo do pedido.

## Requisitos para executar

- .NET SDK 10 e workload MAUI para compilar o aplicativo.
- MySQL 8 em execução.
- Visual Studio 2022 com workload .NET MAUI ou ferramentas .NET equivalentes.
- Android Emulator para teste Android. A compilação de iOS requer macOS e Xcode.

## Banco de dados

Crie o banco vazio uma vez:

```sql
SOURCE database/01_criar_banco.sql;
```

O script cria `loja_limpeza`. Configure o usuário e a senha pela variável de ambiente `ConnectionStrings__DefaultConnection`, por exemplo:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost;Port=3306;Database=loja_limpeza;User ID=root;Password=SUA_SENHA;"
```

O padrão local do `appsettings.json` é `root` sem senha, apenas para a instalação local de demonstração. Use uma conta de banco própria quando necessário.

## Executar API e painel

Na raiz do repositório:

```powershell
dotnet run --project LojaLimpeza.Api/LojaLimpeza.Api.csproj --launch-profile http
```

- Painel web: <http://localhost:5256/>
- Verificação da API: <http://localhost:5256/api/health>
- Categorias: <http://localhost:5256/api/categorias>
- Produtos: <http://localhost:5256/api/produtos>

## Executar o aplicativo

Para compilar e iniciar no Windows:

```powershell
dotnet build LojaLimpeza.Maui/LojaLimpeza.Maui.csproj -f net10.0-windows10.0.19041.0
dotnet run --project LojaLimpeza.Maui/LojaLimpeza.Maui.csproj -f net10.0-windows10.0.19041.0
```

Para Android Emulator:

```powershell
dotnet build LojaLimpeza.Maui/LojaLimpeza.Maui.csproj -f net10.0-android
```

O app Android aponta para `http://10.0.2.2:5256/`, endereço do host visto pelo emulador. Windows e simuladores usam `http://localhost:5256/`. Em um telefone físico, altere o endereço `ApiBaseAddress` em `LojaLimpeza.Maui/MauiProgram.cs` para o IP acessível do computador servidor; telefone e servidor devem estar na mesma rede.

## Testes

```powershell
dotnet test LojaLimpeza.Tests/LojaLimpeza.Tests.csproj
dotnet build LojaLimpeza.Api/LojaLimpeza.Api.csproj
dotnet build LojaLimpeza.Maui/LojaLimpeza.Maui.csproj -f net10.0-windows10.0.19041.0
```

Os testes automatizados não exigem conexão ao MySQL. Para os cenários integrados com banco, execute API e MySQL e siga `docs/Plano-de-Testes.md`. O roteiro manual contempla o Android e o painel web; iOS deve ser validado em um Mac com Xcode.

## Documentação do projeto

- [Especificação funcional, técnica e de API](docs/Especificacao-e-Requisitos.md)
- [Plano de testes automatizados, integração e interface](docs/Plano-de-Testes.md)
- [Script de criação do banco](database/01_criar_banco.sql)
