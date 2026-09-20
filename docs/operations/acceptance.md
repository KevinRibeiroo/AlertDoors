# Registro de aceite

Registro atualizado em 20/09/2026. O projeto GCP está em `southamerica-east1`; identificadores da conta, credenciais, valores dos segredos e saídas detalhadas permanecem fora do repositório. Os dois agendamentos continuam pausados.

| Verificação | Resultado | Evidência |
| --- | --- | --- |
| Coleta pública local limitada | PASS | Pesquisa e detalhe HTTP 200; parser extraiu dados reais. Ver `linkedin-feasibility.md`. |
| Filtros, parser, transporte, mensagens e configuração | PASS | 60 testes unitários, incluindo identificação do cliente HTTP no envio ao Discord. |
| Persistência, concorrência, retomada e SMTP | PASS | 7 testes com Firestore Emulator e Mailpit locais. |
| Intervalos dos agendamentos | PASS | 72 horários em 48 horas simuladas, todos com intervalo de 40 minutos. |
| Imagem Linux | PASS | Build linux/amd64, usuário 1654; demo retorna 0; execução sem configuração retorna 2. |
| Terraform | PASS | Bootstrap aplicado; Firestore e Artifact Registry preexistentes importados; job real configurado e permissão de leitura concedida apenas ao segredo Discord. |
| Projeto GCP acessível | PASS | Consulta autenticada confirmou estado ativo; detalhes guardados localmente. |
| Firestore | PASS | Banco regional `(default)` existente importado, com proteção contra exclusão; execução real concluiu aquisição e finalização da janela. |
| Coleta no Cloud Run | PARCIAL | Dry-run retornou 88 vagas, sem correspondências. Execução real terminou com código 0 e zero vagas aceitas. A coleta foi truncada pelos limites de páginas/detalhes; isso não prova ausência de vagas em todo o LinkedIn. |
| Discord | PARCIAL | Versão 1 do webhook no Secret Manager. Uma mensagem explícita de teste foi confirmada pelo Discord após usar `User-Agent` identificado. Ainda não houve envio de vaga real pelo bot. |
| E-mail real | NOT RUN | Canal desativado. |
| Histórico entre execuções na nuvem | NOT RUN | Aguarda vaga elegível e entrega confirmada pelo bot. |
| Três primeiras execuções agendadas | NOT RUN | Agendamentos ainda não ativados. |

IDs de execução e recibos ficam em evidência privada quando identificarem o ambiente. Uma execução concluída sem vaga elegível não valida o fluxo de envio e deduplicação de vagas reais.
