# AlertDoors Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar um bot .NET que encontre vagas júnior e pleno, além de vagas com senioridade não informada, no LinkedIn, remotas no Brasil ou presenciais/híbridas na cidade de São Paulo, e notifique novidades por Discord e/ou e-mail a cada hora no GCP.

**Architecture:** Um container Linux executa um ciclo de coleta, filtragem, persistência e notificação como Cloud Run Job. Cloud Scheduler dispara o job; Firestore mantém vagas, entregas e a concessão de execução; Secret Manager fornece credenciais. O coletor do LinkedIn é uma integração experimental cuja viabilidade será validada antes de investir no restante da implementação.

**Tech Stack:** C#/.NET 10, HttpClient, AngleSharp 1.8.2, Google.Cloud.Firestore 4.4.0, MailKit 4.18.0, xUnit, Docker, Cloud Run Jobs, Cloud Scheduler, Firestore Native e Secret Manager. Fixar as versões resolvidas nos projetos e arquivos `packages.lock.json`; usar o SDK 10.0.401 como referência inicial.

**Spec:** [Desenho do produto](../specs/2026-09-19-alertdoors-design.md).

**Status:** Plano preparado para revisão. O repositório está em fase de planejamento, sem implementação do bot, coleta real ou implantação. A preparação do repositório não ativa recursos no GCP.

## Global Constraints

Requisitos transcritos do desenho; valem para todas as tarefas:

- Senioridade: júnior e pleno.
- Modalidades: remoto; presencial e híbrido em São Paulo–SP.
- Fonte inicial: LinkedIn.
- Intervalo: 1 hora.
- C# com .NET 10, disponível no ambiente. Sem painel web nesta versão.
- Remoto: vagas destinadas ao Brasil. São Paulo–SP significa a cidade, não todo o estado.
- Não contornar CAPTCHA, login ou bloqueios com proxies ou cookies de contas.
- Não depender do disco do container para persistência.
- Falha no e-mail não deve repetir um envio já confirmado no Discord.
- Não prometer entrega exatamente uma vez.

Detalhes operacionais obrigatórios: um ciclo por processo; uma tarefa Cloud Run por execução; timeout de 10 minutos; concessão de 15 minutos; 1 vCPU e 512 MiB iniciais; agendamento UTC `0 * * * *`. Não há candidatura automática, painel, bot de chat, análise por IA ou contratação de fornecedor de vagas nesta versão.

## Review Focus

1. HTML de login com HTTP 200 ou formato alterado deve virar erro de coleta, nunca zero vagas silenciosamente — tarefas 1 e 2.
2. “São Paulo” sem cidade/UF inequívocas em vagas presenciais/híbridas, remoto restrito a outro país e cargo fora de desenvolvimento não devem produzir falso positivo. `mid-senior` genérico gera alerta com senioridade a confirmar; título misto “Pleno/Sênior” permanece elegível — tarefa 3.
3. Disparo duplicado, queda após enviar e expiração da concessão não podem apagar pendências nem repetir entregas já confirmadas — tarefas 4 e 6.
4. Uma falha na segunda mensagem de um resumo, títulos longos e conteúdo com HTML ou menções devem preservar o progresso e a integridade da notificação — tarefas 5 e 6.
5. Vaga sem data publicada e backlog maior que 20 precisam ter estado explícito; limite de resumo não equivale a entrega, e dados ausentes não podem ser inventados — tarefas 2, 4 e 6.

## Sequência e marcos

| Ordem | Entrega | Critério para avançar |
| --- | --- | --- |
| 1 | Prova da fonte e estrutura mínima | Extração real verificável ou impedimento documentado |
| 2 | Coletor limitado e previsível | Falhas distinguíveis de resultados vazios |
| 3 | Filtros do usuário | Casos positivos, negativos e indeterminados testados |
| 4 | Persistência e concorrência | Reinício e duas execuções concorrentes validados no emulador |
| 5 | Discord e e-mail | Resumos e falhas parciais testados sem destinatários reais |
| 6 | Ciclo completo | Execução única, demonstração e simulação sem envio |
| 7 | Container e implantação reproduzível | Imagem e agendamentos verificados; infraestrutura ainda não ativada |
| 8 | Validação no GCP e ativação | Coleta e notificações reais confirmadas, com histórico persistido |

Dependências: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8. Priorizar execução sequencial nesta sessão: o projeto é pequeno e os contratos se encadeiam. O primeiro marco deve acontecer antes de construir banco e notificadores. A prova na rede do Cloud Run é um segundo marco obrigatório antes da ativação; se houver projeto GCP disponível mais cedo, antecipar uma execução manual do coletor no container, sem habilitar Scheduler ou notificações.

## Estrutura de arquivos

Todos os caminhos abaixo são relativos à raiz do projeto. Criar somente os arquivos da tarefa em execução.

```text
AlertDoors.slnx
global.json
Directory.Build.props
.gitignore
.dockerignore
Dockerfile
compose.yaml
.env.example
README.md
src/AlertDoors/AlertDoors.csproj
src/AlertDoors/Program.cs
src/AlertDoors/appsettings.json
src/AlertDoors/Configuration/BotOptions.cs
src/AlertDoors/Configuration/OptionsValidator.cs
src/AlertDoors/Jobs/JobPosting.cs
src/AlertDoors/Jobs/JobFilter.cs
src/AlertDoors/Sources/SourceContracts.cs
src/AlertDoors/Sources/LinkedInJobSource.cs
src/AlertDoors/Sources/LinkedInPageParser.cs
src/AlertDoors/Sources/LinkedInQueryBuilder.cs
src/AlertDoors/Sources/BoundedHttpClient.cs
src/AlertDoors/State/StateContracts.cs
src/AlertDoors/State/FirestoreStateStore.cs
src/AlertDoors/Notifications/NotificationContracts.cs
src/AlertDoors/Notifications/DiscordNotifier.cs
src/AlertDoors/Notifications/EmailNotifier.cs
src/AlertDoors/Notifications/MessageFormatter.cs
src/AlertDoors/Execution/RunWindow.cs
src/AlertDoors/Execution/BotRunner.cs
src/AlertDoors/Execution/DemoJobSource.cs
tests/AlertDoors.Tests/AlertDoors.Tests.csproj
tests/AlertDoors.Tests/SourceTests.cs
tests/AlertDoors.Tests/FilterTests.cs
tests/AlertDoors.Tests/NotificationTests.cs
tests/AlertDoors.Tests/RunnerTests.cs
tests/AlertDoors.Tests/ScheduleTests.cs
tests/AlertDoors.Tests/Support/JobSamples.cs
tests/AlertDoors.Tests/Support/StubHttpHandler.cs
tests/AlertDoors.Tests/Support/RecordingNotifier.cs
tests/AlertDoors.Tests/Support/InMemoryStateStore.cs
tests/AlertDoors.Tests/Fixtures/linkedin-jobs.html
tests/AlertDoors.Tests/Fixtures/linkedin-empty.html
tests/AlertDoors.Tests/Fixtures/linkedin-blocked.html
tests/AlertDoors.IntegrationTests/AlertDoors.IntegrationTests.csproj
tests/AlertDoors.IntegrationTests/FirestoreStateTests.cs
tests/AlertDoors.IntegrationTests/SmtpTests.cs
infra/terraform/versions.tf
infra/terraform/variables.tf
infra/terraform/main.tf
infra/terraform/iam.tf
infra/terraform/scheduler.tf
infra/terraform/outputs.tf
infra/terraform/terraform.tfvars.example
scripts/Build-Image.ps1
scripts/Verify-Schedule.ps1
docs/operations/linkedin-feasibility.md
docs/operations/deployment.md
docs/operations/acceptance.md
```

Projetos de teste devem usar nomes de classe e de métodos consistentes com os filtros de execução descritos abaixo. `JobSamples`, `StubHttpHandler`, `RecordingNotifier` e `InMemoryStateStore` são apoios de teste escritos nas tarefas que os usam; não substituir Firestore por memória no executável de produção.

## Contratos compartilhados

Estabelecer os contratos abaixo nas tarefas proprietárias. Usar namespaces por pasta sob `AlertDoors`; tipos de suporte de teste ficam em `AlertDoors.Tests.Support`. Os exemplos de testes pressupõem os `using` correspondentes e `using Xunit;`.

```csharp
// Jobs/JobPosting.cs — tarefa 1
public enum WorkMode { Unknown, Remote, Hybrid, Onsite }
public sealed record JobPosting(
    string Id, Uri Url, string Title, string Company,
    string Location, string? CountryCode, WorkMode Mode,
    string? SeniorityText, DateTimeOffset? PublishedAt,
    DateTimeOffset ObservedAt, string Description);

// Sources/SourceContracts.cs — tarefa 1
public enum SourceStatus { Success, Blocked, RateLimited, SchemaChanged, Unavailable }
public sealed record SearchRequest(string Keywords, string Location,
    WorkMode Mode, DateTimeOffset Since, int MaxPages);
public sealed record SourceResult(SourceStatus Status,
    IReadOnlyList<JobPosting> Jobs, bool Truncated, DateTimeOffset? RetryAt);
public interface IJobSource
{
    Task<SourceResult> SearchAsync(SearchRequest request, CancellationToken ct);
}

// Jobs/JobFilter.cs — tarefa 3
public enum MatchDecision { Include, Exclude, Unknown }
public sealed record FilterResult(MatchDecision Decision, string Reason);

// Notifications/NotificationContracts.cs — tarefa 4 define tipos;
// tarefa 5 implementa notificadores.
public enum Channel { Discord, Email }
public sealed record NotificationBatch(string Id, Channel Channel,
    IReadOnlyList<JobPosting> Jobs, string Subject, string Body);
public sealed record SendReceipt(string? ProviderId, DateTimeOffset SentAt);
public interface INotifier
{
    Channel Channel { get; }
    IReadOnlyList<NotificationBatch> Prepare(IReadOnlyList<JobPosting> jobs);
    Task<SendReceipt> SendAsync(NotificationBatch batch, CancellationToken ct);
}

// State/StateContracts.cs — tarefa 4
public sealed record RunLease(string OwnerId, string WindowId,
    DateTimeOffset ExpiresAt);
public interface IStateStore
{
    Task<RunLease?> TryAcquireAsync(string windowId, string ownerId,
        DateTimeOffset now, CancellationToken ct);
    Task EnsureLeaseAsync(RunLease lease, CancellationToken ct);
    Task QueueAsync(RunLease lease, IReadOnlyList<JobPosting> jobs,
        IReadOnlyList<Channel> channels, CancellationToken ct);
    Task<IReadOnlyList<JobPosting>> GetPendingAsync(RunLease lease,
        Channel channel, int limit, CancellationToken ct);
    Task MarkDeliveredAsync(RunLease lease, NotificationBatch batch,
        SendReceipt receipt, CancellationToken ct);
    Task FinishAsync(RunLease lease, bool succeeded, CancellationToken ct);
}
```

`Id` é a identidade normalizada da fonte, por exemplo `linkedin:12345`, nunca o título da vaga. O parser só constrói URLs HTTPS canônicas de vagas cujo host seja LinkedIn; detalhes remotos não devem ser carregados de URLs arbitrárias encontradas no HTML. Datas ausentes permanecem nulas. Todos os horários de controle são UTC; apresentação usa `America/Sao_Paulo` quando necessário. `TimeProvider` deve ser injetável onde o tempo altera comportamento.

## Task 1: validar a fonte com uma ferramenta mínima

**Files:** criar solução, `global.json`, `Directory.Build.props`, `.gitignore`, projetos principal/unitário, `Program.cs`, `JobPosting.cs`, `SourceContracts.cs`, `LinkedInPageParser.cs`, `SourceTests.cs`, os três HTMLs de fixture e `docs/operations/linkedin-feasibility.md`.

**Interfaces:** produzir `JobPosting`, `SourceResult` e `IJobSource`; `LinkedInPageParser.Parse(string html, DateTimeOffset observedAt)` retorna `SourceResult`. O modo inicial de linha de comando é `probe`, sem Firestore e sem envio.

- [ ] Inspecionar as instruções do projeto e o estado Git antes de editar. Reutilizar o repositório existente e preservar a documentação e as exclusões de dados sensíveis já configuradas. Não reinicializar o repositório.
- [ ] Criar a estrutura mínima e fixar SDK/dependências. Usar comandos separados, sem misturar geração de arquivo e instalação em um único comando encadeado:

```powershell
dotnet new sln --name AlertDoors --format slnx
dotnet new console --framework net10.0 --name AlertDoors --output src/AlertDoors
dotnet new xunit --framework net10.0 --name AlertDoors.Tests --output tests/AlertDoors.Tests
dotnet sln AlertDoors.slnx add src/AlertDoors/AlertDoors.csproj tests/AlertDoors.Tests/AlertDoors.Tests.csproj
dotnet add tests/AlertDoors.Tests/AlertDoors.Tests.csproj reference src/AlertDoors/AlertDoors.csproj
dotnet add src/AlertDoors/AlertDoors.csproj package AngleSharp --version 1.8.2
```

Conteúdo de `global.json`: `{"sdk":{"version":"10.0.401","rollForward":"latestPatch"}}`. Em `Directory.Build.props`, habilitar `Nullable`, `ImplicitUsings` e `RestorePackagesWithLockFile`. Ignorar `bin/`, `obj/`, `.env`, `artifacts/`, `.terraform/`, `*.tfstate*`, `*.tfvars` e arquivos de credencial; preservar os arquivos `.example`.

- [ ] Fazer uma consulta manual limitada das páginas públicas de pesquisa do LinkedIn, sem login, com HTTP timeout de 20 segundos e até 2 MiB por resposta. Partir de `https://www.linkedin.com/jobs/search/` com palavras-chave e localização codificadas; determinar seletores e paginação a partir do HTML efetivamente recebido, não de uma API imaginada. Não usar um endpoint interno não verificado como requisito do plano. Registrar status HTTP, horário, URL pública e campos disponíveis; não salvar cookies ou cabeçalhos de autenticação.
- [ ] Salvar uma amostra HTML mínima anonimizada para cada formato observado; criar amostras sintéticas explicitamente identificadas para bloqueio e vazio quando não houver exemplos reais. Uma fixture sintética não prova acesso à fonte.
- [ ] Escrever primeiro os testes: HTML bloqueado com status 200 deve retornar `Blocked`; HTML inesperado deve retornar `SchemaChanged`; somente um estado vazio reconhecido deve retornar `Success` sem vagas. Exemplo:

```csharp
[Fact]
public void UnrecognizedHtmlIsNotAValidEmptyResult()
{
    var parser = new LinkedInPageParser();
    var result = parser.Parse("<html><body>unexpected response</body></html>",
        DateTimeOffset.Parse("2026-09-19T12:00:00Z"));
    Assert.Equal(SourceStatus.SchemaChanged, result.Status);
    Assert.Empty(result.Jobs);
}
```

- [ ] Executar `dotnet test tests/AlertDoors.Tests --filter FullyQualifiedName~SourceTests`. Confirmar falha pelo comportamento ainda ausente; corrigir problemas de projeto antes de contar a etapa como teste de comportamento.
- [ ] Implementar a classificação e extração usando AngleSharp. Extrair ID, título, empresa e URL; preservar campos ausentes. Desabilitar carregamento automático de recursos externos no parser. O `probe` deve imprimir JSON sem dados secretos e terminar com código 0 para resultado interpretável, 3 para bloqueio/limitação e 4 para falha de formato/rede.
- [ ] Executar novamente os testes e `dotnet run --project src/AlertDoors -- probe`. Conferir manualmente pelo menos uma vaga extraída contra sua página pública; isso valida a extração, não significa que ela atende aos filtros do usuário. Registrar se houve acesso aos campos necessários para o filtro.
- [ ] Documentar resultado em `linkedin-feasibility.md`: verificável, bloqueado ou inconclusivo, com evidências e limitações. Se bloqueado/inconclusivo, não avançar na integração como se estivesse funcional; apresentar a alternativa de provedor de dados sem contratar nada. Se o acesso local funcionar, registrar explicitamente que a validação na rede GCP ainda falta.
- [ ] Revisar o diff e criar commit da entrega da tarefa, se Git estiver configurado. Não fazer push nem criar PR nesta etapa.

**Aceite:** prova real de extração e classificação de falhas; ausência de vagas júnior/pleno na amostra não é falha de transporte. A pasta `artifacts/` pode conter o relatório bruto local, mas não deve entrar no commit.

## Task 2: transformar a prova em coletor limitado

**Files:** criar `LinkedInJobSource.cs`, `LinkedInQueryBuilder.cs`, `BoundedHttpClient.cs`, `Support/StubHttpHandler.cs`; modificar `SourceTests.cs`, `Program.cs` e fixtures observadas quando necessário.

**Interfaces:** implementar `IJobSource.SearchAsync`; `LinkedInQueryBuilder.Build(SearchRequest request)` retorna `Uri`; `BoundedHttpClient` recebe `HttpClient` e `TimeProvider`, respeita cancelamento e limita respostas, tempo e tentativas. Usar `SourceResult.Truncated` para indicar paginação ou orçamento esgotados.

- [ ] Escrever testes com `StubHttpHandler`, uma subclasse de `HttpMessageHandler` que recebe `Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>` no construtor e a executa em `SendAsync`. Cobrir timeout, 403, 429 com `Retry-After`, 503, cancelamento, resposta maior que 2 MiB, página repetida, resultado parcial e redirecionamento para login. Testar a codificação de `C#`:

```csharp
[Fact]
public void QueryPreservesCSharpInsteadOfCreatingAFragment()
{
    var request = new SearchRequest("C#", "São Paulo, São Paulo, Brazil",
        WorkMode.Onsite, DateTimeOffset.Parse("2026-09-18T12:00:00Z"), 2);
    var uri = LinkedInQueryBuilder.Build(request);
    Assert.Equal("", uri.Fragment);
    Assert.Contains("C%23", uri.Query);
}
```

- [ ] Executar `dotnet test tests/AlertDoors.Tests --filter FullyQualifiedName~SourceTests` e confirmar os novos casos falhando.
- [ ] Gerar a query com `Uri.EscapeDataString` por valor. Consultar três termos (`.NET`, `C#`, `ASP.NET`) em três escopos (remoto Brasil, híbrido São Paulo, presencial São Paulo), deduplicando IDs entre consultas. Filtros oferecidos pela fonte reduzem candidatos; a decisão final pertence à tarefa 3. Só usar parâmetros de modalidade/data que a prova tenha verificado.
- [ ] Implementar no máximo 3 páginas por consulta e 36 detalhes por ciclo, com orçamento total de coleta de 6 minutos e pausa de 1 segundo entre requisições. Detalhes devem priorizar candidatos que já tenham evidência de tecnologia e senioridade. Parar em página repetida ou final explícito. Um conjunto parcial com falha deve preservar vagas já interpretadas e retornar status não bem-sucedido.
- [ ] Permitir no máximo 3 tentativas em erros transitórios, com pausas de 2 e 4 segundos. Para 429, respeitar `Retry-After` válido; se a espera exceder o orçamento restante, retornar `RateLimited` com `RetryAt`, sem nova requisição antecipada. Sem cabeçalho utilizável, encerrar o ciclo com limitação explícita. Não repetir 401/403/CAPTCHA. Restringir redirecionamentos aos hosts LinkedIn permitidos e classificar login como bloqueio.
- [ ] Consultar as últimas 24 horas em todos os ciclos quando o filtro da fonte funcionar; caso não funcione, coletar com orçamento limitado e aplicar data quando conhecida. Data desconhecida deve ser mostrada como “publicação não informada”, sem classificar a vaga como recém-publicada. Uma vaga antiga conhecida e anterior à janela não entra na fila inicial.
- [ ] Reexecutar os testes. Fazer uma única consulta real de diagnóstico, sem notificações, e registrar contagens e truncamento. Revisar/commitar os arquivos desta tarefa.

**Aceite:** o coletor não faz requisições ilimitadas nem trata bloqueio como lista vazia; consegue preservar resultados parciais sem esconder a falha.

## Task 3: implementar os filtros de vagas

**Files:** criar `JobFilter.cs`, `FilterTests.cs`, `Support/JobSamples.cs`; modificar apenas os tipos de evidência necessários em `JobPosting.cs`, preservando o contrato público.

**Interfaces:** `JobFilter.Evaluate(JobPosting job)` retorna `FilterResult`; `JobSamples.Create(string title)` retorna a vaga sintética abaixo, ajustável com `with` nos testes.

```csharp
public static JobPosting Create(string title) => new(
    "linkedin:123", new Uri("https://www.linkedin.com/jobs/view/123/"),
    title, "Empresa de teste", "São Paulo, SP", "BR", WorkMode.Hybrid,
    null, null, DateTimeOffset.Parse("2026-09-19T12:00:00Z"), "ASP.NET Core");
```

- [ ] Escrever testes de tabela para `Júnior`, `Junior`, `Jr.`, `Pleno`, `Pl.`, inglês `Junior`/`Mid-level`, sênior, liderança, estágio, conflito entre título e senioridade e `mid-senior` sem título conclusivo. Exemplo:

```csharp
[Theory]
[InlineData("Desenvolvedor .NET Júnior", MatchDecision.Include)]
[InlineData("Desenvolvedor C# Pleno", MatchDecision.Include)]
[InlineData("Desenvolvedor .NET Sênior", MatchDecision.Exclude)]
[InlineData("Desenvolvedor .NET", MatchDecision.Include)]
public void RequiresEvidenceForTargetSeniority(string title, MatchDecision expected)
{
    Assert.Equal(expected, new JobFilter().Evaluate(JobSamples.Create(title)).Decision);
}
```

- [ ] Testar São Paulo cidade versus Campinas/Osasco/São Paulo estado; remoto Brasil versus remoto restrito aos EUA; alerta com modalidade ou senioridade não identificável quando o cargo, a tecnologia e a localização forem compatíveis; `Unknown` para país/localização sem evidência. Incluir títulos mistos “Júnior / Sênior” e “Pleno / Sênior” como elegíveis quando não houver outro conflito, e categoria genérica `mid-senior` como alerta com senioridade a confirmar.
- [ ] Executar `dotnet test tests/AlertDoors.Tests --filter FullyQualifiedName~FilterTests` e confirmar falhas antes da implementação.
- [ ] Normalizar caixa, espaços e acentos para comparação; manter texto original para apresentação. Usar tokens ou expressões com limites de palavra para senioridade, evitando encontrar `pl` em palavras como “aplicação”. Exigir tecnologia no título ou descrição com tokens `.NET`, `ASP.NET`, `dotnet` ou `C#`, nunca uma substring genérica como `net` em “internet”. Aceitar desenvolvedor/developer e engenheiro de software/software engineer no título ou descrição; programador e analista de sistemas/desenvolvimento no título; e “analista .NET” ou “engenheiro .NET” no título. Outras stacks na mesma vaga são permitidas. Senioridade ou modalidade ausente gera alerta identificado para confirmação, desde que a localização tenha evidência suficiente.
- [ ] Definir precedência: exclusão inequívoca de local/nível retorna `Exclude`; conflito de evidência retorna `Unknown`; ausência apenas de senioridade ou modalidade retorna `Include` com aviso de confirmação; falta de evidência de localização retorna `Unknown`. Registrar `Reason` estável, por exemplo `outside_city`, `remote_country_missing` ou `matched_mode_unverified`.
- [ ] Reexecutar testes; examinar os resultados filtrados do diagnóstico com contagens por motivo. Revisar/commitar a entrega.

**Aceite:** nenhum campo de preferência é inferido apenas porque a consulta do LinkedIn usou determinado filtro.

## Task 4: persistir histórico, pendências e concessões no Firestore

**Files:** criar `StateContracts.cs`, `FirestoreStateStore.cs`, `NotificationContracts.cs`, projeto de testes de integração, `FirestoreStateTests.cs`, `compose.yaml`; adicionar Google.Cloud.Firestore ao projeto principal.

**Interfaces:** implementar `IStateStore` e definir os contratos de notificação. `FirestoreStateStore(FirestoreDb db, TimeProvider clock, string rootCollection = "alertdoors")` permite testes isolados. Os documentos descritos abaixo ficam sob `{rootCollection}/state/`; cada teste fornece uma coleção raiz exclusiva. Aplicação real nunca pode usar o emulador por engano: exigir modo de desenvolvimento explícito e `FIRESTORE_EMULATOR_HOST` nos testes.

- [ ] Adicionar `Google.Cloud.Firestore` 4.4.0 e o projeto de integração. Configurar o emulador em `compose.yaml` com a imagem oficial do Google Cloud CLI contendo os emuladores; verificar a tag e fixar seu digest na implementação. Executar apenas `gcloud beta emulators firestore start --host-port=0.0.0.0:8080 --project=alertdoors-test` dentro do container, expondo `127.0.0.1:8080` ao host. O ambiente de teste usa `alertdoors-test`, nunca o projeto real.
- [ ] Modelar documentos: `runs/{windowId}` com estado/timestamps; `control/lease` com proprietário, janela e expiração; `jobs/{idHash}` com campos normalizados e primeira/última observação; `deliveries/{channel-idHash}` com vaga, estado `pending|sent`, timestamp de criação, tentativas e recibo. Usar SHA-256 hexadecimal de `JobPosting.Id` para os IDs de documento. Não salvar HTML bruto nem dados de candidatos.
- [ ] Criar o teste de concorrência abaixo usando um `FirestoreDb` conectado ao emulador e uma instância `store` por teste; falhar claramente se o emulador não estiver disponível, sem marcar o teste como aprovação:

```csharp
var now = DateTimeOffset.UtcNow;
var attempts = await Task.WhenAll(
    store.TryAcquireAsync("20260919-1200", "worker-a", now, CancellationToken.None),
    store.TryAcquireAsync("20260919-1200", "worker-b", now, CancellationToken.None));
Assert.Single(attempts.Where(x => x is not null));
```

- [ ] Adicionar testes: aquisição após expirar 15 minutos; proprietário antigo não libera concessão nova; janela concluída não readquire; repetir `QueueAsync` não volta `sent` para `pending`; `MarkDeliveredAsync` de Discord não altera e-mail; 21 pendências com limite 20 deixam uma pendente; falta de data publicada não muda ordenação por criação da pendência. Confirmar falhas com `dotnet test tests/AlertDoors.IntegrationTests --filter FullyQualifiedName~FirestoreStateTests`.
- [ ] Implementar aquisição e finalização por transação. Comparar proprietário e expiração em cada mutação protegida, usando `TimeProvider` atual, e lançar falha de concessão perdida antes de novos envios. `EnsureLeaseAsync` lê o documento de controle e exige proprietário, janela e validade compatíveis; não renova a concessão silenciosamente. Todas as leituras da transação antecedem escritas. Não executar HTTP/SMTP dentro de callbacks transacionais.
- [ ] Implementar upsert que preserva primeira observação e entregas confirmadas; organizar pendências por canal, estado e criação, com consulta limitada e índice documentado/declarado na infraestrutura. Gravar recibos e estado `sent` por lote apenas após confirmação do destino. `FinishAsync(..., false, ...)` libera concessão sem marcar janela concluída; backlog normal maior que 20 não é falha.
- [ ] Reiniciar o processo cliente no teste e confirmar que histórico continua no mesmo emulador. Não confundir reiniciar o cliente com persistência do próprio container do emulador. Reexecutar a suíte e revisar/commitar.

**Aceite:** proteção contra concorrência é transacional e persiste fora do processo; testes em memória não substituem os testes de integração.

## Task 5: implementar Discord e e-mail com progresso por mensagem

**Files:** criar `DiscordNotifier.cs`, `EmailNotifier.cs`, `MessageFormatter.cs`, `NotificationTests.cs`, `SmtpTests.cs`; modificar `compose.yaml` para incluir servidor SMTP de teste isolado e sem encaminhamento externo; adicionar MailKit 4.18.0.

**Interfaces:** ambos os canais implementam `INotifier`. `Prepare` retorna lotes ordenados com conjuntos disjuntos de vagas; `SendAsync` envia um lote e só retorna recibo após sucesso confirmado. IDs de lote são hash do canal e IDs ordenados das vagas. Nenhum notificador escreve diretamente no Firestore.

- [ ] Escrever testes de tamanho do Discord, caracteres especiais, títulos longos, menção `@everyone`, HTML no nome de empresa, campos ausentes e falha no segundo lote. Criar `StubHttpHandler` da tarefa 2 para capturar o JSON enviado e verificar:

```csharp
using var payload = System.Text.Json.JsonDocument.Parse(capturedBody);
Assert.Equal(0, payload.RootElement.GetProperty("allowed_mentions")
    .GetProperty("parse").GetArrayLength());
Assert.True(payload.RootElement.GetProperty("content").GetString()!.Length <= 1900);
```

`capturedBody` é o corpo lido no handler do teste antes de responder HTTP 200 com um ID de mensagem sintético. Usar uma URL falsa de webhook no cliente com handler em memória; não chamar Discord em testes unitários.

- [ ] Confirmar falhas com `dotnet test tests/AlertDoors.Tests --filter FullyQualifiedName~NotificationTests`.
- [ ] Implementar resumo em português com cargo, empresa, local/modalidade e link canônico. Dividir Discord em mensagens de até 1.900 caracteres, sem cortar links; truncar campos textuais excessivos. Desabilitar menções, usar execução do webhook com `wait=true` e ler o ID do recibo. Respeitar `Retry-After`/`retry_after`; se o prazo de execução não permitir aguardar, manter o lote pendente.
- [ ] Para e-mail, produzir partes texto e HTML escapado, usar MimeKit para endereços e cabeçalhos e MailKit para SMTP. TLS obrigatório em produção; permitir SMTP sem TLS apenas no modo de teste com host local explicitamente configurado. Credenciais devem ser específicas para SMTP, conforme o provedor; não presumir que senha comum de Gmail/Outlook funciona.
- [ ] Adicionar teste SMTP real contra o servidor local do `compose.yaml`, com domínio `.test`, e confirmar título, links e escape do HTML na mensagem recebida. Nunca usar destinatário pessoal no teste. Validar que erro de TLS/autenticação resulta em falha e não em recibo de entrega.
- [ ] Executar testes unitários e `dotnet test tests/AlertDoors.IntegrationTests --filter FullyQualifiedName~SmtpTests`. Revisar/commitar.

**Aceite:** um resumo dividido pode confirmar sua primeira mensagem sem perder o estado da segunda; nenhuma configuração secreta aparece no log.

## Task 6: integrar o ciclo completo e modos seguros de diagnóstico

**Files:** criar `BotOptions.cs`, `OptionsValidator.cs`, `RunWindow.cs`, `BotRunner.cs`, `DemoJobSource.cs`, `RunnerTests.cs`, `RecordingNotifier.cs`, `InMemoryStateStore.cs`, `appsettings.json`, `.env.example`, `README.md`; modificar `Program.cs` para composição e comandos.

**Interfaces:** `RunWindow.Id(DateTimeOffset now)` retorna `string`; `BotRunner(IJobSource source, IStateStore state, JobFilter filter, IReadOnlyList<INotifier> notifiers, TimeProvider clock)` expõe `Task<int> RunAsync(CancellationToken ct)`. `RecordingNotifier` implementa `INotifier` e permite registrar lotes e lançar erro por índice. `InMemoryStateStore` implementa o mesmo contrato de estado exclusivamente para unitários.

- [ ] Escrever `RunnerTests` que combinem fonte falsa com resultados fixos, estado e notificadores em memória. Cobrir: dois ciclos sem repetição; mesma vaga em duas buscas; e-mail falha e Discord confirma; erro na segunda mensagem; 21 vagas deixam backlog; fonte bloqueada produz código não zero e ainda permite tentar pendências pré-existentes; concessão recusada envia zero mensagens; cancelamento preserva pendências; campo de configuração ausente falha antes de qualquer rede.
- [ ] Testar janela UTC nos limites de dia:

```csharp
[Theory]
[InlineData("2026-09-19T00:39:59Z", "20260919-0000")]
[InlineData("2026-09-19T00:40:00Z", "20260919-0040")]
[InlineData("2026-09-19T23:59:59Z", "20260919-2320")]
public void MapsClockToFortyMinuteWindow(string input, string expected)
{
    Assert.Equal(expected, RunWindow.Id(DateTimeOffset.Parse(input)));
}
```

- [ ] Executar `dotnet test tests/AlertDoors.Tests --filter "FullyQualifiedName~RunnerTests"` e observar os casos falhando.
- [ ] Implementar o cálculo de janela e o encadeamento. Núcleo do cálculo:

```csharp
long seconds = now.ToUnixTimeSeconds();
long start = seconds - seconds % (40 * 60);
return DateTimeOffset.FromUnixTimeSeconds(start).UtcDateTime
    .ToString("yyyyMMdd-HHmm", System.Globalization.CultureInfo.InvariantCulture);
```

No runner: adquirir concessão; coletar as nove combinações limitadas da tarefa 2; filtrar; persistir aprovadas antes de enviar; buscar até 20 pendências por canal; preparar lotes; validar concessão; enviar e confirmar cada lote sequencialmente; finalizar com sucesso somente se coleta e canais terminarem sem erro. Capturar falha de um canal para continuar o outro. Usar orçamento interno de 9 minutos, deixando margem ao timeout Cloud Run. Cancelamento e finalização devem usar operações limitadas; não iniciar envio após perder a concessão. Chamar `EnsureLeaseAsync` imediatamente antes de cada envio; não se apoiar somente no objeto antigo em memória. Essa verificação não torna um envio externo transacional: uma queda após o destino aceitar ainda pode causar duplicata, conforme a limitação do desenho.

- [ ] Disponibilizar comandos: `probe` (inspeção real limitada), `run --dry-run` (coleta e filtros reais, sem banco e sem envio), `demo` (dados sintéticos e saída marcada como demonstração, sem efeitos externos), `run` (ciclo real com estado e notificadores). Nenhum deles inicia loop de espera de uma hora.
- [ ] Validar configuração antes de usar rede: projeto Firestore presente em produção; pelo menos um canal habilitado no modo real; webhook HTTPS válido se Discord habilitado; host, porta, TLS, remetente e destinatário válidos se e-mail habilitado. Desabilitar ambos por padrão. Variáveis previstas: `GOOGLE_CLOUD_PROJECT`, `ALERTDOORS_MODE`, `DISCORD_ENABLED`, `DISCORD_WEBHOOK_URL`, `EMAIL_ENABLED`, `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASSWORD`, `EMAIL_FROM`, `EMAIL_TO`. `.env.example` lista nomes, valores não secretos e instruções locais; `.env` não é carregado automaticamente sem suporte implementado e documentado.
- [ ] Logs JSON devem conter `runId`, `windowId`, estágio, duração, contagens e motivo de falha. Nunca registrar webhook, senha, cabeçalho Authorization, corpo de e-mail ou exceção de HTTP contendo URL secreta. Usar categorias e mensagens sanitizadas. Códigos de saída: 0 sucesso ou execução duplicada ignorada; 2 configuração; 3 fonte bloqueada/limitada; 4 outra falha de fonte; 5 persistência/envio; 130 cancelamento. O processo deve aguardar todo o ciclo antes de terminar.
- [ ] Executar `dotnet test AlertDoors.slnx`, com emuladores disponíveis, e `dotnet run --project src/AlertDoors -- demo`. Executar `run --dry-run` uma vez e conferir resultados reais sem afirmar que houve envio. Revisar/commitar.

**Aceite:** o mesmo fluxo funciona em execução única e contém estado suficiente para retomar pendências; nenhum sucesso fictício oculta coleta com erro.

## Task 7: preparar container, infraestrutura e roteiro de publicação

**Files:** criar `Dockerfile`, `.dockerignore`, todos os arquivos de `infra/terraform`, `Build-Image.ps1`, `Verify-Schedule.ps1`, `ScheduleTests.cs` e `docs/operations/deployment.md`; modificar README.

**Interfaces:** imagem executa `dotnet AlertDoors.dll run` por padrão; aceita `probe`, `demo` e `run --dry-run` como argumentos. Terraform recebe `project_id`, `region`, `image_uri`, IDs/versões de segredos e opções não secretas; não recebe valores secretos. `schedules_enabled` começa `false`; habilitação é a última mudança da tarefa 8.

- [ ] Testar o agendamento por 48 horas, incluindo virada do dia. A verificação deve ler a expressão de `infra/terraform/scheduler.tf`, para detectar divergência da infraestrutura, e aceitar apenas os cinco campos e listas/passos usados pelo projeto. Esperar 48 horários e diferença de 60 minutos entre todos eles. Executar com `dotnet test tests/AlertDoors.Tests --filter FullyQualifiedName~ScheduleTests`.
- [ ] Criar o Dockerfile multi-stage abaixo; adicionar cópia de arquivos de lock junto ao restante da solução e ajustar contexto conforme necessário. A imagem final contém somente o publish. Fixar digests após validar a imagem na implementação:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet restore src/AlertDoors/AlertDoors.csproj --locked-mode
RUN dotnet publish src/AlertDoors/AlertDoors.csproj -c Release --no-restore -o /app
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "AlertDoors.dll"]
CMD ["run"]
```

- [ ] Excluir de contexto Docker `.git`, `.env`, `artifacts`, estados Terraform e `bin/obj`. Implementar `Build-Image.ps1` com parâmetros obrigatórios `ImageTag` e chamadas `docker build --platform linux/amd64 -t $ImageTag .`; verificar códigos de saída. Não iniciar upload nesse script.
- [ ] Construir `docker build --platform linux/amd64 -t alertdoors:local .` e executar `docker run --rm alertdoors:local demo`. Testar `run` sem configuração e esperar código 2. Inspecionar imagem para garantir ausência de credenciais e execução com usuário sem privilégios.
- [ ] Declarar em Terraform: APIs necessárias, Artifact Registry, Firestore Native (ou importar banco existente), índice de pendências, duas contas de serviço, Secret Manager sem payload de segredos, Cloud Run Job e os dois Scheduler jobs pausados. Fixar versão compatível do provider Google e commitar `.terraform.lock.hcl` após resolução. Não destruir/importar automaticamente um banco que já exista; conferir antes de aplicar. Definir proteção contra exclusão do banco e `prevent_destroy` no recurso de dados.
- [ ] Aplicar IAM mínimo: Scheduler com `roles/run.invoker` somente no job; runtime com `roles/datastore.user` para banco e `roles/secretmanager.secretAccessor` somente nos segredos usados. Scheduler deve chamar a API `jobs:run` com OAuth da conta dedicada, não uma URL pública de serviço. Segredos devem ser referenciados por versão no job; valores serão adicionados por canal seguro fora do Terraform para não aparecerem no estado. Preservar agentes de serviço gerenciados necessários ao Scheduler.
- [ ] Declarar `task_count=1`, `parallelism=1`, `max_retries=0`, timeout `600s`, CPU 1 e memória `512Mi`. Usar as duas expressões UTC; limitar tentativas de disparo e manter controle transacional na aplicação, porque paralelismo 1 não impede duas execuções distintas do mesmo job.
- [ ] Executar `terraform -chdir=infra/terraform init -backend=false`, `terraform -chdir=infra/terraform fmt -check` e `terraform -chdir=infra/terraform validate` depois de instalar/verificar Terraform na fase de execução. Executar `plan` somente com um projeto real definido e autenticado, salvando o resultado para revisão. Não executar `apply` nesta tarefa. Incluir o comando de validação dos horários no roteiro.
- [ ] Documentar sequência de bootstrap: criar APIs/registro/banco/segredos com job desabilitado por variável `deploy_job=false`; publicar imagem; adicionar versões dos segredos; habilitar `deploy_job=true` mantendo Scheduler pausado; realizar validações; só então habilitar `schedules_enabled=true`. Isso evita criar job apontando para imagem ou versão de segredo inexistente. Scripts e README devem usar parâmetros fornecidos pelo usuário, sem inventar project IDs.
- [ ] Revisar/commitar e registrar resultado das validações. Não classificar imagem construída ou `terraform validate` como deploy realizado.

**Aceite:** há imagem executável, infraestrutura reproduzível e um plano de implantação revisável; nenhum recurso cloud precisa estar ativo para concluir esta tarefa.

## Task 8: validar no GCP e ativar os alertas

**Files:** atualizar `docs/operations/deployment.md`, `docs/operations/linkedin-feasibility.md`, `docs/operations/acceptance.md` e README com evidências reais. Não inserir valores secretos nem estado Terraform no repositório.

**Interfaces:** consumir imagem, infraestrutura e comandos anteriores. Esta tarefa precisa de projeto GCP, região, autenticação com permissão de implantação, faturamento quando exigido e pelo menos um canal configurado. A recomendação é começar pelo Discord; o código já suporta ambos.

- [ ] Verificar Git, SDK .NET 10, daemon Docker, Google Cloud CLI, Terraform e a identidade de implantação. Instalar ferramentas ausentes durante a execução com os mecanismos disponíveis, sem iniciar login ou alterar o projeto global silenciosamente. Usar `--project` e `--region` explícitos nos comandos GCP.
- [ ] Obter os dados faltantes somente nesta etapa: projeto GCP, região (`southamerica-east1` escolhida), canal inicial e provedor SMTP se aplicável. O usuário configura webhook e senhas por mecanismo local/Secret Manager; não pedir segredos em conversa. Para banco preexistente, verificar região/modo e planejar reutilização/importação sem destruir dados.
- [ ] Preparar o `terraform plan` concreto, resumir recursos/custo esperado e obter autorização para a implantação paga quando ela ainda não tiver sido dada. A aprovação deve vir depois de código, testes, imagem e plano de recursos prontos para revisão.
- [ ] Aplicar o bootstrap autorizado, publicar a imagem e configurar segredos, mantendo ambos os agendamentos pausados. Usar tag imutável/digest. Criar o job após a imagem existir.
- [ ] Executar manualmente o job com `run --dry-run` e aguardar seu estado final, não só o aceite da API de disparo. Conferir que a coleta funciona no IP/rede do Cloud Run. Se houver bloqueio, manter os agendamentos pausados e registrar o impedimento; não disfarçar a falha com dados de demonstração.
- [ ] Validar uma execução real no canal configurado. Conferir título, empresa, modalidade e link; registrar IDs de execução/recibo sem segredos. Se não houver vaga correspondente, isso é resultado vazio válido apenas quando a coleta foi interpretada com sucesso; testar o canal separadamente com mensagem claramente identificada como teste, sem inventar vaga real.
- [ ] Executar novamente para confirmar persistência e ausência de repetição de entregas confirmadas. Para provar leitura do histórico, usar um comando de diagnóstico somente leitura ou uma nova janela controlada de teste, sem apagar o histórico de produção. Não considerar uma segunda execução ignorada na mesma janela prova suficiente de deduplicação por vaga.
- [ ] Habilitar os dois agendamentos. Verificar as três primeiras execuções planejadas, consultar horários e resultados finais e registrar eventuais atrasos. Se o acompanhamento ocorrer depois do encerramento da sessão, usar uma automação de acompanhamento somente quando solicitada, sem afirmar antecipadamente que essa validação ocorreu.
- [ ] Documentar como pausar Scheduler, executar uma busca manual, consultar logs de falha, trocar segredo e retornar ao digest anterior da imagem. Estado Firestore permanece preservado em rollback.
- [ ] Registrar cada aceite como `PASS`, `FAIL` ou `NOT RUN`, com horário e evidência, e entregar a situação exata: pronto localmente, preparado para implantação, implantado sem coleta funcional ou ativo e verificado. Revisar/commitar documentação final sem fazer push não solicitado.

**Aceite:** bot executado na nuvem, fonte validada a partir do ambiente de produção, pelo menos um canal confirmado e histórico persistido entre execuções. Se faltarem credenciais/projeto ou a fonte bloquear acesso, a parte independente pode estar pronta, mas a operação permanece explicitamente pendente.

## Verificação de conclusão

- [ ] `dotnet restore AlertDoors.slnx --locked-mode` e `dotnet build AlertDoors.slnx -c Release --no-restore` passam.
- [ ] Unitários e integração com emuladores passam; registrar testes que não puderam ser executados.
- [ ] Imagem Linux executa e retorna os códigos de saída esperados.
- [ ] A infraestrutura passa por formatação e validação; o horário produz intervalos de 60 minutos por 48 horas simuladas.
- [ ] Coleta real foi distinguida de demonstração, bloqueio e resultado vazio válido.
- [ ] Nenhum segredo ou dado de autenticação está no diff, logs, contexto Docker ou estado Terraform versionado.
- [ ] No GCP, confirmar resultado do job, entrega real e deduplicação, ou declarar precisamente por que ainda não foi possível ativar.

## Referências verificadas em 19/09/2026

- [API e permissões LinkedIn](https://learn.microsoft.com/en-us/linkedin/shared/authentication/getting-access).
- [Agendar Cloud Run Jobs](https://docs.cloud.google.com/run/docs/execute/jobs-on-schedule).
- [Formato de cron e UTC](https://docs.cloud.google.com/scheduler/docs/configuring/cron-job-schedules).
- [Transações Firestore](https://docs.cloud.google.com/firestore/native/docs/manage-data/transactions).
- [Emulador Firestore](https://docs.cloud.google.com/firestore/native/docs/emulator).
- [Segredos em Cloud Run Jobs](https://docs.cloud.google.com/run/docs/configuring/jobs/secrets).
- [Webhooks Discord](https://docs.discord.com/developers/resources/webhook).
- [Google.Cloud.Firestore 4.4.0](https://www.nuget.org/packages/Google.Cloud.Firestore/4.4.0).
- [AngleSharp 1.8.2](https://www.nuget.org/packages/AngleSharp/1.8.2).
- [MailKit 4.18.0](https://www.nuget.org/packages/MailKit/4.18.0).

## Revisão e forma de execução

Recomendação: execução direta nesta sessão, seguindo a sequência e os testes de cada tarefa. O plano também pode ser executado com subagentes por tarefa e revisão independente, ao custo de mais coordenação e contextos. Escolher a forma de execução após revisar este documento; escrever este plano não inicia a implementação.
