# Implantação no GCP

Estado atual: infraestrutura e Cloud Run Job implantados em São Paulo, com Discord configurado e os dois agendamentos pausados. A coleta manual funcionou, mas não encontrou vaga elegível na amostra limitada; houve apenas uma mensagem explícita de teste do webhook. Consulte [aceite](acceptance.md) para as verificações realizadas e pendentes.

## Preparação

Trabalhe em `develop`. Use SDK .NET 10, Docker com containers Linux, Google Cloud CLI e Terraform >= 1.9 e < 2. Não é necessário manter o computador ligado após a implantação. O desenvolvimento também pode usar as CLIs oficiais em containers.

Defina `$ProjectId` e `$Region` localmente. A região proposta é `southamerica-east1` (São Paulo). Confirme que o projeto está vinculado à sua conta de faturamento do Free Tier/Free Trial e confirme a localização de um eventual banco existente. Vincular faturamento não remove a cota gratuita: dentro dos limites publicados, não há cobrança; somente excedentes, produtos sem gratuidade ou créditos esgotados geram custo. Autentique com `gcloud auth login` e `gcloud auth application-default login` quando necessário; não crie chaves de conta de serviço para o bot.

```powershell
gcloud projects describe $ProjectId
gcloud billing projects describe $ProjectId
gcloud firestore databases list --project=$ProjectId
gcloud artifacts repositories list --project=$ProjectId --location=$Region
dotnet restore AlertDoors.slnx --locked-mode
docker compose up -d
$env:FIRESTORE_EMULATOR_HOST = '127.0.0.1:8080'
dotnet test AlertDoors.slnx --no-restore
./scripts/Verify-Schedule.ps1
./scripts/Build-Image.ps1
docker run --rm alertdoors:local demo
```

Se uma API estiver desativada, a falha de listagem não prova ausência de recursos e não significa que o projeto esteja sendo cobrado. Após habilitação autorizada, repita o inventário antes de criar o banco. Não substitua um banco existente: confirme modo, edição e região, ajuste a configuração e importe o recurso para o estado Terraform quando apropriado.

Copie `infra/terraform/terraform.tfvars.example` para `infra/terraform/deployment.local.tfvars`, ignorado pelo Git. Preencha projeto e região. Deixe `deploy_job=false`, ambos os canais desligados e `schedules_enabled=false`.

```powershell
terraform -chdir=infra/terraform init -backend=false
terraform -chdir=infra/terraform fmt -check
terraform -chdir=infra/terraform validate
terraform -chdir=infra/terraform plan '-var-file=deployment.local.tfvars' '-out=bootstrap.tfplan'
```

Revise o plano antes de ativar recursos. O bootstrap declara seis APIs, um registro de imagens, um banco Firestore Native Standard, um índice de entregas, duas contas de serviço, acesso do runtime ao Firestore e sete recipientes de segredos sem valores. Não cria o job nem os agendamentos enquanto `deploy_job=false`. O banco e os segredos têm proteção contra destruição. A cota gratuita depende dos limites atuais do Google Cloud; configure um alerta de orçamento se sua conta permitir.

O estado Terraform é local e ignorado. Guarde uma cópia privada protegida; não o envie ao GitHub. Antes de operações colaborativas, configure um backend privado com controle de acesso e bloqueio de estado. Nunca aplique o mesmo projeto a partir de estados independentes.

## Bootstrap e imagem

Somente após autorização, aplique o plano revisado. Se houver infraestrutura preexistente fora do estado, importe os recursos correspondentes e gere novo plano antes de aplicar.

```powershell
terraform -chdir=infra/terraform apply bootstrap.tfplan
gcloud auth configure-docker "$Region-docker.pkg.dev"
$ImageTag = "$Region-docker.pkg.dev/$ProjectId/alertdoors/bot:$(git rev-parse --short HEAD)"
./scripts/Build-Image.ps1 -ImageTag $ImageTag
docker push $ImageTag
gcloud artifacts docker images describe $ImageTag --project=$ProjectId
```

Use o URI com `@sha256:...` retornado pelo registro como `image_uri`. Não use uma tag mutável para produção.

O primeiro job usa `job_args=["run", "--dry-run"]`, `deploy_job=true`, canais desligados e agendamentos pausados. Gere um novo plano e aplique somente as mudanças revisadas. Essa execução coleta e filtra sem gravar no Firestore e sem enviar avisos.

```powershell
gcloud run jobs execute alertdoors --project=$ProjectId --region=$Region --wait
gcloud run jobs executions list --job=alertdoors --project=$ProjectId --region=$Region
```

Confirme o resultado final, o código de saída e os logs. O aceite do disparo não prova sucesso do bot. Se o LinkedIn bloquear a rede do Cloud Run, mantenha o Scheduler pausado. Não use cookies, proxies ou CAPTCHA bypass para contornar o bloqueio.

## Segredos e primeiro envio

Escolha Discord, e-mail ou ambos. No Console GCP, acesse Secret Manager e adicione versões aos segredos necessários. Use os valores reais somente ali; não os cole em conversa, comandos com histórico ou arquivos versionados. O Terraform gerencia recipientes e permissões, nunca valores.

| Variável | Segredo |
| --- | --- |
| `DISCORD_WEBHOOK_URL` | `alertdoors-discord-webhook` |
| `SMTP_HOST` | `alertdoors-smtp-host` |
| `SMTP_PORT` | `alertdoors-smtp-port` |
| `SMTP_USER` | `alertdoors-smtp-user` |
| `SMTP_PASSWORD` | `alertdoors-smtp-password` |
| `EMAIL_FROM` | `alertdoors-email-from` |
| `EMAIL_TO` | `alertdoors-email-to` |

Na configuração local, habilite o canal escolhido e informe números de versão em `secret_versions` usando os nomes das variáveis. E-mail usa TLS na porta 465 e STARTTLS obrigatório nas demais; o provedor precisa permitir autenticação SMTP. O runtime recebe acesso somente aos segredos do canal habilitado. Remetente e destinatário ficam fora do estado e do repositório.

Troque `job_args` para `["run"]`, mantendo `schedules_enabled=false`, revise/aplique o plano e execute manualmente. Confirme conteúdo e entrega no destino. A primeira execução pode trazer até 20 vagas por canal; as demais ficam pendentes. Ausência de vagas elegíveis não comprova o canal: nesse caso valide o destino com uma mensagem explicitamente de teste antes de ativar.

Execute em outra janela de 40 minutos e verifique que vagas já confirmadas não reaparecem. Uma repetição ignorada na mesma janela só valida o controle de execução. O Firestore guarda entregas por canal, e uma falha em e-mail não desfaz um envio confirmado no Discord. Uma queda entre envio externo e confirmação no banco pode gerar duplicata.

## Ativação e operação

Somente após coleta, entrega e persistência confirmadas, altere `schedules_enabled=true`, revise e aplique. São dois agendamentos UTC: `0,40 0-23/2 * * *` e `20 1-23/2 * * *`. Produzem 36 disparos por dia, separados por 40 minutos; atrasos do serviço continuam possíveis. A expressão `*/40 * * * *` não atende esse intervalo.

Verifique as três primeiras execuções planejadas e registre seus resultados. O Cloud Run usa uma tarefa, 1 vCPU, 512 MiB, timeout de 600 segundos e zero retentativas automáticas da tarefa. O aplicativo encerra cada ciclo, com orçamento interno de nove minutos e concessão Firestore de quinze minutos.

Pausar disparos:

```powershell
gcloud scheduler jobs pause alertdoors-even --project=$ProjectId --location=$Region
gcloud scheduler jobs pause alertdoors-odd --project=$ProjectId --location=$Region
```

Depois registre `schedules_enabled=false` na configuração local para que o próximo apply não reverta a pausa. Pausar o Scheduler não cancela uma execução já iniciada.

Diagnóstico:

```powershell
gcloud run jobs executions list --job=alertdoors --project=$ProjectId --region=$Region
gcloud logging read 'resource.type="cloud_run_job" AND resource.labels.job_name="alertdoors"' --project=$ProjectId --limit=50
```

Logs da execução real usam contagens e categorias sanitizadas. Diagnósticos `probe` e `dry-run` podem listar dados públicos de vagas; mantenha suas saídas fora do Git. Retornos: 0 sucesso/duplicata ignorada, 2 configuração, 3 fonte bloqueada/limitada, 4 outra falha de coleta, 5 estado/envio, 130 cancelamento.

Para rotacionar credenciais, adicione uma versão no Secret Manager, atualize o número em `secret_versions`, revise/aplique e faça uma execução manual antes de desativar a versão antiga. Para rollback, pause os agendamentos e restaure `image_uri` para o digest anterior por Terraform. Preserve o Firestore e o estado Terraform.

## Custos e limites

Em 30 dias são 1.080 execuções. O consumo depende da duração de cada coleta, das franquias disponíveis na conta e de armazenamento, Firestore, segredos, logs e tráfego. Não há garantia de custo zero. Confira [preços Cloud Run](https://cloud.google.com/run/pricing), [Scheduler](https://cloud.google.com/scheduler/pricing), [Firestore](https://cloud.google.com/firestore/pricing) e [Secret Manager](https://cloud.google.com/secret-manager/pricing) antes de autorizar; configure alertas de orçamento. A coleta pública é experimental, limitada por páginas/detalhes e não promete cobertura de todas as vagas.
