# Icatya 0.5.0 — controles e barra lateral

O painel superior de captura foi removido para aumentar a área do vídeo. Os dois botões circulares ficam centralizados abaixo da imagem: compartilhar/configurar (cinza) e parar compartilhamento (vermelho quando ativo).

O primeiro botão abre uma janela com seleção de monitor, resolução 720p/1080p e 30/60 fps. O bitrate acompanha o perfil existente: 4, 6, 7 ou 10 Mbps. Ao aplicar durante a captura, a captura é parada e reiniciada na mesma conexão com as opções novas; cancelar mantém a transmissão atual. Sem conexão, a confirmação inicia apenas uma prévia local. Os 60 fps continuam sendo uma meta experimental.

Arraste a divisória entre a barra lateral e o vídeo para escolher livremente a largura. O botão no canto superior esquerdo oculta/mostra a lateral; a imagem acompanha o espaço, mantendo a proporção. Há limites mínimos para manter o conteúdo acessível, sem tamanhos predefinidos de ajuste.

Largura e visibilidade são salvas em `%LocalAppData%/Icatya/ui.json`. Arquivos ausentes ou inválidos usam os padrões. A janela receptora do teste local não sobrescreve as preferências da janela principal. Não há alteração no protocolo de transmissão ou adição de dependências.

Validação: compilação, suíte de vídeo GPU/CPU, bitmap WPF, pausa/retomada e preferências com largura fracionária, estado oculto e recuperação de arquivo corrompido. Conferidos visualmente o arraste da divisória, o vídeo ampliado ao ocultar a lateral e a centralização dos botões.