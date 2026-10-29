# Icatya 0.6.0 — salas por convite permanente

## Uso

Na primeira abertura, escolha um nick e cole o convite recebido, ou use **Criar uma nova sala**. Depois de entrar, **Copiar convite da sala** copia o único texto que os amigos precisam usar. Não existe troca manual de resposta nas salas.

O convite começa com `ICATYA-` e contém 128 bits aleatórios. Ele identifica o grupo e funciona como uma chave: qualquer pessoa que o receber poderá entrar. Não há conta, senha, dono permanente ou reserva de nick. Os nicks identificam visualmente os participantes, sem verificar identidade.

Convite e nick ficam em `%LOCALAPPDATA%\Icatya\room.json`, separados das preferências da interface. Ao abrir novamente, o app tenta entrar na última sala automaticamente. A captura só começa por ação do usuário. Sair da sala interrompe o compartilhamento neste PC, sem expulsar os demais. O convite permanece válido mesmo depois que todos saem.

## Serviço

`src/Icatya.Rooms` é um Worker separado chamado **icatya-rooms**, com Durable Objects SQLite e WebSocket Hibernation. Não altera o Worker do RPG. O serviço troca presença e os dados de conexão; não recebe o vídeo e não grava telas. Mensagens de sinalização e URLs de convites não são registradas pelo código; a observabilidade do Worker está desabilitada na configuração.

O limite inicial é **12 conexões por sala**. As cotas gratuitas da Cloudflare continuam aplicáveis à conta e também incluem outros projetos. Hibernação evita manter uma sala ociosa consumindo duração de execução. Nenhum plano pago é ativado por este projeto.

O serviço foi publicado em 28/10/2026 em https://icatya-rooms.webshell222.workers.dev após autorização na conta. O arquivo público `src/Icatya/rooms-service.json` contém somente essa URL HTTPS. As credenciais do Wrangler ficam em `work/cloudflare-config`, fora do Git e do pacote do aplicativo. Para republicar neste PC, definir `XDG_CONFIG_HOME` para essa pasta e executar Wrangler no diretório do servidor.

## Vídeo e limites

Cada participante pode transmitir e receber ao mesmo tempo. O app comprime sua captura uma vez e envia o mesmo vídeo aos espectadores por conexões WebRTC separadas. As telas ativas aparecem em uma grade que acompanha a área disponível.

O upload aumenta com o número de espectadores. Por exemplo, uma transmissão de 4 Mbps para nove amigos pode exigir cerca de 36 Mbps de upload, mais o tráfego adicional do protocolo. Receber várias telas também exige mais CPU/GPU e memória. O limite de 12 é de participantes; não significa que qualquer PC consegue decodificar 11 telas 1080p60.

O serviço de salas não substitui TURN. Ainda não há retransmissor de vídeo: algumas redes restritivas podem entrar na sala e mesmo assim não conseguir transmitir entre si. Em falha de uma conexão direta, o app informa o problema; sair e entrar refaz as conexões. A conexão ao serviço de salas tem tentativa automática de reconexão.

## Desenvolvimento e verificação

Com Node.js e npm disponíveis, em `src/Icatya.Rooms`: `npm ci`, `npm test` e `npm run dev`. O arquivo de lock fixa as dependências. `node test/local-server.js` inicia o servidor local de integração em 127.0.0.1:8787. As dependências e dados locais ficam fora do Git.

O teste `Icatya.Checks --rooms` exige esse servidor e valida convite/nick salvos, três participantes, seis direções de vídeo H.264 simultâneas, saída do criador, continuação das telas, reentrada pelo mesmo convite e pausa. Ele usa vídeo sintético, sem capturar a tela. `ICATYA_DATA_DIRECTORY` permite separar perfis de teste das preferências reais.

O teste do Worker valida presença, isolamento entre salas, remetente definido pelo servidor, troca de sinalização, saída/reentrada, resposta automática de conexão e rejeição de JSON inválido. Testes locais não confirmam o comportamento entre provedores de internet diferentes.

## Interface

Fonte principal Segoe UI, 14 pixels, com títulos da lateral reduzidos e alinhamento de pixels. Engrenagem preenchida de 20 pixels, centralizada em botão de 32 pixels; cinza em repouso e clara sobre fundo arredondado ao passar o mouse. Versão e atualização permanecem nas configurações. A conexão manual antiga fica ali como alternativa de diagnóstico.
