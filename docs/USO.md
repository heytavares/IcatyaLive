# Usar o MoabLive 0.3.1

## Abrir e testar sozinho

1. Feche as janelas das versões anteriores e abra **Iniciar MoabLive.cmd** na pasta principal.
2. Clique em **Testar neste PC**. Uma segunda janela será aberta como receptora.
3. Aguarde a confirmação de conexão local.
4. Na primeira janela, escolha o monitor e comece pelo perfil **720p30**.
5. Clique em **Transmitir tela** e confira a segunda janela.
6. Confira o nome do codificador e os fps medidos. `h264_amf · GPU` indica AMD AMF; `libopenh264 · CPU` indica a alternativa por processador.
7. Clique em **Parar captura**. A imagem deve desaparecer do receptor. Você pode iniciar novamente ou desconectar.

A seleção do monitor só começa a ser capturada ao iniciar a transmissão ou a prévia. O efeito de espelho é esperado quando o monitor compartilhado contém o próprio programa.

Os perfis são metas, não garantia de desempenho. Os testes deste PC mediram perto de 30 fps no caminho de captura e transmissão. A reprodução visual e os jogos precisam ser avaliados separadamente. Nenhum áudio é enviado na versão 0.3.1.

## Testar com um amigo

Confira o papel no alto da coluna: **VOCÊ TRANSMITE** no PC que cria o convite; **VOCÊ ASSISTE** no PC que recebe o convite. Somente o transmissor aplica a resposta. Se houver falha, use **Copiar diagnóstico** nas duas pontas.

Ambos precisam da versão 0.3.1. Vocês podem continuar na chamada de voz do Discord.

1. Quem vai compartilhar clica em **Criar convite para transmitir**.
2. Copia o texto gerado e envia ao amigo pelo chat privado.
3. O amigo cola em **Convite recebido** e clica em **Assistir / gerar resposta**.
4. O amigo copia a resposta e a devolve pelo chat.
5. Quem criou o convite cola a resposta e clica em **Aplicar resposta**.
6. Após conectar, quem transmite escolhe o monitor/perfil e clica em **Transmitir tela**.

Os convites expiram após 20 minutos para iniciar a conexão e contêm informações de rede. Envie apenas a quem deseja conectar. O teste local não comprova a conexão entre redes diferentes; redes restritivas ou CGNAT podem impedir o funcionamento direto, pois não há retransmissor TURN.

## Recebeu um ZIP?

Extraia todo o ZIP para uma pasta e abra **MoabLive.exe**. Mantenha as bibliotecas e a pasta `native` junto do aplicativo. O pacote inclui o runtime para Windows x64 e não exige Visual Studio.

## Clonou o GitHub?

Execute **Preparar video.cmd** para baixar e verificar as bibliotecas. Depois use o iniciador, com o SDK .NET 10 instalado. Os componentes ficam em `native/ffmpeg`, fora do Git. Os ZIPs locais ficam em `releases`.

## Ainda pendente

60 fps estáveis com captura/apresentação completas, captura moderna por GPU, áudio por aplicativo, adaptação automática à rede, teste de redes distintas e medições durante jogos. O requisito de seletor de áudio está registrado em AUDIO-POR-APLICATIVO.md.

