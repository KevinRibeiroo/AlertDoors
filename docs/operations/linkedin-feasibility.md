# Validação da fonte LinkedIn

## Resultado inicial — 19/09/2026

- Pesquisa pública de `.NET` no Brasil, com janela solicitada de 24 horas: HTTP 200.
- O parser reconheceu 60 vagas, extraindo ID, URL canônica, cargo, empresa, local e data disponibilizada pela página.
- Uma página de detalhe foi consultada separadamente com HTTP 200 para conferir a identidade extraída.
- HTML real e resultados brutos ficaram somente na pasta local ignorada `artifacts/`; os testes versionados usam conteúdo fictício.
- Os seis testes iniciais de interpretação passaram, incluindo HTML desconhecido, login, estado vazio explícito e link fora do domínio permitido.

O teste comprova acesso e extração nesse momento e ambiente; não comprova cobertura completa, disponibilidade contínua, correspondência aos filtros de senioridade/modalidade ou acesso a partir do GCP.

## Pendências

- Validar coleta limitada e enriquecimento dos campos necessários aos filtros.
- Validar o estado vazio contra uma resposta real; a fixture inicial de vazio é sintética.
- Validar a coleta no Cloud Run antes de ativar os agendamentos.

## Diagnóstico disponível

```powershell
dotnet run --project src/AlertDoors -- probe --html artifacts/linkedin/search.html
```

O comando interpreta um arquivo de diagnóstico público com até 2 MiB, sem banco nem envio de notificações. Arquivos obtidos por login ou contendo dados pessoais não devem ser usados como fixtures públicas.

## Coletor .NET e filtros

Uma execução limitada do comando probe também retornou Success, com seis resultados e truncamento explícito. Os filtros rejeitaram resultados fora do perfil ou sem evidência suficiente. Nenhuma notificação foi enviada. A suíte local contém 44 testes aprovados para parser, transporte, paginação e filtros.
