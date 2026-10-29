# Distribuição e organização — 0.7.2

## O que enviar

Pasta `releases/0.7.2/`:

- **Icatya-0.7.2-Instalador.exe**: opção principal para os amigos. Instala em `%LOCALAPPDATA%\Programs\iCatya`, sem solicitar elevação administrativa, com menu Iniciar, atalho opcional e desinstalador.
- **Icatya-0.7.2-Portatil.zip**: alternativa sem instalação. Contém uma única pasta principal `iCatya/`; não espalha DLLs em Downloads ao extrair.
- **atualizacao/Icatya-0.7.2-Windows-x64.zip**: pacote técnico, com estrutura na raiz para continuar compatível com o atualizador das versões anteriores. Envie-o como asset da mesma Release, mas não o indique como download portátil para extração manual.
- Arquivos `.sha256`: conferência da integridade dos pacotes.

Nick, sala e avatar continuam em `%LOCALAPPDATA%\Icatya`. A instalação usa esse mesmo perfil, então a migração não exige refazer o cadastro da sala. Não há login. O instalador não é obrigatório para quem já usa a pasta e o atualizador.

O caminho instalado é fixo entre versões. A identidade do aplicativo na barra de tarefas passa a corresponder à identidade dos atalhos criados pelo instalador. A correção de vídeo da 0.7.1 continua incluída.

## Limites dos avisos de segurança

O instalador e o iCatya ainda não são assinados digitalmente. Não foi criado certificado autoassinado nem modificada a segurança do Windows. O SmartScreen poderá avisar sobre um aplicativo desconhecido; uma assinatura válida também precisa construir reputação, segundo a [documentação da Microsoft](https://learn.microsoft.com/windows/apps/package-and-deploy/smartscreen-reputation).

O pedido de rede é independente do aviso de download. A comunicação direta pode exigir autorização no firewall. Não há adição automática de regras ou portas, nem alteração no perfil de rede. Consulte a [explicação da Microsoft sobre permissões de firewall](https://support.microsoft.com/en-us/windows/security/firewall/risks-of-allowing-apps-through-windows-firewall).

O compilador Inno Setup foi obtido da distribuição oficial 7.1.1 x64, com Authenticode válido de Pyrsys B.V. e SHA256 verificado. Foi preparado em modo portátil dentro de `work/tools`; não é distribuído aos usuários.

## Estrutura do projeto

- `src`: código do app e do serviço de salas.
- `tests`: verificações de vídeo, áudio, atualizador e empacotamento.
- `scripts`: ferramentas de build e o roteiro do instalador em `scripts/installer/Icatya.iss`.
- `docs`: documentação e licenças.
- `app/0.7.2`: publicação atual, para desenvolvimento/teste local.
- `releases/0.7.2`: arquivos preparados para distribuição.
- `native` e `work`: dependências e temporários locais, ignorados pelo Git.
- `../Backups`: versões, ZIPs e cópias de distribuição anteriores, preservados sem apagar dados.

## Próximas versões

Depois de publicar o app em `app/<versão>`, use `Gerar instalador.cmd` ou `scripts/Empacotar-Versao.ps1`. A versão vem do projeto C#. O script gera instalador, ZIP portátil com pasta principal e ZIP técnico, todos com hashes. Para recriar uma versão existente, `-Rebuild` preserva os pacotes anteriores em Backups antes de substituí-los.

As regras permanentes também estão em `AGENTS.md`, para manter esse formato nas próximas alterações.

O atualizador aceita tanto `v0.7.1` quanto `V0.7.1` como prefixo de versão, conforme a Release pública existente. Novas Releases devem usar três números, por exemplo `v0.7.2`, e incluir o ZIP técnico com o nome indicado acima.

## Verificação

O modo de teste do instalador extrai dentro de `work`, sem mostrar janela, abrir/fechar o app, criar atalhos ou registrar desinstalador. Os hashes de todos os arquivos extraídos são comparados aos da publicação. O ZIP portátil é testado para garantir que haja apenas a pasta principal `iCatya` no destino. O ZIP técnico continua passando pelo verificador do atualizador.

A instalação interativa e a remoção pela interface do Windows ainda precisam ser conferidas em uso normal; os testes desta etapa não controlaram a tela nem o mouse. Não foi publicada Release no GitHub nem alterado o serviço da Cloudflare.
