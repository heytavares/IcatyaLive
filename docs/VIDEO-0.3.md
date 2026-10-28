# MoabLive 0.3 — vídeo H.264

A versão 0.3 substitui o codec VP8 de teste por H.264 nativo, com seleção de codificador por GPU e alternativa por CPU. A AMD Radeon RX 580 2048SP deste computador iniciou o codificador `h264_amf` e enviou quadros que foram recebidos e decodificados por outro participante local.

## O que mudou

- Seleção e abertura real do codificador: AMD AMF, NVIDIA NVENC, Intel QSV e OpenH264 por CPU. Somente o caminho que consegue gerar quadros é aceito. AMD AMF e OpenH264 foram testados neste PC; NVIDIA e Intel ainda não foram validados em hardware real.
- Imagem reduzida diretamente para um bitmap nativo reutilizado, preservando a proporção do monitor.
- Captura e codificação em tarefas separadas, com capacidade de apenas um quadro pendente; o quadro antigo é liberado quando chega outro.
- Buffers compartilhados e reutilizados com contagem de referências. O capturador, o decodificador e a interface liberam suas próprias referências.
- A interface continua exibindo apenas o quadro mais recente e reutiliza o bitmap de apresentação.
- Contextos, pacotes e imagens do FFmpeg são liberados ao encerrar a sessão.
- FPS informados pela interface são medidos; escolher um perfil não significa que a meta foi atingida.

## Medição local de captura e transmissão

Teste curto, com dois participantes no mesmo processo, tela com variação de pixels e captura do monitor principal. Não inclui apresentação WPF, jogo em execução ou rede externa.

| Perfil configurado | Captura + codificação processadas | Quadros recebidos em cerca de 4 s |
| --- | --- | --- |
| 720p, meta 30 fps | 26,8 fps | 107 |
| 720p, meta 60 fps | 29,7 fps | 118 |
| 1080p, meta 60 fps | 29,7 fps | 118 |

O teste de codificação isolada do FFmpeg conseguiu processar conteúdo sintético 1080p60 pela AMD. O teste integrado acima mostra que isso não equivale a uma live de tela em 60 fps: o caminho completo ainda ficou próximo de 30 fps. Captura moderna por GPU e redução das sincronizações são próximas linhas de investigação; não prometer desempenho durante jogos antes de medir.

## Perfis

720p30 (4 Mbps), 720p60 como meta (6 Mbps), 1080p30 (7 Mbps), 1080p60 como meta (10 Mbps). Os valores de bitrate são configurações do codificador, não medições do tráfego efetivo. Ainda não há adaptação automática à rede.

## Testes e limites

Compilação Release sem avisos ou erros. Verificações cobrem: validade e expiração dos convites, sessão errada, abertura de conexão local, pixels recebidos em H.264, caminho AMD, caminho CPU, pausa/retomada, bloqueio de envio após parar, propriedade dos buffers, descarte, troca 720p → 1080p → 720p no mesmo decodificador e STUN público. A captura integrada também foi exercitada automaticamente sem usar o mouse.

A interface e os perfis foram inspecionados. A verificação visual completa de reprodução nesta versão ficou pendente para permitir que o usuário continuasse usando mouse e teclado. Verificações anteriores da 0.2 não substituem esse teste da 0.3.

As duas pontas precisam usar a 0.3, pois a 0.2 só oferece VP8. Não há teste confirmado entre redes distintas, áudio, captura de aplicativo individual, adaptação à rede ou desempenho durante jogos. O requisito de áudio seletivo está em AUDIO-POR-APLICATIVO.md e ainda não foi implementado.

## Componentes locais

FFmpeg.AutoGen 8.1.0 e FFmpeg n8.1.3-24-g92611900bb, compilação BtbN de 27/10/2026. Bibliotecas compartilhadas substituíveis, mantidas em `native/ffmpeg`, fora do Git. O script `scripts/Preparar-Video.ps1` baixa um pacote fixo, confere o SHA-256 e copia apenas quatro DLLs e os avisos de licença necessários. Não instala serviços nem altera configurações do Windows.

