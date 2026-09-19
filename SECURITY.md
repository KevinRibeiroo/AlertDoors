# Credenciais e dados

Este repositório é público. Versionar somente código, documentação e exemplos sem valores reais.

- Não adicionar tokens, URLs reais de webhook, senhas SMTP, chaves de contas de serviço, certificados privados ou arquivos de autenticação.
- Usar variáveis de ambiente no desenvolvimento e Secret Manager com identidade de serviço no GCP.
- Manter arquivos `.env`, configurações locais, estado e planos Terraform, logs e dados coletados fora do Git. O `.gitignore` cobre esses formatos comuns; revisar também o conteúdo dos arquivos novos e exemplos.
- Não colocar valores secretos no Terraform: referenciar os segredos por identificador/versão. Mesmo um arquivo ignorado pode vazar se for incluído com `git add -f` ou copiado para outro arquivo.
- Antes de publicar, revisar os arquivos preparados para commit e executar uma ferramenta de detecção de segredos quando disponível.
- Se uma credencial for exposta, revogá-la ou substituí-la imediatamente. Apagar o arquivo em um commit posterior não remove a credencial do histórico.
- Não incluir credenciais, dados pessoais ou logs com segredos em issues ou pull requests.

Os exemplos de configuração devem conter somente valores fictícios e claramente identificados. Dados reais de implantação não pertencem à documentação pública.
