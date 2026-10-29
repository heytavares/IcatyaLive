# 0.7.3 — recuperação de lives (em desenvolvimento)

Somente fontes e testes. Não gerar instalador, ZIP ou Release desta versão até o usuário concluir as outras mudanças solicitadas. Os pacotes existentes da 0.7.2 permanecem disponíveis e inalterados.

A versão também inclui a [nova interface e o fechamento individual de lives](UI-0.7.3.md), conforme a referência visual enviada posteriormente pelo usuário. O slogan foi removido.

Inclui também a [correção da baixa taxa de apresentação de vídeo](EXIBICAO-0.7.3.md), com comparação sintética antes/depois e conferência na cópia local do app.

O [cursor na captura de monitor e o rótulo experimental de 60 fps](CURSOR-0.7.3.md) também estão implementados. A busca de 60 fps estáveis ficou para uma etapa futura por solicitação do usuário.

Inclui o [seletor de salas salvas, renomeação compartilhada, nova logo e ajuda pelo Discord](SELETOR-SALAS-0.7.3.md). O serviço recebeu os recursos adicionais de nomes e consultas de presença; os pacotes do aplicativo continuam sem publicação.

## Problema investigado

Um participante conseguia assistir a uma live, enquanto outra permanecia em “Conectando”. O código criava cada conexão de vídeo uma única vez: a presença na sala podia continuar funcionando mesmo depois de uma direção de vídeo falhar, sem tentativa automática de recriá-la. Isso explica um caminho que mantinha a tela esperando, mas não comprova a causa de rede do episódio no PC do amigo; ainda não temos o diagnóstico daquele computador.

## Correções

- Monitor por sala, a cada dois segundos. Recria apenas a direção de vídeo afetada, preservando as demais lives, a presença e o convite.
- Uma negociação sem conclusão tem prazo de 45 segundos. Falhas explícitas têm tolerância de três segundos para quedas transitórias. Há até três novas tentativas, com espera de 1, 2 e 4 segundos. Uma conexão estável por um minuto renova esse limite para uma queda futura.
- Se o canal de vídeo conecta mas não produz imagem por 20 segundos, o receptor pode pedir uma nova conexão pelo canal de controle WebRTC existente. O protocolo da Cloudflare continua usando apenas oferta e resposta; nenhum serviço foi publicado ou alterado.
- O caminho de envio compartilhado das salas reafirma o estado “transmitindo” a cada segundo, como já ocorria no envio individual. Recupera um estado de controle inicial que não tenha chegado na ordem esperada.
- Respostas de sessões antigas são ignoradas. Respostas, pausas e falhas de um receptor substituído não devem alterar a nova conexão.
- Uma pausa transitória do controle não remove o cartão de uma live que a sala ainda informa como ativa. O recebimento de “iniciado” restaura o cartão quando necessário.
- A tela mostra a etapa de recuperação ou um erro, e o diagnóstico das configurações passa a incluir cada conexão de envio/recepção, ICE, pacotes recebidos e quadros decodificados. Não inclui o convite da sala, SDP ou endereços de rede.

Atualizar ambos os lados é necessário para usar o pedido de recuperação pelo controle. A recuperação do próprio envio funciona no cliente atualizado, inclusive quando o outro lado ainda usa uma versão anterior.

## Verificação

`Icatya.Checks --rooms`, com o serviço local de testes: três participantes, seis direções de vídeo/áudio, interrupção de uma direção, retorno de imagens H.264 sem substituir a conexão saudável, resposta antiga ignorada e pedido de recuperação pelo receptor. Também verifica entrada/saída/reentrada, avatar e ausência de presenças duplicadas.

O servidor local aceita `ICATYA_TEST_DROP_FIRST_OFFER=1` para descartar deliberadamente a primeira oferta de cada participante. A falha é aplicada somente à cópia do Worker carregada pela ferramenta local; o arquivo de produção permanece intacto.

Regressões complementares: `--video-recovery` e `--background-checks`. Nenhum desses testes abre a janela do aplicativo ou controla mouse/teclado. O teste do atualizador consulta metadados públicos, sem baixar ou instalar uma atualização.

Resultado local: compilação sem avisos/erros; integração normal e integração descartando a primeira oferta concluídas com sucesso, incluindo vídeo decodificado após a recuperação. Regressões de vídeo e consulta/botão de atualização aprovadas. Os registros ficam em `work/room-recovery-checks.log`, `work/room-recovery-dropped-offer-final.log` e `work/recovery-video-regression.log`, fora do Git. As integrações usam imagens sintéticas e não capturam a tela do usuário. O teste inicial sem a permissão de rede falhou antes de conectar ao serviço local; foi repetido com acesso à rede permitido.

A conexão direta continua dependendo de uma rota WebRTC entre os PCs. As novas tentativas não acrescentam um retransmissor TURN. Se a falha persistir em uso real, copiar o diagnóstico nos dois PCs durante o problema permite distinguir ausência de rota, ausência de pacotes e falha de decodificação.
