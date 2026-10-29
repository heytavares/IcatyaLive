# Estudo histórico anterior à 0.6.0

A implementação atual escolheu Cloudflare para sinalização. Consulte SALAS-0.6.0.md. O texto abaixo registra a avaliação anterior.

# Salas — alternativas em avaliação

Requisitos do grupo: convite único e permanente (não precisa ter seis letras), nick simples, sem login, preferências salvas, múltiplos transmissores e espectadores simultâneos. A saída de qualquer participante não deve encerrar a sala para os demais. Prioridade: evitar serviço contratado ou infraestrutura externa quando possível.

## Caminho sem hospedagem externa

Os próprios aplicativos podem compartilhar o estado da sala e manter conexões entre os participantes. Não é obrigatório que todas as telas dependam de um anfitrião único. Um convite permanente pode identificar o grupo, e cada cliente pode guardar os últimos endereços dos amigos conhecidos. Um novo participante precisa alcançar pelo menos um membro online para descobrir os demais.

O limite está nessa descoberta inicial: um identificador estável não informa o endereço atual dos PCs. IPv6 acessível ou mapeamento de porta podem ajudar em certas redes; CGNAT, firewall e endereços que mudam podem impedir a entrada. Se nenhum endereço conhecido estiver alcançável, um código fixo sozinho não resolve. Isso exige diagnóstico entre redes reais antes de assumir viabilidade. Não configurar roteadores ou abrir portas sem discutir com o usuário.

## Caminho com ponto de encontro gratuito

Um serviço pequeno pode trocar apenas presença e dados de conexão, mantendo as telas entre os PCs. Workers com Durable Objects SQLite da Cloudflare são uma alternativa a avaliar: disponíveis no plano Free, sujeitos a limites; a documentação recomenda WebSocket Hibernation para conexões ociosas. Isso ainda é um serviço externo e exige configuração inicial, portanto não atende literalmente a ausência de serviço. Não foi implantado nem escolhido.

O ponto de encontro também não substitui TURN: quando a rede não permite vídeo direto, retransmissão é uma questão separada, com custo e banda próprios.

## Implementação a discutir

Separar a gestão da sala da sinalização permitirá comparar as duas opções. Usar convite aleatório longo, salvar identidade do grupo e nick localmente, manter os participantes independentes de um líder permanente e codificar uma vez por transmissor antes de enviar para os espectadores. Em uma malha direta, cada espectador adicional consome upload do transmissor; não presumir desempenho igual ao teste de dois PCs.

Fontes primárias consultadas em 28/10/2026:

- https://webrtc.org/getting-started/peer-connections
- https://developer.mozilla.org/en-US/docs/Web/API/WebRTC_API/Signaling_and_video_calling
- https://developers.cloudflare.com/durable-objects/platform/pricing/

Documento de estudo, sem mudança na conexão atual. A execução autorizada neste momento é somente o layout.
