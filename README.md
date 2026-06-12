# fcg-identity

Microsserviço de **identidade, autenticação e autorização** da Fase 2 do FIAP Cloud Games (FCG).
É o **único serviço que emite JWT** (assinatura **RS256**) e expõe o **JWKS** (`/.well-known/jwks.json`)
para que os serviços downstream validem os tokens apenas com a chave pública, sem compartilhar segredo.

Publica o evento de domínio **`UserCreatedEvent`** (via Outbox transacional) sempre que um usuário é
cadastrado; **não consome nenhum evento**.

> **Origem deste repositório**
>
> Este repositório nasceu a partir de [`reinaldogez/fiap-cloud-games-mvp`](https://github.com/reinaldogez/fiap-cloud-games-mvp),
> a entrega da Fase 1 do Tech Challenge (FIAP). Todo o histórico de commits foi preservado — `git log`
> mostra a evolução desde o início da Fase 1. O MVP continua congelado como entrega da Fase 1; a partir
> daqui, `fcg-identity` segue um caminho próprio: o módulo de Usuários/Autenticação do monolito refatorado
> num microsserviço de identidade dedicado, orientado a eventos.

## Sumário

- [fcg-identity](#fcg-identity)
  - [Sumário](#sumário)
  - [Stack](#stack)
  - [Arquitetura](#arquitetura)
  - [Eventos](#eventos)
  - [Endpoints](#endpoints)
    - [REST](#rest)
    - [JWKS](#jwks)
    - [Health checks](#health-checks)
  - [Variáveis de ambiente](#variáveis-de-ambiente)
  - [Rodar local (primeira vez)](#rodar-local-primeira-vez)
    - [Subir a plataforma via `fcg-ops` (recomendado)](#subir-a-plataforma-via-fcg-ops-recomendado)
    - [Desenvolver o serviço (inner loop)](#desenvolver-o-serviço-inner-loop)
  - [Rodar os testes](#rodar-os-testes)
  - [CI/CD (GitHub Actions)](#cicd-github-actions)
    - [Secrets do repositório](#secrets-do-repositório)
    - [Visibilidade da imagem no GHCR](#visibilidade-da-imagem-no-ghcr)
  - [Autenticação e Autorização](#autenticação-e-autorização)
    - [Access token e claims](#access-token-e-claims)
    - [Fluxo](#fluxo)
    - [Policy `OwnerOrAdmin`](#policy-owneroradmin)
  - [Tratamento de erros](#tratamento-de-erros)
  - [Observabilidade](#observabilidade)
  - [GraphQL (leitura)](#graphql-leitura)
  - [Relatório administrativo (Dapper)](#relatório-administrativo-dapper)

## Stack

- **.NET 10 / C# 14** — runtime e linguagem
- **SQL Server + EF Core** (Code-First com migrations) — persistência (escrita e leitura)
- **MassTransit + RabbitMQ** — mensageria; `AddEntityFrameworkOutbox` sobre o banco `identity` (Outbox transacional)
- **Fcg.Contracts** (NuGet via GHCR) — contratos de evento; `UserCreatedEvent` vem 100% do pacote
- **Dapper + Microsoft.Data.SqlClient** — query agregada do relatório administrativo
- **HotChocolate 16** (GraphQL) — endpoint `/graphql` com filtering/sorting/cursor paging (leitura de usuários)
- **BCrypt.Net-Next** — hashing de senhas
- **JwtBearer + System.IdentityModel.Tokens.Jwt** — JWT **RS256** com `kid`; **JWKS** público
- **Scalar + Swashbuckle.AspNetCore.SwaggerUI + Microsoft.AspNetCore.OpenApi** — documentação da API
- **Serilog** (Console `CompactJsonFormatter` em prod, `outputTemplate` em dev) **+ sink Loki** (aditivo)
- **OpenTelemetry** (instrumentação AspNetCore + Console exporter **+ OTLP** → Tempo/Prometheus)
- **xUnit + FluentAssertions + Moq** — testes unitários
- **Microsoft.AspNetCore.Mvc.Testing + Testcontainers** (`MsSql` **+ RabbitMQ**) — testes de integração
- **Reqnroll (xUnit)** — testes BDD com cenários Gherkin em PT-BR
- **Docker Compose** (no `fcg-ops`) — orquestração local: SQL Server, RabbitMQ e a stack de observabilidade
- **SonarCloud** — análise estática e cobertura (coverlet/opencover)

## Arquitetura

DDD + Clean Architecture em 4 camadas (cada uma um `.csproj`), com a regra de dependência
`Api → Infrastructure → Application → Domain` (o Domain não referencia ninguém):

```
src/
  Fcg.Identity.Domain          → Entidades, VOs, Enums, Exceptions, Interfaces e Domain Services
  Fcg.Identity.Application     → Use Cases (orquestração), DTOs, Options, interfaces de serviços externos
  Fcg.Identity.Infrastructure  → EF Core (DbContext, Configs, Repos), Dapper, MassTransit/Outbox, serviços
  Fcg.Identity.Api             → Controllers REST, GraphQL, Middlewares, JWKS, Health, Program.cs
tests/
  Fcg.Identity.Tests.Unit          → Unitários de Domain/Application/Middlewares
  Fcg.Identity.Tests.Integration   → End-to-end com WebApplicationFactory + Testcontainers (SQL + RabbitMQ)
  Fcg.Identity.Tests.Bdd           → BDD Reqnroll: cenários Gherkin (PT-BR) de cadastro e autenticação
```

A mensageria mora na Infrastructure (`Messaging/`, `Outbox/`); a Application permanece agnóstica de broker
— o `CadastrarUsuarioUseCase` recebe `IPublishEndpoint` e publica `UserCreatedEvent` **dentro da mesma
transação** do cadastro (o Outbox grava a mensagem na linha do commit).

## Eventos

| Evento | Direção | Detalhe |
|---|---|---|
| `UserCreatedEvent` | **Publicado** | Exchange `user-created` (fanout), entregue via Outbox. Emitido apenas pelo `POST /api/usuarios` ao vivo. |
| — | **Consumido** | **Nenhum.** Este serviço só publica — sem `Consumers/`, sem `IConsumer<T>`. |

O nome da exchange (`user-created`) é cravado no bus (message topology), não no contrato — o `Fcg.Contracts`
são records puros. O `DevSeedService` **não** emite eventos: cria usuários direto no banco (alimenta o relatório).

## Endpoints

### REST

| Método | Rota | Acesso |
|---|---|---|
| `POST` | `/api/usuarios` | público (emite `UserCreatedEvent`) |
| `GET` | `/api/usuarios/{id}` | próprio dono **ou** `Administrador` (policy `OwnerOrAdmin`) |
| `GET` | `/api/usuarios` | `Administrador` |
| `PUT` | `/api/usuarios/{id}` | próprio dono **ou** `Administrador` |
| `POST` | `/api/usuarios/{id}/alterar-senha` | próprio dono **ou** `Administrador` |
| `PATCH` | `/api/usuarios/{id}/desativar` | `Administrador` |
| `PATCH` | `/api/usuarios/{id}/ativar` | `Administrador` |
| `PATCH` | `/api/usuarios/{id}/tipo` | `Administrador` (admin não pode rebaixar a si mesmo → 400) |
| `POST` | `/api/auth/login` | público |
| `POST` | `/api/auth/refresh` | público (rotaciona o par) |
| `POST` | `/api/auth/logout` | público (idempotente, 204) |
| `GET` | `/api/admin/relatorios/usuarios` | `Administrador` |
| — | `/graphql` | leitura (ver seção GraphQL) |

### JWKS

| Rota | Acesso | Conteúdo |
|---|---|---|
| `GET /.well-known/jwks.json` | público | **Só a chave pública** (`kty`, `use=sig`, `alg=RS256`, `kid`, `n`, `e`). Nunca expõe `d`/`p`/`q`. `kid` = `fcg-identity-key-1`, idêntico ao header do JWT. Estático, sem rotação no MVP. |

### Health checks

| Rota | Verifica |
|---|---|
| `GET /health/live` | self (liveness) — sempre responde se o processo está de pé |
| `GET /health/ready` | **somente SQL Server** (dependência dura). O RabbitMQ **não** entra no readiness — o Outbox desacopla a API do broker, então derrubar o `ready` por causa do broker anularia o benefício do Outbox |
| `GET /health` | agregado (todos os checks, incl. RabbitMQ informativo) |

## Variáveis de ambiente

No ambiente (k8s/CI), a sintaxe de seção do .NET usa `__` (duplo underscore). A coluna **Origem** indica
de onde o valor deve vir no deploy: **Secret** (sensível) ou **ConfigMap** (não-sensível).

| Variável | Origem | Notas |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | **Secret** | String de conexão do SQL Server |
| `Jwt__RsaPrivateKeyPem` | **Secret** | Chave privada RSA em PEM PKCS#8 (assinatura RS256). Validada no startup: não-vazia e importável |
| `Jwt__KeyId` | ConfigMap | `fcg-identity-key-1` — igual ao `kid` do JWKS e do header do JWT |
| `Jwt__Issuer` | ConfigMap | Emissor do token |
| `Jwt__Audience` | ConfigMap | Audiência do token |
| `RabbitMq__Host` | ConfigMap | Host do broker |
| `RabbitMq__Port` | ConfigMap | Porta (default `5672`) |
| `RabbitMq__Username` | **Secret** | Usuário do broker |
| `RabbitMq__Password` | **Secret** | Senha do broker |
| `Loki__Url` | ConfigMap | Endpoint do Loki. **Vazio ⇒ o sink Loki fica desligado** |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | ConfigMap | Endpoint OTLP (Tempo/Prometheus). **Vazio ⇒ o exporter OTLP fica desligado** |
| `AdminSeed__DefaultPassword` | **Secret** | Senha do admin semeado no startup |

> Em `Development`/`Testing` sem a stack LGTM, deixar `Loki__Url` e `OTEL_EXPORTER_OTLP_ENDPOINT` vazios:
> os sinks de rede não entram no pipeline e a aplicação sobe sem nenhuma tentativa de conexão.

## Rodar local (primeira vez)

A orquestração — Docker Compose, infra compartilhada e o Job de migrations — vive no repositório
**`fcg-ops`**, que centraliza SQL Server, RabbitMQ e a stack de observabilidade de todos os serviços
da fase. Este repositório contém só o serviço e o seu `Dockerfile`. Há dois caminhos:

- **Subir a plataforma (recomendado)** — tudo via `fcg-ops` num único `docker compose up`: infra,
  migrations, a API e a observabilidade. Não precisa do SDK .NET nem de User Secrets; toda a
  configuração vem do `.env`/override do `fcg-ops`.
- **Desenvolver o serviço** — o ciclo de edição deste repositório: testes com Testcontainers e,
  opcionalmente, a API ao vivo no host com `dotnet run`.

Secrets **nunca** ficam no repositório — vêm do `.env`/override da infra (no `fcg-ops`) e, no caminho
de desenvolvimento, dos .NET User Secrets (aplicação).

### Subir a plataforma via `fcg-ops` (recomendado)

**Pré-requisitos:** Docker Desktop (ou daemon equivalente). Para `up --build` — construir a imagem a
partir do código em vez de puxar a imagem pública do GHCR — também um **PAT do GitHub com
`read:packages`**, porque o `Dockerfile` faz o restore interno do `Fcg.Contracts`.

Clone o `fcg-ops` ao lado deste repositório e, **a partir dele**:

```bash
cp .env.example .env          # preencha SQLSERVER_SA_PASSWORD, RABBITMQ_USER/PASSWORD, ADMINSEED_PASSWORD

# gere o par RSA e injete a chave privada no override (não-versionado):
./scripts/gen-rsa-key.sh
cp docker-compose.override.example.yml docker-compose.override.yml   # cole o PEM PKCS#8 no override

docker compose up -d          # infra + migrations + API + observabilidade
```

O serviço de migration aplica as migrations e encerra; a API só inicia depois do banco/broker
`healthy` e da migration concluída, e fica exposta em **`http://localhost:8081`**. Para construir a
imagem a partir do código deste repo, exporte `GH_TOKEN=<PAT>` e use `docker compose up --build`.

### Desenvolver o serviço (inner loop)

**Pré-requisitos:** SDK do .NET 10, EF Core CLI (`dotnet tool install --global dotnet-ef`), Docker
(os testes de integração/BDD sobem SQL Server + RabbitMQ via Testcontainers) e um **PAT do GitHub com
`read:packages`** para o restore do `Fcg.Contracts`.

#### 1. Autenticar o feed do `Fcg.Contracts`

O `nuget.config` registra o source `github-fcg` **sem credenciais** (o token nunca é commitado). Antes do
primeiro restore local, autentique-o com seu PAT:

```bash
dotnet nuget update source github-fcg \
  --username <seu-usuario-github> \
  --password <PAT-com-read:packages> \
  --store-password-in-clear-text
```

#### 2. Rodar os testes (ciclo normal)

```bash
dotnet test   # Testcontainers sobe SQL Server + RabbitMQ efêmeros — não precisa de compose
```

Este é o inner loop de quem edita o código: nenhuma infra precisa estar de pé antecipadamente.

#### 3. (opcional) A API ao vivo no host

Para rodar a API fora do container (`dotnet run`), você precisa de SQL Server e RabbitMQ **alcançáveis
no host**. O compose base do `fcg-ops` mantém esses serviços só na rede interna (não publica `1433`/`5672`)
— publique as portas via um override local no `fcg-ops` se quiser este fluxo. Em seguida configure os
User Secrets da aplicação (a connection string aponta para o host/porta que você publicou; abaixo,
`localhost,1433`):

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=localhost,1433;Database=identity;User Id=sa;Password=<SQLSERVER_SA_PASSWORD>;TrustServerCertificate=True;" \
  --project src/Fcg.Identity.Api
dotnet user-secrets set "AdminSeed:DefaultPassword" "<SenhaDoAdmin>" --project src/Fcg.Identity.Api
dotnet user-secrets set "Jwt:RsaPrivateKeyPem" "<PEM-PKCS8-da-chave-privada>" --project src/Fcg.Identity.Api
dotnet user-secrets set "Jwt:KeyId" "fcg-identity-key-1" --project src/Fcg.Identity.Api
```

> A chave RSA é a mesma gerada pelo `gen-rsa-key.sh` do `fcg-ops`. A migração para RS256 **removeu** o
> antigo `Jwt:SigningKey` (HS256): a API valida o PEM no startup e falha imediatamente se ele estiver
> ausente ou não for importável.

Aplique as migrations e rode a API:

```bash
dotnet ef database update -p src/Fcg.Identity.Infrastructure -s src/Fcg.Identity.Api
dotnet run --project src/Fcg.Identity.Api
```

> Em deploy (e no compose do `fcg-ops`), as migrations **não** rodam no boot da API: são aplicadas por
> um Job/serviço dedicado via a flag `--migrate`.

## Rodar os testes

```bash
dotnet test                                           # todas as suítes
dotnet test tests/Fcg.Identity.Tests.Unit/            # unitários (rápidos, sem dependências)
dotnet test tests/Fcg.Identity.Tests.Integration/     # integração (requer Docker)
dotnet test tests/Fcg.Identity.Tests.Bdd/             # BDD Reqnroll (requer Docker)
```

Integração e BDD usam **Testcontainers** para subir, por execução, uma instância efêmera de **SQL Server
e de RabbitMQ** — o Docker precisa estar ativo. Os containers são descartados ao final.

## CI/CD (GitHub Actions)

O workflow [`.github/workflows/ci.yml`](.github/workflows/ci.yml) roda em todo `push` para `main`/`feature/**`
e em PRs para `main`:

1. **`unit-tests`, `integration-tests`, `bdd-tests`** — em paralelo. Restore autenticado no feed do
   `Fcg.Contracts` (token do runner; `permissions: packages: read`), build em `Release` e testes com
   cobertura OpenCover como artefato. Integração/BDD sobem SQL Server + RabbitMQ via Testcontainers — o
   runner `ubuntu-latest` já tem Docker.
2. **`sonar`** — aguarda os três (`needs`), baixa os artefatos de cobertura e envia a análise consolidada
   ao **SonarCloud** (project key `reinaldogez_fcg-identity`).
3. **`publish`** — **só em `push` para `main`**, após os testes. Faz login no GHCR e publica
   `ghcr.io/<owner>/fcg-identity:latest` e `:<sha>`. O build usa **BuildKit secret** (`gh_token`) porque o
   `Dockerfile` faz o restore interno do `Fcg.Contracts` — sem o secret o restore retorna **401**
   (`permissions: packages: write`).

### Secrets do repositório

- `SONAR_TOKEN` — token do SonarCloud (Settings → Secrets → Actions).
- `GITHUB_TOKEN` — fornecido automaticamente pelo runner; cobre o restore autenticado e o push ao GHCR.

Os testes **não** dependem de secrets do GitHub: a `IdentityApiFactory` injeta as configurações de JWT de
teste por variável de ambiente e remove o seed do DI.

### Visibilidade da imagem no GHCR

A imagem carrega o label `org.opencontainers.image.source` apontando para este repositório (definido no
`Dockerfile`). O GHCR usa esse vínculo para conectar o pacote ao repo e **herdar a sua visibilidade** —
como o repositório é público, o pacote **nasce público**, sem nenhum passo manual após o primeiro push.

> Se o repositório fosse privado, o pacote nasceria privado; aí, sim, seria preciso ajustar a visibilidade
> em **GitHub → Packages → Package settings** para liberar `docker pull` anônimo.

## Autenticação e Autorização

JWT Bearer com dois níveis (`Usuario` / `Administrador`) e refresh tokens com **rotação**.

### Access token e claims

Assinado em **RS256** (`RsaSecurityKey`), com header contendo `kid: fcg-identity-key-1` e as claims:

| Claim | Conteúdo |
|---|---|
| `sub` | `Id` do usuário (`Guid`) — usado pela policy `OwnerOrAdmin` |
| `email` | E-mail do usuário |
| `name` | Nome do usuário |
| `jti` | Identificador único do token (`Guid`) |
| `role` | `Usuario` ou `Administrador` |

A validação local usa a **chave pública** (`RsaSecurityKey`), a mesma exposta no JWKS. O `JwtBearerHandler`
é configurado com `MapInboundClaims = false`, `NameClaimType = sub`, `RoleClaimType = role` e `ClockSkew = 30s`.

### Fluxo

1. **Login** — `POST /api/auth/login` `{ email, senha }` → `{ accessToken, tokenType, expiresIn, refreshToken }`.
   Access token vale 1h; refresh token 7 dias.
2. **Chamadas autenticadas** — header `Authorization: Bearer <accessToken>`.
3. **Renovar** — `POST /api/auth/refresh` `{ refreshToken }` → **novo par**; o refresh anterior é revogado e
   marcado como substituído (rotação).
4. **Logout** — `POST /api/auth/logout` `{ refreshToken }` revoga o refresh apresentado. **Idempotente** (204).

O refresh token são 32 bytes aleatórios; no banco guarda-se só o SHA-256 hex (o plaintext nunca persiste).
Falhas de auth retornam **401** com mensagem genérica (`"Credenciais inválidas."` / `"Refresh token inválido."`).

### Policy `OwnerOrAdmin`

Admin passa direto; caso contrário, o handler compara o `sub` do token com o `{id}` da rota. Concentra a
regra de "próprio dono" num único handler em vez de espalhar `if`s pelos controllers.

## Tratamento de erros

Hierarquia de exceptions no Domain, capturada por um middleware global que responde **RFC 7807**
(`application/problem+json`):

| Exception | Status | `type` |
|---|---|---|
| `DomainException` | 400 | `ErroDeValidacao` |
| `DomainConflictException` | 409 | `ErroDeNegocio` |
| `DomainAuthException` | 401 | `ErroDeAutenticacao` |
| outras (inesperadas) | 500 | `ErroInterno` |

```json
{
  "type": "ErroDeValidacao",
  "title": "Erro ao processar requisição",
  "status": 400,
  "errors": ["O formato do e-mail é inválido."],
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736"
}
```

`OperationCanceledException` com a request abortada vira 499 (sem body). O `traceId` usa o TraceId da
`Activity.Current` (com fallback para o `TraceIdentifier` do contexto).

## Observabilidade

- **Serilog** estruturado. Prod: Console `CompactJsonFormatter` (CLEF); Dev: `outputTemplate` legível.
  Soma o sink **Loki** quando `Loki__Url` está definido (aditivo). Enriquecedores: `FromLogContext`,
  `WithMachineName`, `WithEnvironmentName`, `Application=Fcg.Identity.Api` e um `ActivityEnricher` que injeta
  `TraceId`/`SpanId`.
- **OpenTelemetry**: instrumentação AspNetCore + Console exporter, somando **OTLP** quando
  `OTEL_EXPORTER_OTLP_ENDPOINT` está definido. Service name `Fcg.Identity.Api`. O MassTransit instrumenta
  publish e propaga o `TraceId` via headers AMQP, conectando o trace do `POST /api/usuarios` ao consumer
  downstream.

Todo evento de log carrega `TraceId`/`SpanId`, e as respostas de erro repetem o mesmo `traceId` no corpo —
um erro reportado pelo cliente correlaciona direto com o log.

## GraphQL (leitura)

`/graphql` (**HotChocolate 16**) expõe a superfície de **leitura**; a escrita continua só via REST.

| Query | Acesso | Descrição |
|---|---|---|
| `usuarios(first, after, where, order)` | `Administrador` | Cursor paging + filtering + sorting dinâmicos |
| `usuario(id)` | próprio dono **ou** `Administrador` | Usuário por ID |

O tipo `Usuario` expõe `id`, `nome`, `email`, `tipo`, `dataCriacao`, `ativo`. O `SenhaHash` **nunca** é
exposto. Autenticação igual ao REST (`Authorization: Bearer <accessToken>`).

## Relatório administrativo (Dapper)

`GET /api/admin/relatorios/usuarios` (acesso `Administrador`) usa **Dapper** (`QueryMultipleAsync`) para
consolidar todos os indicadores numa única viagem ao banco — evita o N+1 de várias consultas pequenas. O EF
Core fica no write side (integridade das regras); o Dapper, no read side do relatório (`Infrastructure/Dapper/`).

Para dados realistas na demo, habilite o `DevSeedService` em `appsettings.Development.json`
(`"DevSeed": { "Enabled": true }`): cria 50 usuários com datas distribuídas nos últimos 6 meses
(idempotente, e **sem** emitir eventos).
