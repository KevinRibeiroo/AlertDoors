# Registro de aceite

Registro atualizado em 20/09/2026. O projeto GCP está em `southamerica-east1`; identificadores da conta, credenciais, valores dos segredos e saídas detalhadas permanecem fora do repositório. O agendamento horário está ativo.

| Verificação | Resultado | Evidência |
| --- | --- | --- |
| Coleta pública local limitada | PASS | Pesquisa e detalhe HTTP 200; parser extraiu dados reais. Ver `linkedin-feasibility.md`. |
| Filtros, parser, transporte, mensagens e configuração | PASS | 82 testes unitários, incluindo cargos de desenvolvimento .NET, campos a confirmar e identificação do cliente HTTP no envio ao Discord. |
| Persistência, concorrência, retomada e SMTP | PASS | 7 testes com Firestore Emulator e Mailpit locais. |
| Intervalo do agendamento | PASS | 48 horários em 48 horas simuladas, todos com intervalo de 60 minutos. |
| Imagem Linux | PASS | Build linux/amd64 da versão horária; demo retorna 0; imagem publicada por digest imutável. |
| Terraform | PASS | Bootstrap aplicado; Firestore e Artifact Registry preexistentes importados; job real configurado e permissão de leitura concedida apenas ao segredo Discord. |
| Projeto GCP acessível | PASS | Consulta autenticada confirmou estado ativo; detalhes guardados localmente. |
| Firestore | PASS | Banco regional `(default)` existente importado, com proteção contra exclusão; execução real concluiu aquisição e finalização da janela. |
| Coleta no Cloud Run | PARCIAL | Execução manual da imagem atual terminou com código 0, quatro vagas aceitas e coleta truncada em algumas pesquisas. Não comprova cobertura completa do LinkedIn. |
| Discord | PASS | Logs da execução manual mostram entrega confirmada de quatro vagas pelo canal Discord. O valor do webhook permanece somente no Secret Manager. |
| E-mail real | NOT RUN | Canal desativado. |
| Histórico entre execuções na nuvem | PARCIAL | O disparo manual pelo Scheduler na mesma janela terminou com `duplicate_ignored`; repetição entre janelas horárias ainda precisa ser observada. |
| Cloud Scheduler horário | PASS | `alertdoors-hourly` está `ENABLED`, com `0 * * * *` em UTC. O disparo manual pelo Scheduler criou uma execução Cloud Run concluída com sucesso. |
| Primeiras execuções automáticas | NOT RUN | O agendamento foi ativado após a execução manual; os disparos planejados seguintes ainda não ocorreram no momento deste registro. |

IDs de execução e recibos ficam em evidência privada quando identificarem o ambiente. O disparo manual pelo Scheduler confirma a ligação com Cloud Run; a primeira execução automática no horário planejado ainda deve ser conferida.
