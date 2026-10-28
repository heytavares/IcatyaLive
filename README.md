# MoabLive

Aplicativo Windows em C# e WPF para compartilhar tela por conexão direta. Versão atual: **0.3.1 experimental**, com H.264 e codificação por GPU ou CPU. Ainda sem áudio. Neste PC, a captura e transmissão locais mediram perto de 30 fps; 60 fps estáveis ainda não foram atingidos.

## Projeto principal

Abra **MoabLive.slnx** no Visual Studio.

- Aplicativo: `src/MoabLive/MoabLive.csproj`.
- Testes: `tests/MoabLive.Checks/MoabLive.Checks.csproj`.
- Tecnologia: .NET 10, Windows x64 e WPF.

## Pastas

- `src`: código, modelos e serviços de captura/conexão/vídeo.
- `tests`: verificações de integração e medição de captura.
- `docs`: guias, resultados e requisitos.
- `scripts`: preparação dos componentes de vídeo e envio ao GitHub.
- `app`, `releases`, `native` e `work`: arquivos locais, ignorados pelo Git.

## Abrir

Execute **Iniciar MoabLive.cmd**. O iniciador abre o pacote 0.3.1 pronto, quando presente. A versão 0.2 continua guardada em `app/0.2` como opção de retorno.

Se clonou apenas os fontes: execute **Preparar video.cmd** uma vez para baixar os componentes locais. Depois execute o iniciador, com o SDK .NET 10 e desenvolvimento desktop instalados.

## Testar

Clique em **Testar neste PC**, aguarde conectar, escolha o monitor e o perfil e clique em **Transmitir tela**. Confira a segunda janela e depois teste **Parar captura**. Comece em **720p30**. Os perfis de 60 fps são metas experimentais; confira a medição na interface.

Para usar com um amigo, ambos precisam da 0.3. Veja [o guia de uso](docs/USO.md). STUN público auxilia a conexão, sem retransmitir a tela; algumas redes continuam exigindo retransmissão, que o MoabLive não oferece.

[Correção de conexão e tela receptora da 0.3.1](docs/CORRECAO-0.3.1.md).

## Evolução

[Detalhes e medições da 0.3](docs/VIDEO-0.3.md). [Áudio seletivo por aplicativo](docs/AUDIO-POR-APLICATIVO.md) é um requisito futuro: nenhuma captura de áudio existe nesta versão.

## Enviar ao GitHub

Execute **Enviar para GitHub.cmd**. Destino: https://github.com/heytavares/MoabLive. O script inclui fontes, testes, documentação e scripts, cria um commit quando há alterações e envia para a branch atual. Credenciais, executáveis, DLLs, ZIPs, caches e componentes baixados ficam de fora. Não realiza push forçado.

[Instruções do GitHub](docs/GITHUB.md). Nenhum commit ou push é realizado automaticamente ao abrir o programa ou preparar seus componentes.

## Compilar e verificar

Com os componentes preparados e o SDK .NET 10, execute na raiz:

```powershell
dotnet build MoabLive.slnx -c Release
dotnet run --project tests/MoabLive.Checks/MoabLive.Checks.csproj -c Release
```

Medição opcional, que captura o monitor principal e envia para um receptor local por alguns segundos, sem gravar imagens:

```powershell
dotnet run --project tests/MoabLive.Checks/MoabLive.Checks.csproj -c Release -- --capture-benchmark
```

## Dependências

SIPSorcery 10.0.17 e FFmpeg.AutoGen 8.1.0, restaurados pelo NuGet. O script de preparação mantém o FFmpeg compartilhado separado dos fontes. Consulte as licenças, a origem e as instruções de reconstrução nos avisos do pacote. Nenhuma licença dos componentes de terceiros é substituída pelo código do aplicativo.

