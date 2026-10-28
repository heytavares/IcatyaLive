# Enviar o MoabLive ao GitHub

## Uso normal

1. Salve os arquivos editados no Visual Studio.
2. Execute **Enviar para GitHub.cmd**, na pasta principal do MoabLive.
3. Na primeira vez, se solicitado, informe seu nome e e-mail para os commits. Você pode usar o e-mail noreply fornecido pelo GitHub.
4. Caso o Git peça autenticação, entre na sua conta pelo mecanismo de autenticação do próprio Git. Não coloque tokens no código ou no script.
5. Aguarde a mensagem de sucesso. Se ocorrer um erro, a janela permanece aberta para você ler.

Destino configurado: https://github.com/heytavares/MoabLive

O envio não roda automaticamente ao abrir o aplicativo. Ele só acontece quando você executa o arquivo de envio. Nenhum commit ou push foi feito ao preparar esse arquivo.

## O que ele faz

- Confere se está dentro do repositório MoabLive.
- Confere se o destino é o repositório acima.
- Consulta o GitHub antes de preparar o envio.
- Inclui alterações e exclusões nas pastas de código, testes, documentação e scripts, além dos arquivos de projeto na raiz.
- Cria um commit com data e hora quando há alterações.
- Envia os commits para a branch atual.

Se não houver alterações novas, ele ainda tenta enviar commits locais pendentes. Não faz push forçado, não apaga arquivos locais e não sobrescreve divergências do histórico remoto.

## Conferir sem enviar

No PowerShell, a partir da raiz:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Enviar-GitHub.ps1 -VerifyOnly
```

Esse modo lista os arquivos candidatos e não cria commit, não prepara alterações e não usa a rede. A opção de execução vale apenas para aquele processo e não altera a configuração permanente do Windows.

## O que fica local

- `app/`: programa compilado e runtime.
- `releases/`: arquivos ZIP.
- `.vs/`, `bin/`, `obj/`: caches e resultados de compilação.
- Temporários, logs e extensões comuns de certificados e credenciais.

Essas pastas podem continuar existindo normalmente no computador. O `.gitignore` impede que sejam incluídas pelo fluxo padrão, e o script confere os arquivos antes do commit.

Se criar outra pasta de código na raiz, ela deve ser adicionada à lista `Get-ProjectPaths` do script antes de ser enviada. Arquivos com segredos dentro de código normal não são detectados automaticamente.

## Se o envio falhar

- **Autenticação**: entre na conta que possui acesso ao repositório.
- **Repositório não encontrado**: confira se ele existe e se sua conta possui permissão.
- **Histórico remoto diferente**: os arquivos e commits locais ficam preservados. Peça a integração do histórico antes de tentar novamente; não use envio forçado para resolver.
- **Arquivo recusado**: mantenha executáveis e ZIPs nas pastas locais indicadas. Não inclua credenciais.

Um repositório remoto que já contenha README ou outros commits pode precisar de uma integração inicial, pois o repositório local foi criado a partir dos arquivos existentes no computador.
