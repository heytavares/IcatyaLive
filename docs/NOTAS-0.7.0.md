# iCatya 0.7.0 — áudio seletivo e fontes de vídeo

## Usar

1. Abra o botão de compartilhar e escolha **Aplicações** (uma janela) ou **Telas** (um monitor inteiro).
2. Clique em **Próximo** e ajuste resolução e FPS.
3. Para enviar som, habilite **Áudio de aplicativos** e marque somente o jogo/player/navegador desejado. Ajuste o volume de cada aplicativo e o master.
4. Inicie a transmissão. O rodapé informa as fontes selecionadas e mostra o nível do som. O volume no canto superior direito controla o som que você recebe das lives; o alto-falante silencia/restaura esse volume.
5. Parar a transmissão também interrompe a captura do áudio. Para trocar fontes ou volumes de envio, reabra o compartilhamento e aplique as opções (reinicia a captura).

Áudio desligado por padrão. Nenhuma captura de microfone ou áudio geral do PC foi adicionada. Uma janela de prévia local sem destinatário não captura áudio. Selecionar navegador inclui suas abas e subprocessos: não há isolamento por aba. Aplicativos independentes com o mesmo nome continuam separados; processos filhos do mesmo aplicativo são agrupados. Seleções de árvores de processos sobrepostas são recusadas para evitar duplicação.

A captura por aplicativo usa a API de loopback de processos do Windows (build 20348 ou posterior; Windows 11 recomendado), PCM estéreo de 48 kHz e Opus de 96 kbps. Um pacote é codificado uma vez e encaminhado aos espectadores por WebRTC. O serviço da Cloudflare continua recebendo apenas presença e sinalização. As limitações de conexão direta/ausência de TURN continuam valendo.

Cada live recebida tem sua própria fila limitada e decodificador. Há uma saída de áudio compartilhada, volume independente do volume do Windows, descarte de áudio antigo e bloqueio de pacotes após remover uma transmissão. Nenhuma fonte fechada ou indisponível é substituída automaticamente pelo áudio geral. Se o aplicativo reiniciar, atualize a lista e selecione sua nova instância.

## Aplicações e telas

As miniaturas de janelas são fornecidas pelo DWM. A captura de uma janela usa Windows Graphics Capture, mantém a proporção e não recorta o desktop. Janelas minimizadas podem parar de produzir imagens; conteúdo protegido pode aparecer preto. A captura de monitor mantém o caminho GDI já existente. Não há troca automática de janela para monitor em caso de erro.

A captura de janela passou no teste real com duas janelas sintéticas: somente a janela vermelha escolhida apareceu, mesmo sob outra janela azul; redimensionamento e fechamento também passaram. A sessão restrita de comandos negou inicialmente a API, por isso esse teste foi concluído por um executável de verificação aberto no contexto normal do Windows. Não houve captura do desktop real nesses testes.

## Verificação nesta máquina

- Compilação Release com zero avisos e erros.
- Testes C# de vídeo local H.264, pausa/retomada, CPU/GPU, buffers e cadência passaram.
- Opus estéreo, volumes, silêncio, recepção e interrupção do envio passaram.
- Captura real de dois processos sintéticos com tons de 730 e 1470 Hz: apenas o processo escolhido apareceu no áudio capturado. Não foram gravados áudio de Discord, conversas ou microfone.
- Captura WGC real de janela sob oclusão, após redimensionamento e ao fechar: passou.
- Sala local com três participantes: seis direções de H.264 e seis de Opus, permanência após saída do criador, avatares e substituição da presença na reconexão passaram.
- Testes WPF dos controles: etapas fonte/qualidade, áudio inicialmente desligado, master, retorno, slider recebido e mute/restaurar passaram. Revisão visual da seleção de fontes, mixer e barra de volume concluída em janela de teste separada.
- Qualidade subjetiva, sincronismo em jogos, troca de dispositivo físico e internet entre casas ainda exigem teste real. Todos os testes de áudio/rede descritos acima usaram processos no mesmo PC.

Não houve publicação de nova Release no GitHub, commit, push ou alteração no Worker nesta etapa.

