# Correção de vídeo na 0.7.1 (video-fix.1)

Relato: partes do texto/imagem ficavam embaralhadas na live durante mudanças rápidas da tela, embora a origem estivesse normal. A 0.7.1 inicial tratava a apresentação de quadros, mas ainda não continha esta proteção do vídeo recebido.

## Alteração

- Recepção H.264 passa a montar e validar a unidade inteira de imagem a partir dos pacotes RTP. Um quadro com fragmentos faltando, NAL incompleta, marcador inconsistente ou perda de continuidade não é encaminhado ao decodificador.
- Pequena janela de reordenação, com limite de três quadros e 60 ms de espera para lacunas. Pacotes duplicados e atrasados não recriam imagens já apresentadas. Há limites de tamanho e quantidade por quadro.
- Depois de uma perda, o receptor preserva a última imagem íntegra e aguarda uma imagem independente (IDR), em vez de continuar usando referências possivelmente danificadas. O decodificador é reiniciado nessa recuperação e imagens sinalizadas como corrompidas pelo FFmpeg são rejeitadas.
- O receptor pede uma imagem completa pelo canal de controle WebRTC já existente. Há limitação de pedidos e de geração de quadros de recuperação. O caminho funciona também com o codificador compartilhado das salas.
- Diagnóstico informa quadros incompletos descartados e pedidos de recuperação. Nenhum áudio, imagem ou sinalização adicional passa pelo Worker da Cloudflare.

Em uma conexão com perdas, a imagem pode ficar brevemente parada enquanto se recupera, em vez de exibir blocos/trechos de referências danificadas. Isso não substitui uma conexão com capacidade suficiente para o perfil escolhido.

## Testes concluídos

Sem automação da tela, mouse, captura de desktop ou abertura do app:

- Páginas e texto gerados em memória, em 720p e 1080p, codificados em H.264 por CPU e comparados por pixels após a decodificação.
- Fragmentação de imagens em RTP, reordenação com marcador antecipado, passagem pelos limites de sequência de 16 bits e timestamp de 32 bits.
- Perda de fragmento intermediário, primeiro fragmento e marcador final: quadro incompleto não foi apresentado; recuperação por IDR produziu imagem íntegra.
- Após recuperação, 17 trocas rápidas de páginas por perfil mantiveram os pixels dentro da tolerância de compressão do teste.
- Pacotes duplicados não duplicaram a apresentação; fila sem final de quadro permaneceu limitada; marcador inconsistente não ocultou lacunas.
- Pedido de imagem completa chegou ao transmissor por conexão WebRTC local real.
- Regressões de vídeo H.264 local, pausa/retomada, CPU alternativa, cadência e apresentação passaram. Sala local com três participantes manteve as seis direções de vídeo e áudio e os testes de saída/reentrada passaram.
- Compilação Release sem avisos ou erros.

O cenário específico das capturas enviadas ainda precisa ser repetido pelos usuários. A validação automatizada reproduz mudanças rápidas e falhas de entrega controladas; não usou AnyDesk nem as telas de trabalho do usuário. Os caminhos de GPU não estavam disponíveis nesses testes e precisam ser confirmados no uso real.

## Como reconhecer e instalar

A versão permanece **0.7.1**, conforme solicitado. Nas configurações aparece **Versão 0.7.1 · correção de vídeo**. A versão do arquivo é 0.7.1.1 e o manifesto identifica revision=video-fix.1.

O ZIP corrigido usa o mesmo nome **Icatya-0.7.1-Windows-x64.zip**. O pacote e os fontes anteriores foram preservados em Backups antes de substituir os arquivos locais.

**Quem já instalou a 0.7.1 anterior precisa substituir o pacote manualmente**, pois o atualizador compara o número da versão e não oferece reinstalar uma versão igual. Atualize os dois PCs para testar a recuperação completa. Não houve commit, push, publicação de Release ou alteração no serviço de salas nesta correção.
