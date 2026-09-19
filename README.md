# AlertDoors

Bot planejado para buscar vagas de desenvolvimento .NET e enviar novidades por Discord e/ou e-mail.

**Status: planejamento.** Este repositório contém o desenho e o plano de implementação. O bot ainda não foi implementado, publicado no GCP ou ativado.

## Primeira versão

- LinkedIn como fonte inicial, com coleta experimental sujeita a validação real.
- Vagas júnior e pleno; remoto no Brasil e presencial/híbrido na cidade de São Paulo–SP.
- Consultas programadas a cada 40 minutos.
- Histórico persistente para evitar repetir notificações já confirmadas.
- Notificações por Discord, e-mail ou ambos, conforme configuração.

## Arquitetura proposta

Cloud Scheduler dispara um Cloud Run Job em C#/.NET 10. Cada execução busca vagas, aplica os filtros, registra o histórico no Firestore, envia os avisos e encerra. Credenciais ficam no Secret Manager.

O acesso às páginas do LinkedIn será validado antes da implementação completa e novamente no ambiente de nuvem. O projeto não depende de uma API oficial de busca geral de vagas e não prevê contornar login, CAPTCHA ou bloqueios.

## Documentação

- [Desenho da primeira versão](docs/superpowers/specs/2026-09-19-alertdoors-design.md)
- [Plano de implementação](docs/superpowers/plans/2026-09-19-alertdoors-implementation.md)
- [Cuidados com credenciais e dados](SECURITY.md)

Ainda não há comandos de instalação ou execução do bot. Eles serão adicionados conforme a implementação avançar.
