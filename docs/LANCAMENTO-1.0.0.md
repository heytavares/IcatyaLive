# Publicar o iCatya 1.0.0

Código e downloads são duas publicações diferentes. Os arquivos já foram preparados localmente; este guia não executa commit, push ou publicação de Release.

## 1. Atualizar o código

Execute o atalho pessoal **Enviar para GitHub.cmd**. A janela deve mostrar:

```text
Fontes do iCatya 1.0.0
Pasta: C:\Users\andre\Documents\Codex\Projetos\Icatya
```

Aguarde a mensagem de envio concluído. Acesse [o repositório](https://github.com/heytavares/IcatyaLive), selecione a branch **main** e confira o novo README e a versão `1.0.0` em `src/Icatya/Icatya.csproj`.

Não arraste a pasta do projeto inteira pelo site. O script seleciona os fontes e exclui saídas locais, executáveis, DLLs, caches, backups e atalhos `.cmd`. As exclusões preparadas dos atalhos retiram esses arquivos da versão atual do repositório, preservando as cópias no PC. O histórico de commits antigos continua existindo.

`AGENTS.md` contém instruções para ferramentas de desenvolvimento, não é necessário para executar o app e não entra nos pacotes. Mantê-lo no repositório é opcional.

## 2. Criar o rascunho da Release

Abra [Releases](https://github.com/heytavares/IcatyaLive/releases) e escolha **Draft a new release / Criar nova versão**.

- Tag: **v1.0.0**. Crie essa tag na branch **main**, depois de enviar o código atualizado.
- Título: **iCatya 1.0.0**.
- Descrição: copie o conteúdo de [TEXTO-RELEASE-1.0.0.md](TEXTO-RELEASE-1.0.0.md).
- Salve primeiro como rascunho se precisar concluir os anexos.

## 3. Anexar os seis arquivos

Pasta local: `C:\Users\andre\Documents\Codex\Projetos\Icatya\releases\1.0.0\`.

Anexe individualmente estes arquivos à mesma Release, sem mudar os nomes:

| Arquivo local | Função |
| --- | --- |
| `Icatya-1.0.0-Instalador.exe` | Download recomendado; aproximadamente 94,6 MiB. |
| `Icatya-1.0.0-Instalador.exe.sha256` | Integridade do instalador. |
| `Icatya-1.0.0-Portatil.zip` | Download para extrair e abrir; aproximadamente 130,3 MiB. |
| `Icatya-1.0.0-Portatil.zip.sha256` | Integridade do ZIP portátil. |
| `atualizacao/Icatya-1.0.0-Windows-x64.zip` | Pacote técnico necessário ao atualizador; aproximadamente 130,3 MiB. |
| `atualizacao/Icatya-1.0.0-Windows-x64.zip.sha256` | Integridade do pacote técnico. |

O GitHub recebe os arquivos, não a pasta `atualizacao`: o ZIP técnico aparecerá nos anexos com seu nome normal. **Ele é necessário para que usuários de versões anteriores consigam atualizar pelo app.** Não substitua esse ZIP pelo portátil: os formatos internos são diferentes.

Não anexe `app/`, `work/`, `native/`, o `iCatya.exe` iniciador da raiz ou as DLLs individualmente. Não é necessário anexar o texto deste guia. Os arquivos automáticos `Source code` são apenas os fontes e não substituem os downloads acima.

## 4. Publicar

Confirme que todos os uploads terminaram. Para distribuir a 1.0.0 como versão estável:

- Deixe **Set as a pre-release** desmarcado.
- Marque **Set as the latest release**, quando essa opção estiver disponível.
- Clique em **Publish release**.

O atualizador consulta somente a Release estável mais recente. Um rascunho ou uma prévia não será oferecido automaticamente.

## 5. Conferir os downloads publicados

1. Confira os botões do README e a página da Release.
2. Baixe o instalador da própria página para conferir o arquivo entregue. Faça a instalação/abertura em outro PC Windows compatível quando possível.
3. Confira também o ZIP portátil: extraia tudo; deve existir uma única pasta `iCatya`, com o executável diretamente dentro dela, sem `app/` no caminho.
4. Em uma cópia antiga do app, abra Configurações e confira se oferece **1.0.0**. Não precisa baixar outra vez manualmente se o atualizador daquela versão funcionar.
5. Após atualizar, confira nick, avatar e sala. Na 1.0.0, a consulta deve informar que já está atualizada.

Se a consulta disser que falta o pacote, confira o nome exato `Icatya-1.0.0-Windows-x64.zip` e se ele terminou de carregar. A verificação também depende do SHA256 disponibilizado pelo GitHub nos metadados; se ele ainda não estiver pronto, aguarde e tente novamente. Os arquivos `.sha256` anexados servem para conferência manual e não substituem o metadado usado pelo app.

## Conferência feita antes da entrega

- Instalador extraído em modo de teste, sem abrir o app, criar atalhos ou registrar uma instalação: **514 arquivos idênticos à origem por SHA256**.
- ZIP portátil extraído em diretório novo: uma única pasta `iCatya`, executável e componentes presentes.
- SHA256 dos três pacotes conferido com os arquivos auxiliares.
- Compilação e testes anteriores da interface concluídos, incluindo fechamento/reabertura de live e controles ao ficar ocioso.

A conferência local não equivale a um teste da atualização publicada no GitHub ou em todas as redes dos usuários. Faça a etapa 5 após publicar. 60 fps continuam experimentais; redes que impedem conexões diretas ainda podem falhar por ausência de TURN.

Referência: [documentação oficial do GitHub para criar e publicar Releases](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository).
