# Icatya 0.6.1 — reconexão e foco de telas

## Reconexão

Cada instalação guarda um identificador aleatório em `%LOCALAPPDATA%\Icatya\client-id.txt`, separado do convite e do nick. Ele é enviado somente ao serviço de salas e não aparece nas listas dos participantes. Uma nova conexão dessa instalação substitui a anterior na mesma sala. Duas pessoas diferentes podem continuar usando o mesmo nick.

Se uma janela for substituída por outra da mesma instalação, ela informa o motivo e para de tentar reconectar automaticamente, evitando alternância infinita entre as duas. A substituição não usa nick como identidade.

O app confirma a resposta do serviço a cada 30 segundos, com prazo de 10 segundos. Se a conexão parar de responder, refaz a conexão. O serviço remove presenças sem atividade há mais de 90 segundos quando processa entradas ou mensagens. Em uma sala com clientes novos ativos, a limpeza ocorre nas verificações periódicas; o retorno da mesma instalação substitui imediatamente o registro antigo.

O servidor preserva compatibilidade com o ping da 0.6.0, usando o timestamp da resposta automática da Cloudflare. Porém, essa versão antiga não envia identificador da instalação: **atualizar ambos os PCs para 0.6.1** é necessário para a substituição imediata. Presenças antigas também podem desaparecer pela expiração.

## Layout

Somente os cartões de participantes e telas foram inspirados nas referências; não foram adicionados chat nem menus de servidores/canais do Discord. A lateral própria do Icatya continua ajustável e ocultável pelo botão existente.

- Participantes aparecem como cartões coloridos com inicial e nome. Não há upload de fotos, câmera ou login.
- Transmissões aparecem como cartões de vídeo com nome sobreposto e selo LIVE.
- A grade mantém proporção 16:9 e centraliza a última linha.
- Clique em uma transmissão para focar: a tela ocupa a área maior e os demais cartões ficam em miniaturas abaixo.
- Clique na miniatura de outra transmissão para trocar o foco.
- Use **Voltar à grade** ou **Esc** para sair do foco. Se a transmissão focada parar ou o participante sair, volta à grade automaticamente.
- O mesmo cartão e bitmap são movidos entre grade, foco e miniaturas; não é criado um segundo decodificador para o foco.

## Verificação

Testes do Worker passaram, incluindo reconexão com socket antigo ainda aberto, pessoas diferentes com o mesmo nick, limite da sala, remetente, roteamento e expiração de uma presença silenciosa após 90 segundos enquanto outro cliente continua ativo.

Testes C# locais e pelo Worker público passaram: três participantes, seis direções de vídeo, saída/reentrada, pausa, substituição da presença anterior e janela substituída sem novas tentativas automáticas. Estes testes online usam instâncias no mesmo PC e não simulam a desconexão do provedor do amigo.

Inspeção da interface confirmou a grade com vídeos sintéticos e cartões de três participantes, e uma transmissão ampliada com miniaturas. Nenhuma tela real foi capturada no teste de layout.

Serviço atualizado: `icatya-rooms`, versão `610ae9f6-4e85-41f0-b1e7-c13dbc05c5ab`. O Worker do RPG não foi alterado.

Referência da API de presença compatível com hibernação: https://developers.cloudflare.com/durable-objects/api/state/#getwebsocketautoresponsetimestamp
