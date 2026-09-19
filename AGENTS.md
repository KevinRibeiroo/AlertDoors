# Working conventions

- Work on the `develop` branch. Do not create feature branches, switch to `main`, or merge into `main` unless the user explicitly requests it.
- Read the implementation plan and design under `docs/superpowers/` before implementing the bot.
- This repository is public. Never commit credentials, real webhook URLs, private keys, local account paths, collected raw data, or infrastructure state.
- Use fictional data in fixtures. Keep live probe responses and local execution notes in ignored directories.
- Verify behavior with focused tests and report any integration that could not be validated. A mock/demo is not evidence of a working LinkedIn or GCP integration.
- Do not activate cloud resources or scheduled notifications without the necessary configuration and authorization.
