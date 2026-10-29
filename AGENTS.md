# Distribuição e organização do iCatya

- Projeto principal nesta pasta. Preserve versões e pacotes anteriores em `../Backups`, com versão e data, antes de substituí-los. Nunca apague arquivos do usuário para organizar a pasta.
- Mantenha somente a versão atual em `app`. Arquivos de desenvolvimento, cache e componentes nativos não devem ser enviados aos amigos nem incluídos no Git.
- Use `scripts/Empacotar-Versao.ps1` para gerar a distribuição. Saída em `releases/<versão>/`.
- `scripts/Organizar-Pastas.ps1` preserva saídas antigas em Backups, mantém a versão atual e não move uma versão cujo executável esteja em uso.
- O instalador é o arquivo principal para usuários. O ZIP portátil deve conter **uma única pasta principal `iCatya/`**. Nunca distribua ZIP portátil que espalhe DLLs e outros arquivos diretamente na pasta Downloads ao extrair.
- O ZIP técnico do atualizador permanece separado em `releases/<versão>/atualizacao/`, com arquivos na raiz para compatibilidade com os atualizadores existentes. Seu nome é `Icatya-<versão>-Windows-x64.zip`. Não é o ZIP recomendado para extração manual.
- Conservar o perfil em `%LOCALAPPDATA%\Icatya`. O instalador usa `%LOCALAPPDATA%\Programs\iCatya` e não modifica regras de firewall, configurações de segurança nem certificados.
- Antes de entregar, verificar SHA256, extração do instalador em modo de teste e a pasta principal do ZIP portátil. Não prometer ausência de alertas do Windows enquanto os binários não estiverem assinados.
