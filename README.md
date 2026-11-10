# Conserta Aí — API .NET (participação cidadã)

> **Global Solution 2026/2 — CitizenTech · FIAP 2TDS**
> Disciplina: **Advanced Business Development with .NET**

O **Conserta Aí** é um app em que o morador reporta problemas de zeladoria (buraco, poste apagado, lixo, calçada quebrada) e acompanha a resolução, enquanto o gestor da subprefeitura recebe uma fila já classificada e priorizada.

Esta API .NET é a **camada de participação cidadã** do ecossistema:
- **Apoios:** em vez de criar um report duplicado, o cidadão *apoia* a ocorrência que já existe. Isso aumenta a prioridade dela na fila do gestor e mostra a ele que não está sozinho.
- **Comentários:** o cidadão acrescenta informações (com etiquetas, numa relação N:N) e o gestor modera.
- **Linha do tempo:** cada ocorrência tem um histórico de participação, guardado no MongoDB.

É a mesma API consumida pelo app Mobile (Expo) e levada para o Azure na disciplina de DevOps.

| | |
|---|---|
| **Cidadão beneficiado** | Morador que vê o problema na rua e quer resolvê-lo sem ligar no 156 e ficar sem retorno |
| **Problema** | Reports duplicados, mal descritos e sem prioridade; o cidadão não sabe se alguém já reportou e desiste |
| **Valor entregue pela API .NET** | Deduplicação por **apoio** (1 ocorrência + N apoios), fila priorizada por impacto, comentários moderados e histórico de participação |
| **ODS** | **11** (cidades sustentáveis), **9** (infraestrutura) e **10** (redução das desigualdades: os apoios por bairro mostram onde o atendimento é pior) |

---

## Sumário

1. [Arquitetura](#1-arquitetura)
2. [Requisitos da disciplina → implementação](#2-requisitos-da-disciplina--implementação)
3. [Conceitos das aulas → onde estão no código](#3-conceitos-das-aulas--onde-estão-no-código)
4. [Estrutura da solução](#4-estrutura-da-solução)
5. [Como executar](#5-como-executar)
6. [Autenticação (JWT compartilhado com a API Java)](#6-autenticação-jwt-compartilhado-com-a-api-java)
7. [Endpoints](#7-endpoints)
8. [Exemplos de requisição (CRUD em JSON)](#8-exemplos-de-requisição-crud-em-json)
9. [Testes automatizados](#9-testes-automatizados)
10. [Roteiro de demonstração (apresentação)](#10-roteiro-de-demonstração-apresentação)
11. [Banco de dados](#11-banco-de-dados)
12. [Deploy no Azure (DevOps)](#12-deploy-no-azure-devops)
13. [Variáveis de ambiente e segredos](#13-variáveis-de-ambiente-e-segredos)
14. [Integrantes](#14-integrantes)


---

## 1. Arquitetura

```mermaid
flowchart LR
    APP["📱 App Mobile<br/>(Expo + Axios)"]

    subgraph JAVA["API Java (Spring Boot)"]
        JAUTH["POST /auth/login<br/>emite JWT (HS256)"]
        JOC["Ocorrências + IA"]
    end

    subgraph NET["API .NET 8 — Minimal API (este repositório)"]
        direction TB
        MW["Correlation ID → JWT → Rate Limit → Idempotência"]
        EP["Endpoints<br/>Apoios · Comentários · Etiquetas · Linha do tempo"]
        APL["Aplicação<br/>Serviços + Strategy de moderação"]
        DOM["Domínio<br/>Apoio · Comentário · Etiqueta (N:N)"]
        MW --> EP --> APL --> DOM
    end

    SQL[("Oracle / Azure SQL<br/>EF Core<br/>APOIO · COMENTARIO · ETIQUETA<br/>COMENTARIO_ETIQUETA · IDEMPOTENCY_KEY")]
    MONGO[("MongoDB Atlas<br/>collection atividades")]
    MQ{{"RabbitMQ<br/>exchange consertaai.eventos"}}
    OTEL["OpenTelemetry<br/>traces + métricas"]
    LOG["Serilog<br/>console + arquivo diário"]

    APP -- "Bearer JWT" --> JAVA
    APP -- "mesmo Bearer JWT" --> NET
    APL --> SQL
    APL --> MONGO
    APL -- "apoio.registrado / apoio.removido" --> MQ
    MQ -- "consome e recalcula prioridade" --> JOC
    NET -. "/health (check HTTP)" .-> JAVA
    NET -.-> OTEL
    NET -.-> LOG
```

- **Duas APIs, dois bancos, uma fila.** A API Java é dona de usuários e ocorrências. A API .NET guarda só o `id_ocorrencia` e o `id_usuario` (claim `sub` do token), **sem chave estrangeira entre bancos**.
- Cada apoio registrado ou retirado gera um evento no RabbitMQ (`apoio.registrado` / `apoio.removido`) com o **total atualizado**. A Java consome esse evento para atualizar `QTD_APOIOS` e recalcular a prioridade. O plano B é chamar `PATCH /internal/ocorrencias/{id}/apoios` via HttpClient; ele já está implementado e é escolhido por configuração.
- **Persistência poliglota:**
  - **Relacional (EF Core):** dados transacionais com regras de unicidade.
  - **MongoDB:** linha do tempo de leitura, paginada e ordenada direto no banco.

---

## 2. Requisitos da disciplina → implementação

| # | Requisito (pontuação) | Como foi implementado | Onde ver |
|---|---|---|---|
| 1 | **Minimal API RESTful + validações + códigos HTTP + OpenAPI/Swagger** (20) | <ul><li>Endpoints mapeados com `MapGet/MapPost/MapPut/MapDelete`.</li><li>Validação por DataAnnotations com o filtro `ValidacaoFiltro<T>` (400 `ValidationProblemDetails`).</li><li>Regras de domínio geram 400/403/404/409/422.</li><li>Todos os erros saem em ProblemDetails (RFC 7807) com `correlationId`.</li><li>O Swagger documenta parâmetros, contratos, respostas, o botão **Authorize** e o header `Idempotency-Key`.</li></ul> | `src/ConsertaAi.Api/Endpoints`, `Filtros/ValidacaoFiltro.cs`, `Respostas/Problemas.cs`, `Configuracao/SwaggerConfiguracao.cs` |
| 2 | **JWT: autenticação e autorização** (20) | <ul><li>`AddJwtBearer` valida assinatura HS256, emissor, audiência e expiração.</li><li>Policies **`CidadaoOnly`** e **`GestorOnly`**.</li><li>Respostas **401** (sem token, inválido ou expirado) e **403** (papel errado).</li><li>Aceita o token da API Java (`"roles": ["CIDADAO"]` ou `ROLE_CIDADAO`).</li><li>Regras de dono: só o autor edita o comentário; o autor ou o gestor podem apagar.</li></ul> | `Configuracao/AutenticacaoConfiguracao.cs`, `Seguranca/*` |
| 3 | **Idempotência** (15) | <ul><li>O `IdempotenciaMiddleware` lê o header `Idempotency-Key` nos POSTs de `/apoios` e `/comentarios`.</li><li>**Chave nova:** executa e grava status e corpo da resposta em `IDEMPOTENCY_KEY`.</li><li>**Chave repetida:** devolve a **mesma resposta**, sem gravar de novo, com o header `Idempotency-Replayed: true`.</li><li>**Mesma chave com outro corpo:** **422**. **Requisição simultânea:** **409**. **Sem header:** **400**.</li><li>O índice único `(ID_OCORRENCIA, ID_USUARIO)` em `APOIO` é uma segunda barreira contra duplicidade.</li></ul> | `Middlewares/IdempotenciaMiddleware.cs`, `Dominio/Entidades/RegistroIdempotencia.cs` |
| 4 | **Rate Limiting + HTTP 429** (15) | <ul><li>`AddRateLimiter` nativo, com janela fixa.</li><li>**5 POSTs por minuto por usuário** (partição pela claim `sub`), conforme o plano do grupo.</li><li>Limitador global de 120 requisições por minuto por usuário ou IP.</li><li>Resposta **429** com header **`Retry-After`** e ProblemDetails.</li><li>Gera uma métrica de rejeições.</li></ul> | `Configuracao/RateLimitConfiguracao.cs` |
| 5 | **Testes unitários e de integração com xUnit** (20) | **118 testes** no total: **83 unitários** (xUnit + Moq + FluentAssertions, padrão AAA, `[Fact]`/`[Theory]`) e **35 de integração** (`WebApplicationFactory` + banco em memória), cobrindo 200/201/204/400/401/403/404/409/422/429. | `tests/` |
| 6 | **Health Checks** (API, banco e serviços externos) (10) | Checks customizados (`IHealthCheck`):<ul><li>**banco_dados** (Oracle/Azure SQL): se falhar, **Unhealthy → 503**.</li><li>**mongodb**, **rabbitmq** e **api_java** (HTTP): se falharem, **Degraded → 200**.</li></ul>Endpoints `/health`, `/health/ready` e `/health/live`, com JSON detalhado (latência, provedor, erro). | `Infraestrutura/Health/*`, `Configuracao/HealthChecksConfiguracao.cs` |

**Tecnologias obrigatórias:**
- ASP.NET Core **Minimal API** (.NET 8);
- **EF Core 8**, com Oracle (`Oracle.EntityFrameworkCore`) e SQL Server/Azure SQL.

**Pontos que tiram nota e como foram evitados:**

| Risco | Prevenção |
|---|---|
| API não executa | Sobe com `dotnet run` sem nenhuma dependência (banco em memória) ou com `docker compose up`. Testado com Oracle, SQL Server, MongoDB e RabbitMQ reais. |
| Credenciais expostas | Nenhum segredo no repositório. Tudo vem de variável de ambiente, `dotnet user-secrets` ou variável secreta da pipeline (veja `.env.example`). O `.env` está no `.gitignore`. |
| Código desorganizado | Camadas Domínio, Aplicação, Infraestrutura, API e Testes, com um tipo por arquivo. |
| Solução fora do tema | CitizenTech de zeladoria urbana, ligada aos ODS 9, 10 e 11. |

---

## 3. Conceitos das aulas → onde estão no código

| Conceito (aula) | Aplicação no Conserta Aí |
|---|---|
| **SOLID / Clean Code** | <ul><li>**SRP:** endpoints só traduzem HTTP, serviços orquestram, entidades guardam as regras e repositórios persistem.</li><li>**DIP:** tudo é registrado no `IServiceCollection` por interfaces (`AddAplicacao`, `AddInfraestrutura`).</li><li>Sem *God classes*.</li><li>`Resultado<T>` evita cadeias de if/else e exceções para fluxo de negócio.</li></ul> |
| **Strategy Pattern (OCP + DIP)** | <ul><li>**Moderação de comentários:** `IRegraModeracao` com as estratégias `RegraTamanhoTexto`, `RegraLinguagemOfensiva` e `RegraAntiSpam`. Para criar uma regra nova basta registrar mais uma implementação; o `ModeradorComentario` não muda.</li><li>**Publicação de eventos:** `IPublicadorEventos` com as implementações `RabbitMqPublicadorEventos`, `HttpApiJavaPublicadorEventos` (plano B) e `LogPublicadorEventos`.</li></ul> |
| **EF Core + Oracle + N:N** | <ul><li>`Comentario` ⇄ `Etiqueta` pela tabela de junção `COMENTARIO_ETIQUETA`, com **chave primária composta** (`HasKey(ce => new { ce.ComentarioId, ce.EtiquetaId })`), como em `JogoPlataforma` na aula.</li><li>Consultas com **`.Include().ThenInclude()`**.</li><li>**Migrations** do EF Core aplicadas no Oracle (`Dados/Migrations`).</li></ul> |
| **MongoDB (MongoDB.Driver)** | <ul><li>Entidade `Atividade` com `[BsonId]` e `[BsonRepresentation(BsonType.String)]`.</li><li>`IMongoCollection<T>`, `Builders<T>.Filter` (ocorrência + tipo opcional) e `Builders<T>.Sort` (data).</li><li>`Skip/Limit` e `CountDocumentsAsync` executados no servidor.</li><li>Índice `ix_ocorrencia_data`.</li></ul> |
| **Paginação** | <ul><li>`PagedResult<T>` com `TotalPaginas`, `HasPreviousPage` e `HasNextPage`.</li><li>Filtros lidos da query string com `[AsParameters]`.</li></ul> |
| **HATEOAS (REST nível 3)** | <ul><li>`LinkDto(Href, Rel, Method)` e `ResourceEnvelope<T>(Data, Links)`.</li><li>URL base obtida por `IHttpContextAccessor`.</li><li>Links que **dependem do perfil e do estado**:<ul><li>`update` só para o autor do comentário;</li><li>`delete` para o autor ou o gestor;</li><li>`apoiar` ou `retirar-apoio` conforme o usuário já apoiou;</li><li>CRUD de etiquetas só para o gestor.</li></ul></li><li>Paginação com `self`, `first`, `previous`, `next` e `last`.</li></ul> |
| **JWT** | <ul><li>`TokenService` gera tokens com `ClaimTypes.Name`, `ClaimTypes.Role` e `HmacSha256`.</li><li>`TokenValidationParameters` completo.</li><li>`AddAuthorizationBuilder().AddPolicy(...)` e `RequireAuthorization("...")`.</li></ul> |
| **Health Checks** | Classes `BancoDadosHealthCheck`, `MongoDbHealthCheck`, `RabbitMqHealthCheck` e `ApiJavaHealthCheck`, todas implementando `IHealthCheck`. |
| **Serilog** | <ul><li>Logs estruturados (chave-valor) com `Enrich.FromLogContext()`.</li><li>Sink de console com template `[{Timestamp} {Level}] [{CorrelationId}]`.</li><li>Sink de **arquivo JSON com rotação diária** (`logs/consertaai-AAAAMMDD.log`).</li><li>`UseSerilogRequestLogging`.</li></ul> |
| **Correlation ID** | O `CorrelationIdMiddleware` lê ou gera o `X-Correlation-ID`, devolve na resposta, injeta no `LogContext` e grava como tag do trace. |
| **OpenTelemetry** | <ul><li>**ActivitySource:** spans customizados (`RegistrarApoio`, `RetirarApoio`, `CriarComentario`, `AtualizarComentario`, `RemoverComentario`, `PublicarEventoRabbitMq`) com tags.</li><li>**Meter:** contadores `apoios_registrados_total`, `apoios_retirados_total`, `comentarios_operacoes_total`, `idempotencia_requisicoes_repetidas_total`, `rate_limit_rejeicoes_total`, `erros_total` e `integracoes_falhas_total`.</li><li>**Instrumentação:** ASP.NET Core e HttpClient.</li><li>**Exportação:** `ConsoleExporter`, mais OTLP quando `OTEL_EXPORTER_OTLP_ENDPOINT` está definido.</li></ul> |
| **xUnit + Moq + FluentAssertions** | <ul><li>Padrão **AAA** e nomes no formato `Metodo_Cenario_ResultadoEsperado`.</li><li>`[Fact]`, `[Theory]` + `[InlineData]`/`[MemberData]`.</li><li>`Mock<IRepositorio>` com `.Verify(..., Times.Once)` e `Times.Never`.</li></ul> |

---

## 4. Estrutura da solução

```
├── src/
│   ├── ConsertaAi.Dominio/          # Entidades e regras puras, exceções de domínio e contratos (interfaces)
│   │   ├── Entidades/               # Apoio, Comentario, Etiqueta, ComentarioEtiqueta (N:N), RegistroIdempotencia, Atividade (Mongo)
│   │   ├── Interfaces/              # IApoioRepositorio, IComentarioRepositorio, IEtiquetaRepositorio, ...
│   │   └── Comum/PagedResult.cs
│   ├── ConsertaAi.Aplicacao/        # Casos de uso
│   │   ├── Servicos/                # ApoioServico, ComentarioServico, EtiquetaServico, AtividadeServico
│   │   ├── Moderacao/               # Strategy: IRegraModeracao + 3 estratégias
│   │   ├── Integracoes/             # IPublicadorEventos, IConsultaOcorrencia (contratos externos)
│   │   ├── Observabilidade/         # AplicacaoMetricas (Meter + ActivitySource)
│   │   └── Dtos/                    # Requests/Responses, LinkDto, ResourceEnvelope
│   ├── ConsertaAi.Infraestrutura/   # EF Core (Oracle/SQL Server), MongoDB, RabbitMQ, HTTP e Health Checks
│   │   ├── Dados/                   # AppDbContext, Mapeamentos, Migrations (Oracle), inicializador
│   │   ├── Repositorios/            # Repositórios EF Core + MongoAtividadeRepositorio
│   │   ├── Mensageria/              # Publicadores (RabbitMQ, HTTP, Log)
│   │   └── Health/                  # BancoDados, MongoDb, RabbitMq, ApiJava
│   └── ConsertaAi.Api/              # Minimal API
│       ├── Endpoints/               # Apoios, Comentários, Etiquetas, Atividades, Auth (testes)
│       ├── Middlewares/             # CorrelationId, Idempotência, tratador global de exceções
│       ├── Configuracao/            # JWT, Rate Limit, Swagger, Serilog/OTel, Health Checks
│       ├── Hateoas/                 # GeradorLinks
│       └── Program.cs
├── tests/
│   ├── ConsertaAi.Tests.Unit/         # Domínio + Aplicação (xUnit, Moq, FluentAssertions)
│   └── ConsertaAi.Tests.Integration/  # Endpoints (WebApplicationFactory + banco em memória)
├── scripts/
│   ├── infra.sh                     # Azure CLI: RG, Azure SQL, ACR, ACI
│   ├── script-bd.sql                # DDL do Azure SQL / SQL Server
│   └── script-bd-oracle.sql         # DDL do Oracle (gerado das Migrations)
├── dockerfiles/Dockerfile
├── docker-compose.yml               # API + Oracle + MongoDB + RabbitMQ
├── azure-pipeline.yml               # CI (build, testes, imagem, artefato)
├── azure-pipeline-release.yml       # CD (deploy na Container Instance)
└── integrantes.txt                  # identificação do grupo (RM, nome, turma)
```

---

## 5. Como executar

### Opção A — rápida, sem nenhuma dependência (banco em memória)

Pré-requisito: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet run --project src/ConsertaAi.Api
```

Abra **http://localhost:5080/swagger**.

No ambiente *Development*:
- o banco é EF Core InMemory;
- a linha do tempo fica em memória;
- os eventos vão para o log;
- o `POST /auth/token-teste` está habilitado;
- a chave JWT é gerada aleatoriamente a cada execução, então os tokens deixam de valer quando a API reinicia.

### Opção B — ambiente completo (Oracle + MongoDB + RabbitMQ) com Docker

1. Copie o modelo de variáveis:

   ```bash
   cp .env.example .env
   ```

2. Edite o `.env` e **troque todas as senhas**. Se alguma porta já estiver em uso, ajuste `API_PORTA`, `MONGO_PORTA` etc.
3. Suba o ambiente:

   ```bash
   docker compose up -d --build
   ```

4. Acesse:
   - Swagger: **http://localhost:8080/swagger**
   - Health: **http://localhost:8080/health**
   - Painel do RabbitMQ: **http://localhost:15672**

Ao iniciar, a API aplica as **Migrations do EF Core no Oracle**. Ela tenta conectar de novo várias vezes enquanto o banco ainda está subindo.

### Opção C — Azure SQL / SQL Server

```bash
dotnet user-secrets set "ConnectionStrings:ConsertaAi" "Server=...;Database=...;User Id=...;Password=..." --project src/ConsertaAi.Api
```

```bash
dotnet user-secrets set "JWT_SECRET" "<mesmo segredo da API Java>" --project src/ConsertaAi.Api
```

```bash
dotnet run --project src/ConsertaAi.Api --environment Production --BancoDados:Provedor=SqlServer
```

Com o provedor `SqlServer`, a API cria o schema sozinha (`EnsureCreated`). Também é possível criar antes com `scripts/script-bd.sql`.

### Rodar os testes

```bash
dotnet test
```

### Teste ponta a ponta com a API rodando (38 verificações, PowerShell)

Com a API no ar (Opção A), rode em outro terminal:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/testar-api.ps1
```

Para testar no Docker compose, use `-Base http://localhost:8080`. O script percorre:
- JWT (401/403);
- validações (400);
- idempotência (repetição, 409 e 422);
- HATEOAS e CRUD de comentários e etiquetas;
- linha do tempo;
- rate limit (429 com `Retry-After`);
- Swagger.

---

## 6. Autenticação (JWT compartilhado com a API Java)

- A **API Java** emite o token em `POST /auth/login` (HS256), com `sub` = id do usuário e `roles` = `CIDADAO` ou `GESTOR`.
- A **API .NET** valida o token com o **mesmo segredo, issuer e audience**, lidos das variáveis `JWT_SECRET`, `JWT_ISSUER` e `JWT_AUDIENCE`.
- Formatos de papel aceitos:
  - `"roles": ["CIDADAO"]`
  - `"role": "GESTOR"`
  - `ROLE_CIDADAO` (Spring Security)
  - `ClaimTypes.Role`
- O app faz login uma vez e envia o mesmo `Authorization: Bearer <token>` para as duas APIs.

**Para testes e demonstração sem a API Java:** `POST /auth/token-teste` com `{ "usuarioId": "10", "nome": "Maria", "perfil": "CIDADAO" }`.
- O token emitido tem o mesmo formato.
- O endpoint só existe quando `Auth:EmissorTokenTesteHabilitado=true`. Em produção real, use `false`.

| Situação | Resposta |
|---|---|
| Sem token, token inválido ou expirado | **401** (`WWW-Authenticate: Bearer`) |
| Token válido, mas o papel não atende à policy (ex.: GESTOR fazendo `POST /apoios`) | **403** |
| Usuário editando o comentário de outra pessoa | **403** |

---

## 7. Endpoints

| Método e rota | Acesso | Descrição | Respostas |
|---|---|---|---|
| `POST /apoios` | **CIDADAO** · `Idempotency-Key` · 5/min | Apoia uma ocorrência e publica `apoio.registrado` | 201, 400, 401, 403, 404\*, 409, 422, 429 |
| `DELETE /apoios/{ocorrenciaId}` | **CIDADAO** | Retira o apoio e publica `apoio.removido` | 204, 404 |
| `GET /ocorrencias/{id}/apoios` | autenticado | Total de apoios e se o usuário já apoiou (links `apoiar` / `retirar-apoio`) | 200 |
| `GET /ocorrencias/{id}/comentarios?pagina=&tamanhoPagina=&ascendente=` | autenticado | Lista paginada com HATEOAS | 200, 400 |
| `GET /comentarios/{id}` | autenticado | Detalhe com etiquetas (N:N) | 200, 404 |
| `POST /comentarios` | autenticado · `Idempotency-Key` · 5/min | Cria o comentário (moderado por Strategy) | 201, 400, 404\*, 422, 429 |
| `PUT /comentarios/{id}` | **autor** | Edita o texto e as etiquetas | 200, 400, 403, 404 |
| `DELETE /comentarios/{id}` | **autor ou GESTOR** | Remove o comentário | 204, 403, 404 |
| `GET /etiquetas`, `GET /etiquetas/{id}` | autenticado | Etiquetas disponíveis | 200, 404 |
| `POST/PUT/DELETE /etiquetas` | **GESTOR** | CRUD de etiquetas | 201/200/204, 403, 409 |
| `GET /ocorrencias/{id}/atividades?tipo=&ascendente=&pagina=&tamanhoPagina=` | autenticado | Linha do tempo (MongoDB) | 200, 400 |
| `GET /health`, `/health/ready`, `/health/live` | público | Saúde da API e das dependências | 200, 503 |
| `POST /auth/token-teste` | público (só testes) | Emite um JWT de teste | 200, 400 |

\* Com `ApiJava:ValidarOcorrencias=true`, a ocorrência é validada em `GET /ocorrencias/{id}` na API Java:
- se ela não existir, a resposta é **404**;
- se estiver RESOLVIDA ou CANCELADA, a resposta é **422**;
- se a Java estiver fora do ar, a participação **não** é bloqueada (resiliência).

---

## 8. Exemplos de requisição (CRUD em JSON)

```http
### Token de teste (cidadão)
POST /auth/token-teste
Content-Type: application/json

{ "usuarioId": "10", "nome": "Maria", "perfil": "CIDADAO" }
```

```http
### CREATE — Apoio (idempotente)
POST /apoios
Authorization: Bearer <token>
Idempotency-Key: 3f1c2d9a-7b1e-4c55-9d0a-1a2b3c4d5e6f
Content-Type: application/json

{ "ocorrenciaId": 501 }
```

Resposta `201 Created` (`Location: /ocorrencias/501/apoios`):

```json
{
  "data": { "id": "d467e5aa-...", "ocorrenciaId": 501, "usuarioId": "10", "dataApoio": "2026-11-10T17:17:07Z", "totalApoios": 1 },
  "links": [
    { "href": "http://localhost:8080/ocorrencias/501/apoios", "rel": "self", "method": "GET" },
    { "href": "http://localhost:8080/apoios/501", "rel": "retirar-apoio", "method": "DELETE" },
    { "href": "http://localhost:8080/ocorrencias/501/comentarios", "rel": "comentarios", "method": "GET" },
    { "href": "http://localhost:8080/ocorrencias/501/atividades", "rel": "atividades", "method": "GET" }
  ]
}
```

```http
### READ — Resumo de apoios
GET /ocorrencias/501/apoios
Authorization: Bearer <token>

### DELETE — Retirar apoio
DELETE /apoios/501
Authorization: Bearer <token>
```

```http
### CREATE — Comentário com etiquetas (N:N)
POST /comentarios
Authorization: Bearer <token>
Idempotency-Key: 9b2f...
Content-Type: application/json

{
  "ocorrenciaId": 501,
  "texto": "O buraco aumentou depois da chuva",
  "etiquetaIds": [ "8f6f6c1e-3b1a-4c55-9d0a-1a2b3c4d5e01", "8f6f6c1e-3b1a-4c55-9d0a-1a2b3c4d5e04" ]
}

### READ — Lista paginada
GET /ocorrencias/501/comentarios?pagina=1&tamanhoPagina=10
Authorization: Bearer <token>

### UPDATE — Somente o autor
PUT /comentarios/{id}
Authorization: Bearer <token do autor>
Content-Type: application/json

{ "texto": "O buraco aumentou depois da chuva de ontem", "etiquetaIds": [ "8f6f6c1e-3b1a-4c55-9d0a-1a2b3c4d5e01" ] }

### DELETE — Autor ou gestor
DELETE /comentarios/{id}
Authorization: Bearer <token>
```

```http
### CRUD de etiquetas (GESTOR)
POST /etiquetas            { "nome": "Acessibilidade", "descricao": "Afeta cadeirantes" }
PUT  /etiquetas/{id}       { "nome": "Acessibilidade", "descricao": "Afeta cadeirantes e idosos" }
DELETE /etiquetas/{id}
GET  /etiquetas
```

Etiquetas iniciais (seed):

| Id | Nome |
|---|---|
| `8f6f6c1e-3b1a-4c55-9d0a-1a2b3c4d5e01` | Risco a pedestres |
| `...5e02` | Problema recorrente |
| `...5e03` | Informação nova |
| `...5e04` | Piorou |

Exemplo de erro (sempre no formato ProblemDetails):

```json
{ "type": "https://httpstatuses.io/409", "title": "Conflito", "status": 409,
  "detail": "Você já apoiou esta ocorrência.", "instance": "/apoios",
  "correlationId": "66a98ea4-...", "traceId": "89aa7217..." }
```

---

## 9. Testes automatizados

```bash
dotnet test
```

São **118 testes**, todos aprovados:

| Projeto | Testes | O que cobre |
|---|---|---|
| `ConsertaAi.Tests.Unit` | **83** | <ul><li>**Domínio** (`[Fact]`/`[Theory]`): validações de Apoio, Comentário, Etiqueta, RegistroIdempotencia e PagedResult; autoria e regra de remoção do comentário.</li><li>**Aplicação** (`Mock<IRepositorio>` + `.Verify`): apoio novo, duplicado (409, `Times.Never`), índice único violado, ocorrência inexistente ou encerrada, retirada de apoio, moderação (estratégias isoladas e combinadas), etiquetas inexistentes, edição e remoção por autor, outro cidadão e gestor, resiliência quando o MongoDB está fora.</li></ul> |
| `ConsertaAi.Tests.Integration` | **35** | `WebApplicationFactory<Program>` + EF Core InMemory:<ul><li>**201** com HATEOAS; **401** (sem token, assinatura inválida, expirado); **403** (papel errado, não autor); **409** (apoio duplicado); **400** (validação, sem `Idempotency-Key`); **422** (mesma chave e outro corpo); **429** + `Retry-After`; **204/404**.</li><li>Idempotência conferida **no banco** (uma linha só).</li><li>Token no formato da API Java; paginação e links; CRUD de etiquetas.</li><li>Linha do tempo; health checks; Swagger; Correlation ID.</li></ul> |

Os mesmos testes rodam no CI do Azure DevOps (`azure-pipeline.yml`), com publicação dos resultados e da cobertura.

---

## 10. Roteiro de demonstração (apresentação)

1. **Health.** Abra `GET /health` e mostre `banco_dados`, `mongodb` e `rabbitmq` em Healthy. Depois rode `docker compose stop rabbitmq` e mostre o status **Degraded**: a API segue funcionando.
2. **JWT.**
   - Chame `POST /apoios` sem token → **401**.
   - Gere um token GESTOR e repita → **403**.
   - Gere um token CIDADÃO, clique em **Authorize** e repita → **201**.
3. **Idempotência.** Envie o mesmo `POST /apoios` duas vezes com a mesma `Idempotency-Key`: as respostas são iguais e a segunda traz `Idempotency-Replayed: true`. Depois confira o banco:

   ```sql
   SELECT * FROM APOIO WHERE ID_OCORRENCIA = 501;
   ```

   Aparece **uma linha só**. Também mostre `SELECT * FROM IDEMPOTENCY_KEY;`.
4. **Regra de negócio.** Envie o mesmo apoio com uma chave nova → **409** "Você já apoiou esta ocorrência".
5. **Rate limit.** Faça 6 POSTs em menos de 1 minuto → **429** com `Retry-After`.
6. **HATEOAS.**
   - Em `GET /ocorrencias/501/apoios`, o link muda de `apoiar` para `retirar-apoio`.
   - Nos comentários, só o autor recebe `update`.
7. **N:N.** Crie um comentário com etiquetas e confira no banco:

   ```sql
   SELECT C.TEXTO, E.NOME FROM COMENTARIO C JOIN COMENTARIO_ETIQUETA CE ON CE.ID_COMENTARIO = C.ID JOIN ETIQUETA E ON E.ID = CE.ID_ETIQUETA;
   ```

8. **MongoDB.** Abra `GET /ocorrencias/501/atividades?tipo=ApoioRegistrado`: o filtro, a ordenação e a paginação rodam no Mongo.
9. **RabbitMQ.** No painel, a fila `consertaai.apoios` recebe `apoio.registrado` com o total de apoios.
10. **Observabilidade.**
    - Logs no console com `[CorrelationId]` e o arquivo `logs/consertaai-AAAAMMDD.log`.
    - Spans e métricas no console (`Observabilidade:ExportarConsole=true`).
11. **Testes.** Rode `dotnet test` e mostre os 118 aprovados.

---

## 11. Banco de dados

**Tabelas (relacional):**

| Tabela | Conteúdo |
|---|---|
| `APOIO` | Índice único `(ID_OCORRENCIA, ID_USUARIO)` |
| `COMENTARIO` | Comentários das ocorrências |
| `ETIQUETA` | Nome único |
| `COMENTARIO_ETIQUETA` | Junção N:N, com chave primária composta e FKs em cascata |
| `IDEMPOTENCY_KEY` | Respostas armazenadas dos POSTs idempotentes |

**Oracle (padrão das aulas):**
- Ao iniciar, a API aplica as **Migrations** do EF Core (`src/ConsertaAi.Infraestrutura/Dados/Migrations`).
- O DDL equivalente está em `scripts/script-bd-oracle.sql`.
- Para criar uma nova migration:

  ```bash
  dotnet tool restore
  ```

  ```bash
  dotnet ef migrations add NomeDaMudanca --project src/ConsertaAi.Infraestrutura --startup-project src/ConsertaAi.Api --output-dir Dados/Migrations
  ```

**Azure SQL / SQL Server (deploy no Azure):**
- Use `BancoDados__Provedor=SqlServer`.
- O DDL está em `scripts/script-bd.sql`.
- Para regenerar o DDL:

  ```bash
  EF_PROVEDOR=SqlServer dotnet ef dbcontext script --project src/ConsertaAi.Infraestrutura --startup-project src/ConsertaAi.Api
  ```

**MongoDB:**
- Collection `atividades`, com o índice `{ ocorrenciaId: 1, dataRegistro: -1 }`.
- Formato de um documento:

  ```json
  { "_id": "d400fe01-...", "ocorrenciaId": 501, "tipo": "ApoioRegistrado", "usuarioId": "10", "descricao": "Apoio registrado. Total de apoios: 1.", "recursoId": "...", "dataRegistro": "2026-11-10T17:27:28Z" }
  ```

---

## 12. Deploy no Azure (DevOps)

1. **Infraestrutura:** rode `scripts/infra.sh` (Azure CLI). Ele cria:
   - o Resource Group `rg-consertaai`;
   - o Azure SQL (Server + Database);
   - o Azure Container Registry;
   - a primeira imagem;
   - a Azure Container Instance da API.

   Opcionalmente cria também o RabbitMQ em outra Container Instance (`CRIAR_RABBITMQ=true`).
2. **CI** (`azure-pipeline.yml`): dispara no merge na `main` e executa restore → build → **testes xUnit publicados** → publish → `docker build/push` no ACR → artefato `drop`.
3. **CD** (`azure-pipeline-release.yml`): dispara automaticamente quando o CI gera um novo artefato e atualiza a Container Instance com a imagem `:<BuildId>`.
4. **Segredos:** ficam no Variable Group `consertaai-dotnet`:
   - `SQL_CONNECTION_STRING`, `JWT_SECRET` e `MONGO_URI`, como variáveis **secretas**;
   - `ACR_NAME`, `ACR_LOGIN_SERVER`, `RESOURCE_GROUP`, `ACI_NAME`, `DNS_LABEL`, `JWT_ISSUER` e `JWT_AUDIENCE`.
5. **Conferência do CRUD:** use o Swagger da instância (`http://<dns>.<região>.azurecontainer.io:8080/swagger`) e confira cada operação com `SELECT` no Azure SQL.

---

## 13. Variáveis de ambiente e segredos

| Variável | Exemplo | Obrigatória |
|---|---|---|
| `JWT_SECRET` | mesmo segredo da API Java (32+ caracteres) | sim (fora de Development) |
| `JWT_ISSUER` / `JWT_AUDIENCE` | `consertaai` / `consertaai-app` | não (esses são os valores padrão) |
| `BancoDados__Provedor` | `Oracle` \| `SqlServer` \| `InMemory` | não (padrão: Oracle) |
| `ConnectionStrings__ConsertaAi` | Oracle: `User Id=...;Password=...;Data Source=host:1521/FREEPDB1` | sim (exceto InMemory) |
| `MongoDbSettings__ConnectionString` | `mongodb+srv://usuario:senha@cluster.mongodb.net` | não (sem ela, a linha do tempo fica em memória) |
| `Eventos__Modo` | `RabbitMq` \| `HttpApiJava` \| `Log` | não (padrão: Log) |
| `RabbitMq__HostName` / `UserName` / `Password` (ou `RabbitMq__Uri`) | `rabbitmq` | quando `Eventos__Modo=RabbitMq` |
| `ApiJava__BaseUrl` | `https://api-java.exemplo.com` | não (habilita o check `api_java`) |
| `ApiJava__ValidarOcorrencias` | `true` | não |
| `RateLimit__EscritasPorJanela` | `5` | não |
| `Auth__EmissorTokenTesteHabilitado` | `false` em produção | não |

> **Nenhuma senha, chave ou connection string fica no repositório.** O `appsettings.json` contém apenas valores não sensíveis. Use:
> - `.env`, que está no `.gitignore` e tem o modelo em `.env.example`;
> - `dotnet user-secrets`;
> - variáveis secretas da pipeline.

---

## 14. Integrantes

| RM | Nome |
|---|---|
| 556649 | Matheus Arazin de Oliveira |
| 565597 | Artur Pioli Silva |
| 563454 | Kevin Martins Campos |
| 566058 | Pedro Gabriel Claes |

A identificação completa (RM, nome e turma) está em [`integrantes.txt`](integrantes.txt).

---

