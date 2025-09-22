# ARRUME — captação de leads e encaminhamento via WhatsApp

Plataforma que recebe um pedido do cliente, salva o lead (SQLite no DEV, SQL Azure no PROD) e envia mensagens de WhatsApp:
- Para o **cliente**: lista de profissionais indicados
- Para cada **capoteiro/estofador**: dados do lead

## Stack
- ASP.NET Core 8 (MVC)
- SQLite (DEV) / SQL Server Azure (PROD)
- Z-API (WhatsApp)
- jQuery + jQuery Validate (validações client-side)
- ViaCEP / BrasilAPI (auto-preenchimento por CEP)

## Estrutura (pastas principais)
- `Controllers/`
  - `HomeController`: exibe páginas “Início” e “Obrigado”
  - `LeadController`: recebe o formulário, valida, completa por CEP, salva e dispara WhatsApp
- `Models/`
  - `Lead`: dados do lead + anotações de validação PT-BR
  - `Capoteiro`: dados do profissional
- `Services/`
  - `IZApiService` + `ZApiService`/`ZApiFakeService`: envio WhatsApp (real/fake)
  - `ICapoteiroProvider` + `CapoteiroProviderDevColleagues` (DEV) / `CapoteiroProviderAzure` (PROD)
  - `ILeadStore` + `LeadStoreSqlite` (DEV) / `LeadStoreSqlServer` (PROD)
- `Data/`
  - `DevSqliteDbContext`: contexto EF só para DEV/SQLite
- `Views/`
  - `Home/Index.cshtml`: formulário
  - `Home/Obrigado.cshtml`
  - `_Layout.cshtml`
- `wwwroot/`
  - `js/form-lead.js` (normalizações, ViaCEP client-side)
  - `css/site.css`

## Como funciona (resumo)
1. O usuário preenche o formulário. CEP preenche **Cidade/UF** automaticamente (client-side) e o servidor confirma/faz fallback (server-side).
2. O `LeadController` normaliza/valida, salva o lead e busca profissionais:
   - **DEV**: usa lista “Dev:Colleagues” do `appsettings` (fake de colegas).
   - **PROD**: consulta tabela `CLIENTES` no Azure SQL (com filtro de categorias).
3. Envia WhatsApp via **Z-API**:
   - Cliente: recebe a lista de profissionais.
   - Profissionais: recebem os dados do lead.

## Configuração
Use os `appsettings` e/ou variáveis de ambiente:

- `ZApi:UseFake` = `false` para usar Z-API real (DEV e PROD).
- `ZApi:UrlBase`, `ZApi:Instance`, `ZApi:Token`, `ZApi:ClientToken` (se exigido pela instância), `ZApi:SenderPhone` (opcional, evita mandar para o próprio número).
- `ConnectionStrings:Sqlite` (DEV) e `ConnectionStrings:AzureSql` (PROD).
- `AzureSql:CategoriaClienteIds` (categorias aceitas na consulta de profissionais).

> **Dica (DEV com Z-API real):** deixe `ASPNETCORE_ENVIRONMENT=Development`, configure `ZApi__UseFake=false` e preencha `Dev:Colleagues` no `appsettings.json` com números **55DDDNÚMERO**.

## Executar em DEV (SQLite + Z-API real ou fake)
Pré-requisitos: .NET 8 SDK

**Windows PowerShell**
```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
# (opcional) usar Z-API real em DEV:
$env:ZApi__UseFake = "false"
$env:ZApi__Instance = "<sua-instancia>"
$env:ZApi__Token = "<seu-token>"
$env:ZApi__ClientToken = "<seu-client-token-optional>"
dotnet run

## Executar em PROD
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__AzureSql="Server=tcp:<servidor>.database.windows.net,1433;Initial Catalog=<db>;User ID=<user>;Password=<pwd>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
export ZApi__UseFake=false
export ZApi__UrlBase=https://api.z-api.io
export ZApi__Instance="<sua-instancia>"
export ZApi__Token="<seu-token>"
export ZApi__ClientToken="<seu-client-token-optional>"
dotnet run

