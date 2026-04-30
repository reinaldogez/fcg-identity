# FIAP Cloud Games MVP
MVP da plataforma FIAP Cloud Games. API em .NET 10 desenvolvida como Tech Challenge da Pós-Graduação em Arquitetura de Sistemas .NET (FIAP), utilizando DDD, EF Core e Autenticação JWT.

## Table of Contents
- [FIAP Cloud Games MVP](#fiap-cloud-games-mvp)
  - [Table of Contents](#table-of-contents)
  - [Sobre o projeto](#sobre-o-projeto)
  - [Stack](#stack)
  - [Estrutura de pastas](#estrutura-de-pastas)
  - [Configuração local (primeira vez)](#configuração-local-primeira-vez)
    - [1. Criar o arquivo de variáveis de ambiente](#1-criar-o-arquivo-de-variáveis-de-ambiente)
    - [2. Subir o SQL Server via Docker](#2-subir-o-sql-server-via-docker)
    - [3. Configurar os secrets da aplicação](#3-configurar-os-secrets-da-aplicação)
    - [4. Aplicar as migrations](#4-aplicar-as-migrations)
    - [5. Rodar a API](#5-rodar-a-api)
  - [CI/CD (GitHub Actions)](#cicd-github-actions)
    - [Secrets obrigatórios no repositório](#secrets-obrigatórios-no-repositório)
  - [Como rodar os testes](#como-rodar-os-testes)
  - [Autenticação e Autorização](#autenticação-e-autorização)
    - [Fluxo](#fluxo)
    - [Endpoints de `UsuarioController`](#endpoints-de-usuariocontroller)
    - [Smoke test pelo Scalar](#smoke-test-pelo-scalar)
  - [Observabilidade](#observabilidade)

## Sobre o projeto

A FIAP Cloud Games (FCG) será uma plataforma de venda de jogos digitais e gestão de servidores para partidas online. Esta entrega é a **Fase 1** do Tech Challenge, focada em estabelecer a base da plataforma com **cadastro de usuários e autenticação JWT**, garantindo persistência de dados, qualidade de software e boas práticas de desenvolvimento que servirão de fundação para as próximas fases (matchmaking, biblioteca de jogos, gestão de servidores).

**Escopo desta entrega:**

- **Cadastro de usuários** identificados por nome, e-mail e senha. O e-mail é validado quanto ao formato e a senha precisa ter pelo menos 8 caracteres, com letras, números e caracteres especiais. As senhas nunca são guardadas em texto puro — apenas o hash BCrypt vai para o banco.
- **Autenticação via JWT** com dois níveis de acesso: usuário comum (acessa a plataforma) e administrador (administra usuários). O login devolve um access token de 1 hora e um refresh token de 7 dias, que pode ser trocado por um novo par sem precisar fazer login de novo. A cada renovação o refresh token anterior é revogado, e o logout invalida o refresh token apresentado.
- **Gestão de perfil:** atualizar dados, trocar de senha, desativar a conta (soft delete) e alterar o tipo de usuário. As regras de quem pode fazer o quê são aplicadas via políticas de autorização (por exemplo: o próprio usuário ou um administrador podem alterar dados; só administradores podem desativar contas).
- **API REST com Controllers MVC** em .NET 10, documentada com OpenAPI (Scalar) — os endpoints podem ser explorados diretamente pelo navegador em `https://localhost:7222/scalar/v1`.
- **Middleware global de erros** que captura exceções e devolve respostas padronizadas (formato `ProblemDetails`, RFC 7807) com um `traceId` em cada resposta para facilitar a correlação com logs.
- **Persistência com Entity Framework Core** (Code-First) e migrations versionadas, usando SQL Server.
- **Testes automatizados** cobrindo as principais regras de negócio (unitários) e o fluxo completo da API (integração com SQL Server real via Testcontainers).
- **Modelagem em DDD:** entidades, value objects, domain services e exceptions de domínio organizados em camadas independentes (Domain, Application, Infrastructure, API), preservando a regra de dependência de dentro para fora.

## Stack

- **.NET 10 / C# 14** — runtime e linguagem
- **SQL Server + EF Core (Code-First com Migrations)** — persistência relacional
- **BCrypt.Net-Next** — hashing de senhas
- **Microsoft.AspNetCore.Authentication.JwtBearer + System.IdentityModel.Tokens.Jwt** — JWT HS256
- **Scalar + Microsoft.AspNetCore.OpenApi** — documentação interativa da API (equivalente ao Swagger)
- **xUnit + FluentAssertions + Moq** — testes unitários
- **Microsoft.AspNetCore.Mvc.Testing + Testcontainers.MsSql** — testes de integração
- **Docker Compose** — SQL Server local para desenvolvimento
- **Serilog + Serilog.Formatting.Compact** — logs estruturados JSON (CLEF) com enriquecimento automático (TraceId, SpanId, MachineName)
- **OpenTelemetry SDK (AspNetCore)** — rastreamento distribuído W3C, fundação para Tempo/Grafana

## Estrutura de pastas

DDD + Clean Architecture em quatro camadas (cada uma é um `.csproj` separado), seguindo a regra de dependência `API → Infrastructure → Application → Domain`:

```
src/
  FCG.Domain          → Entidades, Value Objects, Enums, Exceptions, Interfaces e Domain Services. ZERO dependências externas.
  FCG.Application     → Use Cases (orquestração), DTOs e contratos de serviços externos.
  FCG.Infrastructure  → EF Core (DbContext, Configs, Repositórios), serviços (BCrypt, JWT) e Migrations.
  FCG.API             → Controllers MVC, Middlewares (erro, rate limit), Authorization handlers e composição da aplicação (Program.cs).
tests/
  FCG.Tests.Unit          → Unitários para Domain, Application e Middlewares.
  FCG.Tests.Integration   → Integração end-to-end com WebApplicationFactory + Testcontainers (SQL Server real em Docker).
docs/                  → Event Storming, decisões arquiteturais e documentação de DDD.
```

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
dotnet user-secrets set "Jwt:SigningKey" "<chave-aleatoria-com-mais-de-32-caracteres>" --project src/FCG.API
```

Substitua `<SA_PASSWORD>` pela senha definida no `.env`. Para `Jwt:SigningKey`, use uma string aleatória com pelo menos 32 caracteres (ex: `openssl rand -base64 64`). A API valida no startup e falha imediatamente se a chave for menor.

> **Windows:** o `openssl` não está disponível no PowerShell por padrão. Use o **Git Bash** (incluído com o [Git para Windows](https://git-scm.com/download/win)) ou instale o [OpenSSL para Windows](https://slproweb.com/products/Win32OpenSSL.html) separadamente.

**CI/produção (variáveis de ambiente):**
```bash
ConnectionStrings__DefaultConnection=Server=...;Database=...;User Id=...;Password=...;
AdminSeed__DefaultPassword=SuaSenhaAqui
Jwt__SigningKey=ChaveAleatoriaComMaisDe32Caracteres
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

## CI/CD (GitHub Actions)

O workflow em [`.github/workflows/ci.yml`](.github/workflows/ci.yml) roda automaticamente em todo `push` para `main` ou `feature/**` e em Pull Requests para `main`. Ele executa dois jobs paralelos: testes unitários e testes de integração.

Os testes de integração usam Testcontainers, que sobe um SQL Server real via Docker — o runner `ubuntu-latest` já tem Docker, então funciona sem configuração extra.

### Secrets obrigatórios no repositório

Configure em **Settings → Secrets and variables → Actions → New repository secret**:

| Secret | Descrição | Exemplo |
|---|---|---|
| `JWT_SIGNING_KEY` | Chave de assinatura JWT usada nos testes (mínimo 32 caracteres) | `openssl rand -base64 64` |
| `ADMIN_SEED_PASSWORD` | Senha do administrador inicial (`AdminSeed:DefaultPassword`) | `Admin@123456` |

> Esses secrets são usados **apenas nos testes de integração** — o banco é um container efêmero e descartado ao final do job. Para um ambiente de produção real, adicione também `ConnectionStrings__DefaultConnection` e `Jwt__Issuer`/`Jwt__Audience` como secrets ou variáveis de ambiente no servidor de deploy.

## Como rodar os testes

```bash
dotnet test                                      # Roda todas as suítes
dotnet test tests/FCG.Tests.Unit/                # Apenas unitários (rápido, sem dependências)
dotnet test tests/FCG.Tests.Integration/         # Integração end-to-end (requer Docker rodando)
```

Os testes de integração usam `Testcontainers.MsSql` para subir uma instância efêmera de SQL Server por execução — o Docker Desktop (ou daemon equivalente) precisa estar ativo. As migrations são aplicadas automaticamente no container antes de cada cenário, e o banco é descartado ao final.

## Autenticação e Autorização

A API usa **JWT Bearer** com dois níveis de acesso (`Usuario` e `Administrador`) e refresh tokens com **rotação**.

### Fluxo

1. **Login** — `POST /api/auth/login` com `{ "email", "senha" }` retorna:
   ```json
   {
     "accessToken": "eyJhbGc...",
     "tokenType": "Bearer",
     "expiresIn": 3600,
     "refreshToken": "Y3Jp..."
   }
   ```
   Access token vale 1 hora, refresh token 7 dias.
2. **Chamadas autenticadas** — adicione `Authorization: Bearer <accessToken>` no header.
3. **Renovar** — `POST /api/auth/refresh` com `{ "refreshToken" }` retorna **novo par** (access + refresh). O refresh apresentado é revogado e marcado como substituído pelo novo (rotação).
4. **Logout** — `POST /api/auth/logout` com `{ "refreshToken" }` revoga o refresh atual. Operação **idempotente**: tokens inexistentes ou já revogados também retornam 204. Access tokens já emitidos continuam válidos até expirar.

Falhas de autenticação retornam **401** com mensagem genérica `"Credenciais inválidas."` (ou `"Refresh token inválido."`) — não vazamos se foi o e-mail, a senha, o status do usuário ou o token.

### Endpoints de `UsuarioController`

| Método | Rota | Acesso |
|---|---|---|
| `POST` | `/api/usuarios` | público |
| `GET` | `/api/usuarios/{id}` | próprio dono **ou** `Administrador` (policy `OwnerOrAdmin`) |
| `GET` | `/api/usuarios` | `Administrador` |
| `PUT` | `/api/usuarios/{id}` | próprio dono **ou** `Administrador` (policy `OwnerOrAdmin`) |
| `POST` | `/api/usuarios/{id}/alterar-senha` | próprio dono **ou** `Administrador` |
| `PATCH` | `/api/usuarios/{id}/desativar` | `Administrador` |
| `PATCH` | `/api/usuarios/{id}/tipo` | `Administrador` (admin não pode rebaixar a si mesmo → 400) |

### Smoke test pelo Scalar

Em desenvolvimento, abra `https://localhost:7222/scalar/v1`. O botão **Authorize** usa o SecurityScheme Bearer (configurado via `BearerSecuritySchemeTransformer`): cole apenas o `accessToken` (sem o prefixo `Bearer`) e os endpoints protegidos passam a enviar o header automaticamente.

Casos prontos em `src/FCG.API/FCG.API.http` (login → refresh → logout, e Authorization header já preenchido nos endpoints protegidos).

## Observabilidade

A API usa **Serilog** para logs estruturados com correlação de rastreamento via **OpenTelemetry** (TraceId/SpanId W3C). O formato de saída varia por ambiente:

| Ambiente | Formato | Motivo |
|---|---|---|
| `Development` | Console colorido (texto legível) | DX — fácil de acompanhar durante desenvolvimento |
| `Production` | Console JSON (CLEF, uma linha por evento) | Pronto para em etapas futuras utilizar Promtail/Alloy → Loki |

Todo evento de log carrega automaticamente `TraceId`, `SpanId`, `Application`, `MachineName` e `Environment` como propriedades estruturadas. Exemplo de linha em produção:

```json
{"@t":"2026-04-29T14:32:01.123Z","@mt":"HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms","RequestMethod":"POST","RequestPath":"/api/auth/login","StatusCode":200,"Elapsed":84.3,"TraceId":"4bf92f3577b34da6a3ce929d0e0e4736","SpanId":"00f067aa0ba902b7","Application":"FCG.API"}
```

Respostas de erro (4xx/5xx) incluem o mesmo `traceId` no corpo (`ProblemDetails.Extensions["traceId"]`), permitindo correlacionar um erro reportado pelo cliente diretamente com o evento de log correspondente.
