# AlertDoors — desenho da primeira versão

Status: proposta para revisão; o bot ainda não foi implementado nem ativado.

## Objetivo e filtros

Buscar vagas de desenvolvimento .NET no LinkedIn a cada 40 minutos e notificar novidades por Discord, e-mail ou ambos.

Confirmado pelo usuário:
- Senioridade: júnior e pleno.
- Modalidades: remoto; presencial e híbrido em São Paulo–SP.
- Fonte inicial: LinkedIn.
- Intervalo: 40 minutos.
- A operação não deve depender do computador do usuário; Cloud Run é o destino provável indicado por ele.

Premissas configuráveis desta proposta:
- Remoto: vagas destinadas ao Brasil. São Paulo–SP significa a cidade, não todo o estado.
- Destino proposto: container Linux executado como Cloud Run Job, acionado pelo Cloud Scheduler. Cada execução faz um ciclo e termina; execução local serve para desenvolvimento e diagnóstico.
- C# com .NET 10, disponível no ambiente. Sem painel web nesta versão.
- Os dois canais serão implementados; só ficam ativos quando configurados. Em produção, credenciais ficam no Secret Manager e são disponibilizadas ao job. No desenvolvimento, usar variáveis de ambiente. Nunca registrar segredos no repositório.

## Hospedagem e agendamento propostos

Recomendação após comparar as opções do GCP: **Cloud Run Jobs + Cloud Scheduler**, adequado ao ciclo finito de buscar, notificar e encerrar, com pouca administração de infraestrutura.

| Opção | Adequação para este bot | Quando considerar |
| --- | --- | --- |
| Cloud Run Jobs + Scheduler | Opção recomendada: container com um ciclo por execução e histórico externo. | Primeira versão e operação recorrente. |
| Cloud Run Service ou função HTTP + Scheduler | Alternativa viável; o ciclo precisa caber no tratamento da requisição e ser protegido contra repetição. | Se surgir uma API ou painel e houver vantagem em compartilhar a aplicação. |
| Compute Engine com serviço e timer | Oferece controle do sistema operacional, mas exige manutenção da VM, atualizações e recuperação do processo. | Se aparecer necessidade concreta de processo permanente ou dependência incompatível com execução por job. |

Dimensionamento inicial proposto: 1 vCPU e 512 MiB, ajustáveis após medição. Região escolhida: `southamerica-east1` (São Paulo), mantendo o Firestore regional próximo ao processamento e os dados na América do Sul. A região de hospedagem não altera os filtros das vagas.

Referência de custo, sem promessa de gratuidade: em 30 dias são 1.080 execuções. Se cada execução inteira consumir até um minuto, incluindo inicialização, a cobrança mínima de um minuto por execução equivale a 64.800 vCPU-segundos e 32.400 GiB-segundos com o dimensionamento acima. Os limites e preços publicados para `southamerica-east1`, a conta de faturamento e os demais serviços devem ser conferidos antes da ativação. Execuções mais longas ou outros usos da conta alteram esse resultado. Scheduler oferece três agendamentos gratuitos por conta de faturamento; esta proposta usa dois. Fora da franquia, o preço publicado pode gerar cobrança. Firestore, segredos, armazenamento das imagens, builds, logs, tráfego e o provedor de e-mail têm consumo próprio e devem entrar na verificação de custo antes da ativação.

- **Cloud Run Jobs:** container .NET 10, uma tarefa por execução, paralelismo 1, timeout inicial de 10 minutos e sem retentativa automática da tarefa. Tentativas curtas de operações transitórias ficam no aplicativo; pendências persistidas podem ser retomadas no próximo ciclo.
- **Cloud Scheduler:** dois agendamentos em UTC para o mesmo job: `0,40 0-23/2 * * *` e `20 1-23/2 * * *`. Juntos produzem 00:00, 00:40, 01:20, 02:00, 02:40 etc., com 36 disparos planejados por dia. Não usar `*/40 * * * *`, que alterna intervalos de 40 e 20 minutos. O início efetivo pode sofrer atraso do provedor; não se trata de uma garantia de tempo real.
- **Firestore:** histórico de vagas, entregas por canal e controle de execução compartilhado. Não depender do disco do container para persistência. A primeira versão usará Firestore também no desenvolvimento, com emulador para testes; não manter dois bancos diferentes.
- **Controle de concorrência:** aquisição transacional de uma concessão temporária com identificador do proprietário e expiração de 15 minutos, maior que o timeout do job. Registrar também as janelas de 40 minutos já concluídas. Disparos duplicados não devem executar uma janela concluída; execuções concorrentes devem sair sem enviar. Uma concessão abandonada expira e permite recuperação. Operações de envio ficam fora das transações, pois transações podem ser repetidas.
- **Secret Manager:** webhook do Discord e credenciais SMTP. E-mail usará TLS na porta compatível com o provedor, normalmente 465 ou 587, com validação no ambiente de destino.
- **IAM:** identidade dedicada para o Scheduler invocar o job e outra para o aplicativo acessar Firestore e somente os segredos necessários, sem arquivos de chave de conta de serviço dentro da imagem.
- **Observabilidade:** logs estruturados no Cloud Logging, contagem de vagas e entregas por execução e código de saída de falha quando a coleta ou entrega falhar. Sucesso do disparo pelo Scheduler não significa sucesso da execução do bot.
- **Entrega:** Dockerfile, configuração reproduzível de implantação e instruções para publicar e agendar. Projeto GCP, região, destinos de notificação e credenciais serão informados na etapa de configuração. Nenhum recurso pago será criado apenas ao revisar este desenho; o custo depende do uso e dos serviços escolhidos.

Essa estrutura mantém o processamento em uma aplicação de console portável, com interfaces para fonte, persistência e notificações. Cloud Run é o destino inicial proposto, sem tornar a máquina pessoal parte da operação.

## Opções para obter as vagas

1. **Coletor experimental das páginas públicas do LinkedIn (proposta inicial).** Permite tentar consultas no intervalo solicitado sem credenciais da conta. Depende da disponibilidade das páginas e da estrutura do HTML; sua viabilidade precisa ser comprovada antes de apresentar a integração como funcional. Não há garantia de cobertura completa ou de disponibilidade contínua.
2. **Provedor externo de dados de vagas.** Pode oferecer um contrato de API mais estável para o bot, mas exige escolher fornecedor, verificar cobertura e custos e configurar uma chave. Não será contratado nesta etapa.
3. **Processar os alertas recebidos do LinkedIn.** Aproveita os e-mails de alerta, mas não satisfaz a descoberta a cada 40 minutos: a frequência nativa documentada é diária ou semanal. Não é a opção proposta.

A documentação oficial não lista busca geral de vagas entre as permissões abertas. Não será inventada uma integração oficial de busca. A aplicação terá uma interface de fonte para permitir substituição do coletor sem reescrever as notificações.

Referências consultadas em 19/09/2026:
- https://learn.microsoft.com/en-us/linkedin/shared/authentication/getting-access
- https://www.linkedin.com/help/linkedin/answer/a511279/job-alerts-on-linkedin?lang=en
- https://docs.cloud.google.com/run/docs/execute/jobs-on-schedule
- https://docs.cloud.google.com/scheduler/docs/configuring/cron-job-schedules
- https://docs.cloud.google.com/run/docs/container-contract
- https://docs.cloud.google.com/run/docs/configuring/jobs/secrets
- https://docs.cloud.google.com/firestore/native/docs/manage-data/transactions
- https://docs.cloud.google.com/run/docs/overview/what-is-cloud-run
- https://cloud.google.com/run/pricing
- https://cloud.google.com/scheduler/pricing

## Fluxo

1. Ao receber um disparo, validar configuração, identificar a janela de execução e adquirir a concessão no Firestore antes de consultar a fonte.
2. Consultar separadamente vagas remotas no Brasil e vagas presenciais/híbridas na cidade de São Paulo. Buscar variações de .NET, C# e ASP.NET.
3. Normalizar os resultados: ID da fonte, URL canônica, título, empresa, local, modalidade, senioridade e data de publicação, quando disponíveis.
4. Aplicar os filtros. Exigir tecnologia .NET/C#/ASP.NET e cargo de desenvolvedor ou engenheiro de software no título ou na descrição. Usar senioridade explícita no título ou nos dados disponíveis; não equiparar automaticamente a categoria ampla “mid-senior” a pleno. Aceitar títulos mistos como “Pleno/Sênior” quando incluírem júnior/pleno; excluir sênior isolado, liderança e estágio explícitos. Se faltar evidência suficiente de senioridade, localização ou modalidade, contabilizar o resultado como indeterminado e não enviar como correspondência confirmada.
5. Registrar vagas e entregas pendentes no Firestore antes do envio.
6. Enviar um resumo por canal habilitado com título, empresa, localização/modalidade e link. Incluir salário e data apenas quando fornecidos pela fonte.
7. Registrar sucesso individual por canal. Falha no e-mail não deve repetir um envio já confirmado no Discord.
8. Registrar o resultado da execução, liberar a concessão pertencente à execução e encerrar o processo. O próximo disparo vem do Cloud Scheduler. Não manter o container aguardando 40 minutos.

Na primeira execução, usar resultados das últimas 24 horas, quando a fonte oferecer esse filtro, e limitar o resumo a 20 vagas. Não marcar vagas omitidas pelo limite como entregues. Nos próximos ciclos, usar uma janela sobreposta de consulta e deduplicar por ID; o agendamento não garante que a fonte já tenha disponibilizado toda vaga publicada no período.

## Componentes

- **Coordenador:** executa um ciclo, controla janelas e concessões de execução e respeita cancelamento e timeout. O agendamento pertence à infraestrutura.
- **Fonte LinkedIn:** consultas, paginação limitada, interpretação dos dados e diagnóstico de falhas.
- **Filtro:** regras testáveis de tecnologia, cargo, senioridade, local e modalidade.
- **Repositório Firestore:** vagas observadas, estado de entrega por vaga/canal e concessão de execução, persistentes entre containers.
- **Notificador Discord:** webhook vindo da configuração de segredos, limites de tamanho e menções desativadas para conteúdo coletado.
- **Notificador de e-mail:** SMTP com TLS e conteúdo escapado; destinatário configurável e credenciais vindas da configuração de segredos.
- **Configuração e logs:** filtros e intervalo ajustáveis, contagem de resultados e entregas, sem registrar segredos.

## Falhas e limites

- Timeouts, cancelamento e tentativas limitadas para falhas transitórias.
- Respeitar respostas de limitação e o tempo de espera informado pela fonte. Não contornar CAPTCHA, login ou bloqueios com proxies ou cookies de contas.
- Detectar página de bloqueio ou HTML incompatível e reportar falha; não representar isso como “nenhuma vaga encontrada”.
- Retentar entregas pendentes sem refazer as entregas confirmadas nos demais canais.
- Uma queda após o destino aceitar a mensagem, mas antes da confirmação no banco, pode causar duplicata em nova tentativa. Não prometer entrega exatamente uma vez.
- Um coletor que não consiga obter resultados reais não será declarado pronto. A falha será apresentada junto da alternativa de fonte necessária.
- A disponibilidade da coleta precisa ser verificada também no Cloud Run: uma consulta que funcione na máquina de desenvolvimento pode falhar a partir da rede do provedor de nuvem. Não apresentar o deploy como funcional sem essa verificação.

## Validação e entrega

- Testes de filtros: cargo, júnior/pleno, título misto com sênior, exclusão de sênior isolado, cidade de São Paulo, remoto Brasil e campos ausentes.
- Testes de interpretação do HTML com exemplos locais, inclusive páginas de bloqueio.
- Testes de persistência e entregas independentes por canal, incluindo falha e reinício.
- Verificar os agendamentos por pelo menos 48 horas simuladas, inclusive a virada de dia, confirmando intervalos de 40 minutos.
- Testes de concorrência, concessão abandonada, disparo duplicado, cancelamento e limites de tentativas; usar o emulador Firestore para validar transações e persistência.
- Modo de consulta única sem envio para validar resultados reais. Modo de demonstração com dados fictícios explicitamente identificados, separado do coletor real.
- Compilar, executar os testes e verificar a integração real antes de afirmar que a coleta funciona.
- Entregar instruções em português para configuração, execução e diagnóstico. Ativar notificações reais somente com os destinos configurados pelo usuário.
- Construir a imagem Linux e validar execução única. Na implantação, validar a coleta na rede do Cloud Run, o acesso por identidade de serviço, a persistência entre execuções e a entrega nos destinos configurados.

## Critério de conclusão

O código deve consultar uma fonte real validada, aplicar os filtros acordados, persistir resultados fora do container e enviar novidades nos canais configurados. A entrega inclui os arquivos para execução no Cloud Run Jobs com disparos planejados a cada 40 minutos pelo Cloud Scheduler. Código compilado, imagem construída ou demonstração com dados fictícios, isoladamente, não significa bot em operação. A ativação na nuvem depende de projeto GCP, permissões, faturamento quando exigido e destinos configurados. Até esses dados estarem disponíveis, a entrega deve ser descrita como preparada para implantação, sem afirmar que já está hospedada ou enviando alertas.
