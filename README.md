<p align="center">
  <img src="src/Icatya/Assets/logo-icatya.png" width="96" alt="Logo do iCatya">
</p>

<h1 align="center">iCatya</h1>

Aplicativo para Windows que permite compartilhar e assistir a várias telas com seus amigos, em salas com convite permanente, sem criar uma conta.

**Versão 1.0.0 · Windows x64**

## Baixar

Acesse a [Release 1.0.0](https://github.com/heytavares/IcatyaLive/releases/tag/v1.0.0) e escolha:

| Arquivo | Para quem é |
| --- | --- |
| **Icatya-1.0.0-Instalador.exe** | Opção recomendada. Instala o app e permite criar um atalho na Área de Trabalho. |
| **Icatya-1.0.0-Portatil.zip** | Para usar sem instalar. Extraia tudo e abra `Icatya.exe` dentro da pasta `iCatya`. |

Os links ficam disponíveis após a publicação da Release. Para acompanhar as próximas versões, consulte [Releases](https://github.com/heytavares/IcatyaLive/releases).

**Para usar o app, baixe um dos arquivos acima.** A opção `Code → Download ZIP` e os arquivos `Source code` contêm o código do projeto, não o aplicativo pronto. O pacote `Icatya-1.0.0-Windows-x64.zip` é destinado ao atualizador.

No ZIP portátil, mantenha os arquivos internos junto do executável. A estrutura já permite abrir o app diretamente, sem uma pasta `app` intermediária:

```text
iCatya/
├── Icatya.exe
├── arquivos internos
├── licenses/
└── native/
```

## O que o iCatya oferece

- Salas com convite permanente, nick e avatar, sem login.
- Salas salvas, pesquisa e retorno à última sala utilizada neste PC.
- Várias transmissões simultâneas, com visualização em grade ou foco.
- Captura de monitor inteiro ou de uma janela de aplicativo.
- Perfis de 720p e 1080p; 60 fps identificados como experimentais.
- Seleção dos aplicativos cujo áudio será transmitido, com controles de volume.
- Fechamento de uma live apenas para você, mantendo um cartão para reabri-la.
- Tentativas automáticas de recuperação de conexões interrompidas.
- Tema escuro, barra lateral ajustável e controles sobre o vídeo que somem quando o mouse fica parado.
- Verificação de atualizações pelas configurações.

## Começar a usar

1. Abra o iCatya e escolha seu nick. Crie uma sala ou entre usando um convite recebido.
2. Use **Convidar** para copiar o convite e enviar aos amigos. Todos usam o mesmo convite.
3. No botão de compartilhar tela, escolha uma janela ou monitor; depois ajuste resolução, fps e áudio. Comece com **720p, 30 fps**.
4. Selecione explicitamente os aplicativos cujo som pode entrar na live. Deixe o Discord fora da seleção se não quiser transmiti-lo.
5. Clique em uma live para focar. Clique novamente na imagem focada ou pressione **Esc** para voltar à grade.

O **X** fecha somente a recepção daquela live no seu PC. A pessoa continua transmitindo e aparece como **Ao vivo** na lista. Clique no cartão com seu avatar e nick para assistir novamente.

A engrenagem reúne avatar, atualização, diagnóstico e ajuda. O seletor no nome da sala permite alternar entre salas salvas, criar outra ou renomear a atual. O nome da sala é compartilhado com os participantes.

## Requisitos e limitações

- **Windows 10 versão 2004/build 19041 ou superior, x64.** Windows 11 é recomendado para o áudio por aplicativo, que exige build **20348 ou superior**.
- Instalador e ZIP portátil incluem o runtime necessário; não é preciso instalar o SDK para usar o app.
- A qualidade e a fluidez dependem dos PCs, da conexão e da quantidade de transmissões. **60 fps são experimentais e não são garantidos.**
- A captura seletiva de áudio também precisa ser conferida com seus aplicativos e dispositivo. Compartilhar uma janela não seleciona automaticamente todos os sons desejados.
- As telas e o áudio usam conexões diretas entre os PCs. O serviço na Cloudflare organiza a presença e o encontro entre os participantes, sem retransmitir a mídia.
- Algumas redes podem impedir a conexão direta. O iCatya ainda não oferece retransmissor TURN; por isso, nem toda combinação de redes consegue se conectar.
- O app ainda não possui assinatura digital. O Windows pode mostrar avisos de reputação ou pedir permissão de rede. Não é necessário desativar o antivírus ou o firewall.

## Atualizações

Abra **Configurações → Verificar atualizações**. Quando houver uma versão nova, use o botão de instalação exibido pelo app.

O atualizador verifica o pacote, substitui os arquivos do aplicativo e reabre o programa. O perfil fica separado em `%LOCALAPPDATA%\Icatya`, para preservar nick, avatar e salas. Mantenha o arquivo interno `Atualizar-Icatya.ps1`; o atalho externo `.cmd` é opcional.

## Ajuda

Encontrou um problema? Entre em contato pelo Discord: **fvckjax**.

Você também pode [abrir uma issue](https://github.com/heytavares/IcatyaLive/issues). Informe a versão, sua versão do Windows e como reproduzir o problema. Para falhas de conexão, copie o diagnóstico nas configurações; se possível, confira os dois PCs envolvidos. Não publique convites de salas privadas.

## Compilar pelos fontes

Requer Windows x64, **SDK .NET 10** com suporte a aplicações desktop e acesso à internet para restaurar os componentes.

Clone o repositório e execute os comandos abaixo na raiz:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Preparar-Video.ps1
dotnet build icatyalive.slnx -c Release
dotnet run --project src/Icatya/Icatya.csproj -c Release
```

Os atalhos pessoais `.cmd` não fazem parte do repositório. Os scripts de manutenção ficam em `scripts/`.

| Pasta | Conteúdo |
| --- | --- |
| `src/Icatya` | Aplicativo C#/WPF. |
| `src/Icatya.Rooms` | Serviço de presença e sinalização das salas. |
| `tests` | Verificações de interface, vídeo, áudio, conexão e distribuição. |
| `scripts` | Preparação, compilação e empacotamento. |
| `docs` | Guias, notas técnicas e licenças de terceiros. |

`app`, `releases`, `work`, `native`, executáveis, pacotes e atalhos pessoais são saídas locais e ficam fora do Git. Não coloque credenciais nos fontes.

## Componentes e documentação

O projeto utiliza .NET/WPF, SIPSorcery, FFmpeg.AutoGen/FFmpeg, NAudio, Concentus, SharpDX e ícones Lucide. As licenças e avisos de terceiros acompanham os pacotes; consulte também [docs/LICENCAS](docs/LICENCAS).

- [Notas da 1.0.0](docs/NOTAS-1.0.0.md)
- [Guia de publicação da 1.0.0](docs/LANCAMENTO-1.0.0.md)
- [Envio do código ao GitHub](docs/GITHUB.md)
