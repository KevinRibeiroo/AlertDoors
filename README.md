# AlertDoors

Bot para buscar vagas de desenvolvimento .NET e enviar novidades por Discord e/ou e-mail.

**Status: implementação em `develop`.** Coleta, filtros, persistência e notificadores possuem testes locais. A coleta no Cloud Run e os destinos reais ainda precisam de validação antes de ativar o bot.

## Primeira versão

- LinkedIn como fonte inicial, com coleta experimental sujeita a validação real.
- Vagas júnior e pleno; remoto no Brasil e presencial/híbrido na cidade de São Paulo–SP.
- Consultas programadas a cada 40 minutos.
- Histórico persistente para evitar repetir notificações já confirmadas.
- Notificações por Discord, e-mail ou ambos, conforme configuração.

## Arquitetura

Cloud Scheduler dispara um Cloud Run Job em C#/.NET 10. Cada execução busca vagas, aplica os filtros, registra o histórico no Firestore, envia os avisos e encerra. Credenciais ficam no Secret Manager.

O acesso às páginas do LinkedIn será validado antes da implementação completa e novamente no ambiente de nuvem. O projeto não depende de uma API oficial de busca geral de vagas e não prevê contornar login, CAPTCHA ou bloqueios.

## Documentação

- [Desenho da primeira versão](docs/superpowers/specs/2026-09-19-alertdoors-design.md)
- [Plano de implementação](docs/superpowers/plans/2026-09-19-alertdoors-implementation.md)
- [Cuidados com credenciais e dados](SECURITY.md)
- [Implantação e operação no GCP](docs/operations/deployment.md)
- [Registro de aceite](docs/operations/acceptance.md)

## Executar e testar

Requisitos: SDK .NET 10 (ver `global.json`); Docker para os testes de integração. Trabalhar sempre na branch `develop`.

```powershell
dotnet restore AlertDoors.slnx --locked-mode
dotnet run --project src/AlertDoors -- demo
dotnet run --project src/AlertDoors -- probe
dotnet run --project src/AlertDoors -- run --dry-run
```

`demo` usa dados fictícios. `probe` consulta uma pesquisa e até dois detalhes; `run --dry-run` executa as buscas e filtros reais. Esses modos não usam banco nem enviam mensagens. A coleta é limitada e pode retornar resultados parciais, sinalizados por `Truncated`.

Para todos os testes, iniciar os emuladores e aguardar a porta 8080 ficar disponível:

```powershell
docker compose up -d
$env:FIRESTORE_EMULATOR_HOST = '127.0.0.1:8080'
dotnet test AlertDoors.slnx --no-restore
docker compose stop
```

O SMTP de teste atende em `127.0.0.1:1025`, com caixa de mensagens local em `http://127.0.0.1:8025`. Os testes usam destinatários fictícios e não encaminham e-mails para fora.

## Configuração de execução real

As variáveis estão listadas em [.env.example](.env.example). O arquivo é um exemplo: `.env` não é carregado automaticamente.

- `run` exige projeto Firestore e pelo menos um canal habilitado/configurado.
- Discord: `DISCORD_ENABLED=true` e webhook via `DISCORD_WEBHOOK_URL`.
- E-mail: `EMAIL_ENABLED=true`, host/porta, remetente/destinatário e credenciais SMTP quando exigidas pelo provedor. Porta 465 usa TLS imediato; outras portas usam STARTTLS obrigatório.
- Produção usa `ALERTDOORS_MODE=Production` e a identidade do serviço GCP. Não aceita `FIRESTORE_EMULATOR_HOST`.
- Desenvolvimento usa `ALERTDOORS_MODE=Development` e exige `FIRESTORE_EMULATOR_HOST`. SMTP sem TLS só é permitido explicitamente para localhost nesse modo.
- O filtro exige `.NET`, `ASP.NET`, `dotnet` ou `C#` e um cargo de desenvolvedor/developer ou engenheiro de software/software engineer no título ou na descrição. Aceita júnior/pleno, inclusive títulos mistos como “Pleno/Sênior”; sênior isolado, liderança e estágio não são elegíveis. Remoto exige Brasil; híbrido/presencial exige a cidade de São Paulo–SP. Campos ausentes ou conflitantes são indeterminados; nenhum campo é inferido só pelo filtro da pesquisa.

```powershell
dotnet run --project src/AlertDoors -- run
```

Cada processo faz um ciclo e termina. O intervalo de 40 minutos pertence ao Cloud Scheduler. Retornos: `0` sucesso/execução duplicada ignorada; `2` configuração; `3` bloqueio/limitação da fonte; `4` outra falha de coleta; `5` persistência/notificação; `130` cancelamento. Entregas confirmadas ficam no Firestore; falhas continuam pendentes. Uma queda entre envio e confirmação no banco ainda pode causar uma duplicata.
