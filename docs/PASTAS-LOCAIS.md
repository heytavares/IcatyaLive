# Organização local

Projeto principal: `C:\Users\andre\Documents\Codex\Projetos\Icatya`.

Versões anteriores: `C:\Users\andre\Documents\Codex\Projetos\Backups`.

Ao preparar uma nova versão, preservar a versão anterior na pasta Backups, identificada pelo número da versão e pela data. Manter o projeto atual neste diretório. Não usar os caminhos antigos em C:\Projetos ou nas pastas de conversas anteriores.

Distribuição atual: `releases/0.7.2`. Enviar aos usuários o instalador ou o ZIP `Portatil`, que contém uma única pasta `iCatya/`. O ZIP na subpasta `atualizacao` serve ao atualizador e mantém a estrutura compatível com as versões antigas.

`Gerar instalador.cmd` prepara os pacotes e organiza as versões anteriores automaticamente. `scripts/Organizar-Pastas.ps1` também pode ser executado separadamente. Pastas ou arquivos em uso são preservados para uma tentativa posterior, sem fechar programas.
