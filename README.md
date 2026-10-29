# iCatya

[Notas da versão 1.0.0](docs/NOTAS-1.0.0.md). Fontes atualizados; a Release e os downloads da 1.0.0 ainda não foram publicados.

Aplicativo Windows em C# e WPF para compartilhar telas simultâneas em salas com convite permanente, nick e retorno automático à última sala. O vídeo usa conexão direta; presença e sinalização usam um serviço separado na Cloudflare. Versão atual dos fontes: **1.0.0** (pacotes ainda não publicados), com H.264 e codificação por GPU ou CPU. O controle de cadência foi corrigido e a interface informa separadamente os fps processados/recebidos e os enviados para apresentação. Áudio estéreo seletivo por aplicativo disponível (Windows build 20348+). Seleção de janela ou monitor disponível; veja as notas e verificações desta versão. Neste PC, a captura e transmissão locais mediram perto de 30 fps; 60 fps estáveis ainda não foram atingidos.

[Correção de corrupção do vídeo (0.7.1 video-fix.1)](docs/CORRECAO-VIDEO-0.7.1.md).

[Recuperação de lives na 0.7.3 em desenvolvimento](docs/NOTAS-0.7.3.md). Por solicitação do usuário, ainda sem novos pacotes de distribuição; os downloads abaixo são da 0.7.2.

[Nova interface, ícones Lucide e fechar/reabrir uma live individual](docs/UI-0.7.3.md), também nos fontes da 0.7.3. Cabeçalho sem slogan.

[Seletor de salas salvas, nomes compartilhados, nova logo e ajuda com Discord](docs/SELETOR-SALAS-0.7.3.md), disponíveis na prévia local da 0.7.3.

[Correção da exibição que ficava em 15–20 fps](docs/EXIBICAO-0.7.3.md). Superfícies alternadas, apresentação BGR32, miniaturas mais leves e comparação antes/depois.

[Melhorias de exibição e atualizador da 0.7.1](docs/NOTAS-0.7.1.md). A validação visual de FPS em uma live real ainda está pendente.

## Para baixar e enviar aos amigos

Use o **instalador** em `releases/0.7.2/`. A alternativa é o **ZIP Portatil**, que extrai uma única pasta `iCatya`. O ZIP técnico do atualizador fica separado em `atualizacao/`. [Distribuição, avisos do Windows e organização](docs/INSTALADOR-0.7.2.md).

## Projeto principal

Abra **icatyalive.slnx** no Visual Studio.

- Aplicativo: `src/Icatya/Icatya.csproj`.
- Testes: `tests/Icatya.Checks/Icatya.Checks.csproj`.
- Tecnologia: .NET 10, Windows x64 e WPF.

## Pastas

- `src`: código, modelos e serviços de captura/conexão/vídeo.
- `tests`: verificações de integração e medição de captura.
- `docs`: guias, resultados e requisitos.
- `scripts`: preparação dos componentes de vídeo e envio ao GitHub.
- `app`, `releases`, `native` e `work`: arquivos locais, ignorados pelo Git.

## Abrir

Execute **Iniciar Icatya.cmd**. O iniciador compila e abre os fontes atuais, usando o SDK .NET 10. As versões anteriores ficam preservadas na pasta Backups, ao lado do projeto.

Se clonou apenas os fontes: execute **Preparar video.cmd** uma vez para baixar os componentes locais. Depois execute o iniciador, com o SDK .NET 10 e desenvolvimento desktop instalados.

## Perfil e avatares

Escolha seu avatar na engrenagem. A seleção fica salva e é mostrada aos amigos da sala. O painel de perfil acompanha a lateral recolhível. Veja [notas da 0.6.3](docs/NOTAS-0.6.3.md).

## Foco e reconexão

A 0.6.1 corrige presenças duplicadas na reconexão e acrescenta grade de cartões e foco de telas. Clique no vídeo para ampliar, use as miniaturas para trocar, e Esc para voltar. Atualize ambos os PCs. Veja [notas da 0.6.1](docs/NOTAS-0.6.1.md).

## Salas

Na primeira abertura, escolha nick e convite ou crie uma sala. Os amigos usam o mesmo convite, sem trocar respostas. O serviço de salas já está publicado na Cloudflare e o pacote inclui seu endereço. Veja [salas 0.6.0](docs/SALAS-0.6.0.md).

## Testar

Clique em **Testar neste PC**, aguarde conectar, escolha o monitor e o perfil e clique em **Transmitir tela**. Confira a segunda janela e depois teste **Parar captura**. Comece em **720p30**. Os perfis de 60 fps são metas experimentais; confira a medição na interface.

Para usar com um amigo, ambos devem usar a 0.7.0. Veja [o guia de uso](docs/USO.md). STUN público auxilia a conexão, sem retransmitir a tela; algumas redes continuam exigindo retransmissão, que o Icatya não oferece.

[Correção de conexão e tela receptora da 0.3.1](docs/CORRECAO-0.3.1.md).

## Evolução

[Detalhes e medições da 0.3](docs/VIDEO-0.3.md). [Áudio seletivo e escolha de aplicações/telas](docs/NOTAS-0.7.0.md): uso, requisitos e verificações.

## Enviar ao GitHub

Execute **Enviar para GitHub.cmd**. Destino: https://github.com/heytavares/IcatyaLive. O script inclui fontes, testes, documentação e scripts, cria um commit quando há alterações e envia para a branch atual. Credenciais, executáveis, DLLs, ZIPs, caches e componentes baixados ficam de fora. Não realiza push forçado.

[Instruções do GitHub](docs/GITHUB.md). Nenhum commit ou push é realizado automaticamente ao abrir o programa ou preparar seus componentes.

## Compilar e verificar

Com os componentes preparados e o SDK .NET 10, execute na raiz:

```powershell
dotnet build icatyalive.slnx -c Release
dotnet run --project tests/Icatya.Checks/Icatya.Checks.csproj -c Release
```

Medição opcional, que captura o monitor principal e envia para um receptor local por alguns segundos, sem gravar imagens:

```powershell
dotnet run --project tests/Icatya.Checks/Icatya.Checks.csproj -c Release -- --capture-benchmark
```

## Dependências

SIPSorcery 10.0.17 e FFmpeg.AutoGen 8.1.0, restaurados pelo NuGet. O script de preparação mantém o FFmpeg compartilhado separado dos fontes. Consulte as licenças, a origem e as instruções de reconstrução nos avisos do pacote. Nenhuma licença dos componentes de terceiros é substituída pelo código do aplicativo.



## Identidade e tema

Nome do aplicativo: **Icatya**. Nome escolhido para o projeto: **icatyalive**. A pasta principal atual é `C:\Users\andre\Documents\Codex\Projetos\Icatya`. O endereço remoto agora é heytavares/IcatyaLive.

[Paleta de cores](docs/PALETA-0.4.md).

[Desempenho e estabilidade da 0.4.1](docs/DESEMPENHO-0.4.1.md). Verificação prolongada: `dotnet run --project tests/Icatya.Checks/Icatya.Checks.csproj -c Release -- --capture-stability`.

[Atualizador e publicação de versões](docs/ATUALIZACOES.md). O pacote inclui **Atualizar Icatya.cmd**; o envio dos fontes ao GitHub é separado da publicação de uma Release.

[Layout e configurações da 0.6.0](docs/LAYOUT-0.6.0.md). [Alternativas para salas em estudo](docs/SALAS-ALTERNATIVAS.md).
