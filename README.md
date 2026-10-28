# MoabLive

Aplicativo Windows em C# e WPF para compartilhar tela por conexão direta entre computadores. Versão atual: **0.2 experimental**, sem áudio e com vídeo de teste a até 10 fps. 1080p60 e aceleração por GPU ainda não estão implementados.

## Projeto principal

A raiz do repositório é esta pasta **MoabLive**. Abra **MoabLive.slnx** no Visual Studio para carregar o aplicativo e os testes.

- Aplicativo: `src/MoabLive/MoabLive.csproj`.
- Testes de integração: `tests/MoabLive.Checks/MoabLive.Checks.csproj`.
- Tecnologia: .NET 10, Windows x64, WPF.

## Organização

```text
MoabLive/
├── src/MoabLive/             Código do aplicativo
│   ├── Models/              Quadros e perfis de captura
│   └── Services/            Captura, convites e conexão
├── tests/MoabLive.Checks/    Verificações de integração
├── docs/                    Instruções e resultados de testes
├── scripts/                 Automação do envio ao GitHub
├── app/                     Aplicativo pronto local — ignorado pelo Git
├── releases/                Pacotes ZIP locais — ignorados pelo Git
├── MoabLive.slnx            Solução para abrir no Visual Studio
├── Iniciar MoabLive.cmd     Abre o aplicativo
└── Enviar para GitHub.cmd   Cria commit e envia as alterações
```

O Visual Studio também pode criar `.vs/`, `bin/` e `obj/`. Essas pastas são locais e não são enviadas.

## Abrir o aplicativo

Na instalação local preparada, execute **Iniciar MoabLive.cmd**. Se o aplicativo pronto não estiver presente, o iniciador tenta executar o projeto a partir dos fontes; nesse caso, precisa do SDK .NET 10 e do desenvolvimento desktop para .NET.

Para testar a conexão sozinho ou com um amigo, consulte [o guia de uso](docs/USO.md). A conexão direta entre redes diferentes ainda precisa ser validada; algumas redes exigem retransmissão, que esta versão não oferece.

## Enviar ao GitHub

Repositório: https://github.com/heytavares/MoabLive

Execute **Enviar para GitHub.cmd** na raiz. Ele inclui código, testes, documentação e scripts, cria um commit quando existem alterações e faz o envio para a branch atual no GitHub. O nome e o e-mail do autor são solicitados se não estiverem configurados. O Git pode pedir sua autenticação do GitHub.

Executáveis, bibliotecas baixadas, ZIPs, caches do Visual Studio, arquivos temporários, logs e arquivos comuns de credenciais ficam excluídos. O script também limita as pastas incluídas e interrompe o envio se encontrar arquivos privados comuns ou arquivos acima de 10 MB. Isso não substitui revisar o código antes de incluir senhas ou outros dados privados.

O script não faz push forçado e não resolve divergências ou conflitos automaticamente. Se o repositório remoto tiver alterações incompatíveis, o envio para sem sobrescrever o histórico remoto.

Detalhes: [guia do GitHub](docs/GITHUB.md).

## Compilar e verificar

Com o SDK .NET 10 instalado, na raiz:

```powershell
dotnet build MoabLive.slnx -c Release
dotnet run --project tests/MoabLive.Checks/MoabLive.Checks.csproj -c Release
```

As verificações criam conexões locais e verificam quadros reais de vídeo. Consulte [o registro de validação da versão 0.2](docs/VERIFICACAO-0.2.txt). O teste local não comprova conectividade entre redes distintas.

## Dependências

SIPSorcery 10.0.17 e SIPSorcery.VP8 10.0.17, restaurados pelo NuGet. As dependências compiladas não precisam ser enviadas ao repositório. Os avisos e licenças acompanham o pacote local de distribuição.
