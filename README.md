# Agenda de Contatos — API

API REST para gerenciamento de contatos: cadastro, consulta paginada com busca e filtros, edição, inativação/reativação, favoritos e importação assíncrona de arquivos CSV. O acesso é restrito a usuários autenticados (JWT).

O frontend fica em um repositório separado: [`unimed_contacts_record_web`](../unimed_contacts_record_web).

## Stack

| Item | Escolha |
|---|---|
| Linguagem / framework | C# / ASP.NET Core (.NET 10), controllers |
| Banco | PostgreSQL 17 |
| Acesso a dados | Dapper (consultas e gravações) |
| Esquema e migrations | Entity Framework Core (Npgsql), aplicadas na inicialização |
| Mensageria | RabbitMQ 4 (cliente oficial `RabbitMQ.Client`) |
| Leitura de CSV | CsvHelper |
| Autenticação | JWT Bearer; senha com hash PBKDF2 (hasher do ASP.NET Core Identity) |
| Documentação da API | Swagger (Swashbuckle) |
| Testes | xUnit, NSubstitute, Testcontainers |
| Infra local | Docker Compose |

## Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download) (versão fixada em `global.json`)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) em execução

## Como executar

### Opção 1: tudo com Docker

Clone os dois repositórios **lado a lado** (o compose constrói o frontend a partir de `../unimed_contacts_record_web`):

```
repos/
├── unimed_contacts_record_api/
└── unimed_contacts_record_web/
```

Na pasta da API:

```bash
docker compose up --build
```

| Serviço | Endereço |
|---|---|
| Frontend | http://localhost:3000 |
| API | http://localhost:5251 |
| Swagger (documentação interativa) | http://localhost:5251/swagger |
| PostgreSQL | `localhost:5432` |
| RabbitMQ (AMQP) | `localhost:5672` |
| Painel do RabbitMQ | http://localhost:15672 |

Detalhes do frontend (execução, build e integração com a API) estão no README do [`unimed_contacts_record_web`](../unimed_contacts_record_web).

### Opção 2: infraestrutura no Docker, API local

```bash
docker compose up -d postgres rabbitmq
dotnet run --project src/ContactsRecord.Api
```

A API sobe em http://localhost:5251, abre o Swagger no navegador e usa as configurações de `appsettings.Development.json`.

O Swagger fica disponível no ambiente `Development`, que é o usado pelo `dotnet run` e pelo Docker Compose.

### Credenciais de desenvolvimento

| Serviço | Usuário | Senha |
|---|---|---|
| API (login) | `admin@unimed.local` | `Admin@123` |
| PostgreSQL (banco `contacts_record`) | `contacts` | `contacts` |
| RabbitMQ (painel e AMQP) | `contacts` | `contacts` |

Servem apenas para desenvolvimento local.

### Variáveis de ambiente

Podem ser definidas antes de subir o compose, por exemplo `IMPORT_BATCH_SIZE=1000 docker compose up --build`:

| Variável no compose | Configuração na API | Padrão | Descrição |
|---|---|---|---|
| `JWT_EXPIRATION_MINUTES` | `Jwt__ExpirationMinutes` | `60` | Validade do token de acesso, em minutos |
| `IMPORT_BATCH_SIZE` | `Imports__BatchSize` | `500` | Linhas do CSV inseridas por ida ao banco na importação |

Fora do compose, use os nomes da coluna "Configuração na API".

### Acesso à API pelo Swagger

Todos os endpoints exigem um token JWT, exceto o login. Na inicialização a API cria o usuário configurado, se ele ainda não existir.

1. Execute `POST /api/auth/login` com as credenciais acima e copie o `accessToken` da resposta.
2. Clique em **Authorize**, cole o token e confirme.
3. Os demais endpoints passam a enviar o token automaticamente.

### Migrations

As migrations são aplicadas automaticamente quando a API inicia. Para criar uma nova:

```bash
dotnet tool restore
dotnet ef migrations add NomeDaMigration -p src/ContactsRecord.Infrastructure -s src/ContactsRecord.Api -o Persistence/Migrations
```

## Como executar os testes

```bash
dotnet test
```

| Projeto | Tipo | O que cobre |
|---|---|---|
| `ContactsRecord.Domain.Tests` | Unitário | Value objects; entidades `Contact`, `User` e `ContactImport` (incluindo as transições de status); classe base `Entity` |
| `ContactsRecord.Application.Tests` | Unitário | `ContactService`, `AuthService`, `ContactImportService` e `ImportFileService` com dependências substituídas (NSubstitute) e relógio fixo |
| `ContactsRecord.Infrastructure.Tests` | Unitário | Leitura de CSV: delimitador `,` e `;`, BOM, cabeçalho, aspas, linhas em branco |
| `ContactsRecord.IntegrationTests` | Integração | API real contra PostgreSQL e RabbitMQ reais: autenticação, CRUD, busca, filtros, paginação, concorrência, commit e rollback do Unit of Work e importação de ponta a ponta |

Os testes de integração precisam do Docker em execução: o Testcontainers sobe um PostgreSQL e um RabbitMQ descartáveis a cada execução, sem tocar no ambiente local.

Para rodar apenas os unitários:

```bash
dotnet test tests/ContactsRecord.Domain.Tests
dotnet test tests/ContactsRecord.Application.Tests
dotnet test tests/ContactsRecord.Infrastructure.Tests
```

## Endpoints

| Método | Rota | Descrição |
|---|---|---|
| `POST` | `/api/auth/login` | Troca e-mail e senha por um token de acesso (único endpoint público) |
| `GET` | `/api/contacts` | Lista contatos com paginação, busca e filtros |
| `POST` | `/api/contacts` | Cadastra um contato (`201`) |
| `PUT` | `/api/contacts/{id}` | Edita os dados de um contato |
| `PATCH` | `/api/contacts/{id}/deactivate` | Inativa |
| `PATCH` | `/api/contacts/{id}/activate` | Reativa |
| `PUT` | `/api/contacts/{id}/favorite` | Marca como favorito |
| `DELETE` | `/api/contacts/{id}/favorite` | Desmarca como favorito |
| `POST` | `/api/imports` | Envia um CSV (`multipart/form-data`, campo `file`) e enfileira o processamento (`202`) |
| `GET` | `/api/imports` | Histórico de importações, mais recentes primeiro |
| `GET` | `/api/imports/{id}` | Status e contadores de uma importação |
| `GET` | `/api/imports/{id}/errors` | Linhas não importadas, com linha, conteúdo original, tipo e motivo |

Parâmetros das listagens:

| Parâmetro | Onde | Padrão | Descrição |
|---|---|---|---|
| `page` | todas | `1` | Página (mínimo 1) |
| `pageSize` | todas | `20` | Itens por página (de 1 a 100) |
| `search` | `/api/contacts` | — | Busca por nome, e-mail ou telefone |
| `isFavorite` | `/api/contacts` | — | `true` / `false`; ausente = todos |
| `isActive` | `/api/contacts` | — | `true` / `false`; ausente = todos |
| `kind` | `/api/imports/{id}/errors` | — | `Invalid` / `Duplicate`; ausente = todos |

A forma mais simples de testar é pelo Swagger, em http://localhost:5251/swagger. Pela linha de comando:

```bash
TOKEN=$(curl -s -X POST http://localhost:5251/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@unimed.local","password":"Admin@123"}' | jq -r .accessToken)

curl -X POST http://localhost:5251/api/contacts \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name":"Ana Silva","email":"ana@example.com","phone":"(11) 98765-4321"}'

curl -X POST http://localhost:5251/api/imports \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@contatos.csv"
```

Erros seguem o padrão **ProblemDetails** ([RFC 9457](https://www.rfc-editor.org/rfc/rfc9457)):

| Status | Quando |
|---|---|
| `400` | Dados inválidos (nome vazio, e-mail ou telefone em formato inválido; arquivo vazio, sem extensão `.csv` ou acima de 5 MB) |
| `401` | Sem token, token inválido ou expirado, ou login com credenciais inválidas |
| `404` | Contato ou importação não encontrados |
| `409` | Telefone já pertence a outro contato (ativo ou inativo) |
| `503` | RabbitMQ indisponível ao enviar uma importação |

## Importação de CSV

### Formato do arquivo

```csv
nome;email;telefone
Ana Silva;ana@example.com;(11) 98765-4321
Bruno Souza;;11 3456-7890
```

- Cabeçalho obrigatório com as colunas `nome`, `email` e `telefone`, em qualquer ordem e sem diferenciar maiúsculas.
- Delimitador `,` ou `;` (detectado automaticamente; o Excel em português usa `;`).
- UTF-8, com ou sem **BOM** (os bytes invisíveis que o Excel coloca no início de arquivos "CSV UTF-8").
- Campos entre aspas são aceitos (`"Silva, Ana"`); linhas em branco são ignoradas.
- Até 5 MB por arquivo.

### Fluxo

```
POST /api/imports
  └─ valida o arquivo → grava a importação (Pending) com o conteúdo no PostgreSQL
  └─ publica { importId } na fila "contact-imports" do RabbitMQ → responde 202 + Location

ContactImportConsumer (escuta a fila)
  └─ ImportFileService.ProcessAsync(importId)
       ├─ Processing → lê o CSV → valida cada linha com as regras do cadastro manual
       ├─ insere em lotes de IMPORT_BATCH_SIZE (um único INSERT por lote)
       ├─ cada lote numa transação (Unit of Work): contatos, linhas não importadas e contadores juntos
       └─ Completed | CompletedWithErrors | Failed
  └─ confirma a mensagem (ack)

GET /api/imports/{id}          → acompanha o status
GET /api/imports/{id}/errors   → consulta as linhas com erro e os motivos
```

### Status da importação

| Status | Significado |
|---|---|
| `Pending` | Recebida e aguardando processamento |
| `Processing` | Em processamento; os contadores avançam a cada lote |
| `Completed` | Todas as linhas foram importadas |
| `CompletedWithErrors` | Terminou, mas há linhas inválidas ou duplicadas (veja `/errors`) |
| `Failed` | Não foi possível processar o arquivo (motivo em `failureReason`, por exemplo cabeçalho inválido) |

### Linhas não importadas

Cada linha não importada aparece em `/api/imports/{id}/errors` com o número da linha, o conteúdo original, o tipo e o motivo, deixando explícito o dado que causou o problema:

| Tipo | Exemplo de motivo |
|---|---|
| `Invalid` | `Telefone inválido. Informe DDD + número, ex.: (11) 98765-4321.` |
| `Duplicate` | `Telefone 11987654321 já cadastrado para o contato "Ana Silva" (ativo).` |
| `Duplicate` | `Telefone 11987654321 repetido no arquivo (já informado na linha 2).` |

**Reprocessar o mesmo arquivo não gera contatos duplicados:** no segundo envio nenhuma linha é importada, e todas aparecem como `Duplicate`, cada uma indicando o contato que já tem aquele telefone.

## Arquitetura

DDD pragmático em quatro camadas, com dependências apontando para o domínio:

```
Api ──► Application ──► Domain
 │           ▲
 └──► Infrastructure
```

| Camada | Responsabilidade |
|---|---|
| `Domain` | Entidades `Contact`, `User` e `ContactImport`; value objects `PhoneNumber` e `Email`. Regras de negócio e invariantes, sem dependências externas. |
| `Application` | Casos de uso (`ContactService`, `AuthService`, `ContactImportService`, `ImportFileService`), contratos (repositórios, `IImportQueue`, `ICsvContactReader`, `IPasswordHasher`, `ITokenService`) e DTOs. |
| `Infrastructure` | Repositórios com Dapper sobre uma sessão de banco por escopo (`DbSession`, que implementa o Unit of Work), esquema e migrations com EF Core, RabbitMQ (publicação e consumidor), leitura de CSV, JWT, hash de senha, registro de dependências. |
| `Api` | Controllers, política de autorização, tradução de exceções para ProblemDetails, inicialização do banco. |

DTOs têm o sufixo `Dto` explícito. Na Application: `ContactInputDto`, `ContactQueryDto`, `ContactOutputDto`, `LoginInputDto`, `LoginOutputDto`, `ContactImportOutputDto`, `ImportQueryDto`, `ImportRowErrorQueryDto`, `ImportRowErrorOutputDto`, `PagedQueryDto` e `PagedResultDto<T>`. Na Infrastructure, os `*PersistenceDto` representam as linhas das tabelas e nunca saem dessa camada.

## Decisões de arquitetura e benefícios

| Decisão | Benefício |
|---|---|
| DDD pragmático em camadas | Regras de negócio isoladas e testáveis sem banco; troca de tecnologia (banco, fila, leitor de CSV) sem tocar no domínio |
| Contratos na Application, implementações na Infrastructure | Dependências apontam para o domínio (SOLID, inversão de dependência); casos de uso testados com dublês |
| Sem MediatR/CQRS | Menos cerimônia e menos dependências para o tamanho do problema (YAGNI) |
| Value objects `PhoneNumber` e `Email` | Normalização e validação em um único lugar; o mesmo telefone escrito de formas diferentes é reconhecido como igual |
| Índice único no telefone | Unicidade garantida pelo banco mesmo com requisições simultâneas |
| EF Core só para esquema, Dapper para os dados | Migrations versionadas e automáticas, com SQL explícito e controle total das consultas |
| Uma classe de persistência por tabela | O esquema (EF) e o SQL (Dapper) usam a mesma definição e não divergem |
| JWT com política global de autenticação | API sem estado; todo endpoint novo nasce protegido |
| RabbitMQ para a importação | Processamento desacoplado da requisição, início imediato (sem polling), mensagens que sobrevivem a reinícios e escala horizontal adicionando consumidores |
| Arquivo no PostgreSQL, mensagem só com o id | Upload e registro na mesma base transacional; mensagens pequenas; sem volume extra para arquivos |
| `ImportFileService` separado do consumidor | O processamento é testável sem RabbitMQ; o consumidor só recebe e confirma mensagens |
| Lotes com `INSERT ... ON CONFLICT (phone) DO NOTHING RETURNING phone` | Um comando por lote (desempenho em arquivos grandes) e idempotência garantida pelo banco |
| Unit of Work (uma transação por lote) | Contadores sempre iguais aos dados gravados; importação interrompida continua do último lote salvo, sem reprocessar nem recontar |
| Tamanho do lote por variável de ambiente | Ajuste fino por ambiente sem recompilar |
| Mesmas regras no cadastro manual e na importação | Uma única fonte de validação (DRY); o CSV não aceita o que a tela recusaria |
| Docker Compose + Testcontainers | Ambiente completo com um comando; testes de integração contra serviços reais, sem afetar o ambiente local |

## Principais decisões técnicas

### Unicidade do telefone, inclusive em operações simultâneas

1. **Normalização no domínio:** o value object `PhoneNumber` guarda apenas dígitos. `+55 (11) 98765-4321`, `(11) 98765-4321` e `11987654321` são o mesmo número.
2. **Índice único no PostgreSQL** (`ix_contacts_phone`): é a garantia real. Quando duas requisições simultâneas tentam gravar o mesmo telefone, o banco aceita apenas uma; a violação (`23505`) é traduzida em `409 Conflict`.
3. **Verificação prévia no serviço:** antes de gravar, o `ContactService` consulta se o telefone já existe para devolver uma mensagem clara. Ela não substitui o índice, porque requisições concorrentes podem passar juntas por ela.

Os testes de integração comprovam os dois caminhos: 10 cadastros simultâneos com o mesmo telefone resultam em exatamente um `201`, e 10 gravações simultâneas direto no repositório (sem a verificação prévia) resultam em exatamente uma aceita.

### Regra de unicidade para contatos inativos

**O telefone de um contato inativo continua reservado.** Não é possível cadastrar outro contato com esse número, nem trocar o telefone de outro contato para ele. Nesses casos a API responde `409` com a orientação de reativar o contato existente. Na importação, a linha é registrada como `Duplicate`, indicando que o contato está inativo.

Motivos:

- Inativar não apaga o histórico: o contato inativo continua sendo aquela pessoa.
- Reativar nunca gera conflito, porque o número não pode ter sido reutilizado.
- A importação fica idempotente mesmo com contatos inativos.

Alternativa descartada: índice único parcial (`WHERE is_active`). Ele permitiria reaproveitar o número, mas a reativação passaria a poder falhar por conflito.

### Importação assíncrona com RabbitMQ

- **Por que RabbitMQ:** a requisição só registra a importação e responde `202`; o processamento acontece fora dela. O RabbitMQ entrega a mensagem imediatamente (sem polling), guarda as mensagens em disco e permite processar mais importações em paralelo adicionando instâncias da API.
- **O que o RabbitMQ não acelera:** o tempo de processamento de um único arquivo. Nesse caso o ganho vem da **inserção em lote** (um único `INSERT` por lote).
- **Alternativas consideradas:**
  - PostgreSQL como fila (`FOR UPDATE SKIP LOCKED`): sem infraestrutura nova, mas com polling.
  - Hangfire: mais uma biblioteca e tabelas próprias.
  - MassTransit: simplificaria o uso do RabbitMQ, mas a versão 9 passou a ter licença comercial.
- **Confiabilidade:**
  - Fila durável e mensagens persistentes: sobrevivem a um reinício do RabbitMQ.
  - *Publisher confirms*: o envio só é considerado concluído quando o RabbitMQ confirma que guardou a mensagem.
  - *Ack* manual: a mensagem só é confirmada depois do processamento. Se a API parar no meio, ela é entregue de novo e a importação **continua do último lote salvo**: cada lote é gravado numa transação junto com os contadores, então o número de linhas processadas é exato.
  - Uma importação já concluída é ignorada se a mesma mensagem chegar duas vezes.
  - Uma importação por vez em cada consumidor (`prefetch = 1`).
  - Falha inesperada: a importação é marcada como `Failed` e a mensagem é reprocessada uma única vez.
- **`ON CONFLICT DO NOTHING` é seguro:** o conflito é decidido pelo índice único, de forma atômica, no próprio PostgreSQL. A cláusula é restrita à coluna `phone`, então qualquer outra violação continua gerando erro, e o `RETURNING phone` informa quais linhas entraram, o que permite explicar cada duplicada. Os valores vão como parâmetros, sem risco de SQL injection.

### Avaliação de design patterns

Além dos padrões já presentes desde o início (Repository, Value Object, Factory Method em `Create`/`Restore`, Adapter para Identity/RabbitMQ/CsvHelper, Options, injeção de dependência e Producer/Consumer na fila), avaliei onde outros padrões **melhorariam a lógica** e implementei apenas o que fazia sentido agora, considerando o prazo do desafio:

| Padrão | Decisão | Motivo |
|---|---|---|
| **Unit of Work** | ✅ Implementado | Cada lote da importação era gravado em comandos independentes; uma falha no meio podia deixar contadores diferentes dos dados e obrigava a recomeçar o arquivo. Com uma transação por lote (`IUnitOfWork` + `DbSession` compartilhando conexão e transação entre os repositórios), os dados ficam sempre consistentes e a importação retoma do último lote salvo. Custo baixo. |
| **Transactional Outbox** | ⚠️ Avaliado, documentado como limitação | Eliminaria o caso de a API cair entre gravar a importação e publicá-la no RabbitMQ. Exige uma tabela de outbox e um publicador periódico; o ganho não compensa o custo dentro do prazo. |
| **State** (status da importação) | ❌ Não adotado | São 5 status e 4 transições, já protegidas por guardas na entidade; uma classe por estado acrescentaria arquivos sem ganho de clareza. |
| **Specification / Chain of Responsibility** (validação das linhas) | ❌ Não adotado | São três verificações simples em sequência (dados válidos, repetida no arquivo, já cadastrada); mais classes não deixariam a regra mais clara. |
| **Result pattern** (no lugar de exceções) | ❌ Não adotado | Mudaria todas as camadas para um ganho discutível; as exceções de domínio já são traduzidas de forma centralizada em ProblemDetails. |

### EF Core para o esquema, Dapper para os dados

- O EF Core define o esquema e aplica as migrations na inicialização da API, para que o ambiente suba pronto com um único comando.
- Todo o acesso a dados usa Dapper com SQL explícito (busca com `ILIKE`, paginação, inserção em lote com `unnest`).
- Cada tabela tem uma única classe de persistência: o EF gera o esquema a partir dela e o Dapper lê e grava nela.
- Colunas em `snake_case` (`is_active`), convenção do PostgreSQL. Status e tipos de erro são gravados como texto (`Completed`, `Duplicate`), para que as tabelas sejam legíveis sem o código.

### Autenticação

- **Login gera um JWT** assinado com HMAC-SHA256, com o id e o e-mail do usuário. A API valida emissor, audiência, assinatura e expiração, sem tolerância extra de tempo.
- **Protegido por padrão:** uma política global exige usuário autenticado em todos os endpoints. Só o login é marcado como `[AllowAnonymous]`.
- **Usuário criado pela configuração:** não há cadastro público. O usuário inicial vem das chaves `SeedUser:Email` e `SeedUser:Password`.
- **Mensagem única para falha de login:** e-mail inexistente e senha errada devolvem o mesmo `401`, para que não seja possível descobrir quais e-mails estão cadastrados.
- **Senha:** guardada apenas como hash (PBKDF2 com salt, do ASP.NET Core Identity, já incluso no framework).

### Outras decisões

- **Configuração validada na inicialização:** a API não sobe com configuração ausente ou inválida (chave JWT curta, tamanho de lote fora do intervalo, RabbitMQ sem endereço).
- **Transições idempotentes:** inativar um contato já inativo (ou favoritar um favorito) não gera erro nem altera nada.
- **`UpdatedAt`:** registra a data da última alteração de cada contato.
- **Ids GUID v7:** gerados no domínio e ordenáveis por data.
- **`TimeProvider` injetado:** permite testar datas com um relógio fixo.
- **Enums como texto no JSON:** `"status": "Completed"` em vez de números.

## Premissas adotadas

- **Celular e telefone são um único campo** (`phone`). O enunciado cita os dois, mas foram tratados como o mesmo campo para evitar complexidade sem ganho. A unicidade exigida para o celular vale para esse campo.
- **Telefone:** obrigatório, aceita fixo ou celular, com 10 ou 11 dígitos após a normalização; o prefixo `+55` é removido.
- **Nome:** obrigatório, até 150 caracteres.
- **E-mail:** opcional, normalizado para minúsculas e **não** único.
- **Agenda única:** todos os usuários acessam a mesma agenda.
- **Usuários:** existe apenas o usuário criado pela configuração; não há cadastro, perfis nem permissões diferentes. Alterar a senha na configuração não atualiza um usuário que já existe.
- **Busca:** por nome ou e-mail (sem diferenciar maiúsculas) e por telefone. A busca por telefone só é aplicada quando o termo não contém letras, para que "Ana 2" não traga todos os telefones que contêm o dígito 2.
- **Consulta de contatos:** segue o enunciado (paginação, busca e filtros); não há endpoint de busca de contato por id. Por isso o `POST /api/contacts` devolve `201` com o contato criado no corpo, sem cabeçalho `Location`.
- **Ordenação:** contatos por nome; importações da mais recente para a mais antiga; erros pela linha do arquivo.
- **Importação:** reprocessar significa enviar o mesmo arquivo de novo; não há endpoint de "reprocessar".

## Limitações e próximos passos

- **Sem refresh token:** quando o token expira, a API responde `401` e é preciso fazer login de novo.
- **Sem logout no servidor:** o JWT não tem estado; o logout descarta o token no cliente.
- **Sem limite de tentativas de login.**
- **Segredos de desenvolvimento** (chave JWT, senha do usuário inicial) ficam em `appsettings.Development.json`. Em produção devem vir de variáveis de ambiente ou de um cofre de segredos.
- **Sem padrão *outbox*** (avaliado e adiado, veja "Avaliação de design patterns"): a importação é gravada no banco e depois publicada no RabbitMQ. Se o RabbitMQ estiver indisponível, a importação é marcada como `Failed` e a API responde `503`; mas se a API cair exatamente entre as duas etapas, a importação fica em `Pending` sem mensagem. O *outbox* resolveria, ao custo de mais uma tabela e de um publicador periódico.
- **Sem fila de mensagens mortas (*dead-letter queue*):** uma mensagem que falha duas vezes é descartada (a importação fica registrada como `Failed`).
- **RabbitMQ obrigatório na inicialização:** a API não sobe sem conseguir se conectar a ele (no compose, ela espera o RabbitMQ ficar saudável).
- **Arquivos mantidos no banco:** o conteúdo dos CSVs enviados não é apagado após o processamento.
- **Busca sem índice de texto:** o `ILIKE` percorre a tabela, o que é adequado para o volume esperado. Para volumes grandes, um índice `pg_trgm` resolveria.
- **Paginação por offset:** simples e suficiente aqui; para listas muito grandes, a paginação por cursor seria mais eficiente.
- **Migrations na inicialização:** práticas para o desafio; em produção com várias instâncias, o ideal é aplicá-las em um passo separado do deploy.
