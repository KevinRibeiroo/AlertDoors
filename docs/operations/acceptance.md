# Registro de aceite

Validação local em 19/09/2026. Nenhuma implantação ou entrega real está sendo afirmada por este registro. Identificadores do projeto e arquivos de credenciais permanecem fora do repositório.

| Verificação | Resultado | Evidência |
| --- | --- | --- |
| Coleta pública local limitada | PASS | Pesquisa e detalhe HTTP 200; parser extraiu dados reais. Ver `linkedin-feasibility.md`. |
| Filtros, parser, transporte, mensagens e configuração | PASS | 56 testes unitários, incluindo horários. |
| Persistência, concorrência, retomada e SMTP | PASS | 7 testes com Firestore Emulator e Mailpit locais. |
| Intervalos dos agendamentos | PASS | 72 horários em 48 horas simuladas, todos com intervalo de 40 minutos. |
| Imagem Linux | PASS | Build linux/amd64, usuário 1654; demo retorna 0; execução sem configuração retorna 2. |
| Terraform | PASS | Init e validate com provider Google 7.46.1; nenhum apply executado. |
| Plano de bootstrap | PASS | Plano autenticado salvo apenas localmente: 19 adições, zero alterações e zero exclusões; job e Scheduler desabilitados. Inventário do banco ainda pendente. |
| Projeto GCP acessível | PASS | Consulta autenticada confirmou estado ativo; detalhes guardados localmente. |
| Inventário Firestore e faturamento | NOT RUN | APIs de Firestore e Cloud Billing desativadas impediram as consultas; inventário deve ser repetido antes de criar banco. |
| Coleta no Cloud Run | NOT RUN | Aguarda implantação autorizada. |
| Discord/e-mail real | NOT RUN | Aguarda destino e segredos configurados. |
| Histórico entre execuções na nuvem | NOT RUN | Aguarda job e canal reais. |
| Três primeiras execuções agendadas | NOT RUN | Agendamentos ainda não ativados. |

Os testes locais não provam acesso ao LinkedIn a partir da rede GCP. Atualize esta tabela após cada etapa real, com horário e resultado final; IDs de execução e recibos devem ser guardados em evidência privada quando identificarem o ambiente.
