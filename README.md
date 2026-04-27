# fiap-cloud-games-mvp
MVP da plataforma FIAP Cloud Games. API em .NET 10 desenvolvida como Tech Challenge da Pós-Graduação em Arquitetura de Sistemas .NET (FIAP), utilizando DDD, EF Core e Autenticação JWT.

## Configuração local (primeira vez)

Secrets nunca ficam no repositório. Configure via `.env` (Docker) e .NET User Secrets (aplicação).

### 1. Criar o arquivo de variáveis de ambiente

```bash
cp .env.example .env
```

Edite o `.env` e defina uma senha forte para o SQL Server.

### 2. Subir o SQL Server via Docker

```bash
docker compose up -d
```

### 3. Configurar os secrets da aplicação

**Desenvolvimento local (User Secrets):**
```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=FcgDb;User Id=sa;Password=<SA_PASSWORD>;TrustServerCertificate=True;" --project src/FCG.API
dotnet user-secrets set "AdminSeed:DefaultPassword" "<SenhaDoAdmin>" --project src/FCG.API
```

Substitua `<SA_PASSWORD>` pela senha definida no `.env`.

**CI/produção (variáveis de ambiente):**
```bash
ConnectionStrings__DefaultConnection=Server=...;Database=...;User Id=...;Password=...;
AdminSeed__DefaultPassword=SuaSenhaAqui
```

Precedência do .NET (do menor pro maior): `appsettings.json` → `appsettings.Development.json` → User Secrets → Variáveis de ambiente.

### 4. Aplicar as migrations

```bash
dotnet ef database update -p src/FCG.Infrastructure -s src/FCG.API
```

### 5. Rodar a API

```bash
dotnet run --project src/FCG.API
```
