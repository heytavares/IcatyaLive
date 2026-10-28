# MoabLive 0.2 — conexão direta em teste

## Abrir
Clique duas vezes em **Iniciar MoabLive.cmd**. Não precisa abrir o Visual Studio, editar código, instalar .NET ou compilar. O pacote inclui o runtime para Windows x64. Mantenha a pasta `app` ao lado do iniciador.

Se a versão 0.1 estiver aberta, feche aquela janela e abra pelo iniciador para carregar a 0.2. Não é necessário fechar ou descartar alterações no Visual Studio.

Se recebeu o pacote ZIP: extraia todo o conteúdo para uma pasta e abra **MoabLive.exe** dentro dela. Nesse pacote, o executável e seus arquivos de apoio ficam juntos, sem a subpasta `app`.

## Testar sozinho
1. Clique em **Testar neste PC**. Uma segunda janela será aberta como receptora.
2. Aguarde a confirmação de conexão local.
3. Na primeira janela, selecione o monitor e clique em **Transmitir tela**.
4. Confira o vídeo na segunda janela. Se capturar o monitor em que o programa está, aparecerá o efeito de espelho; isso é esperado.
5. Clique em **Parar captura**. A imagem deve desaparecer das duas janelas.
6. Você pode transmitir novamente ou clicar em **Desconectar**.

O teste usa dois participantes WebRTC reais dentro do aplicativo, com codificação VP8, transporte ICE/DTLS/SRTP e decodificação. Não simula o recebimento copiando a prévia local. Ele não confirma conectividade pela internet.

## Testar com um amigo
Ambos precisam usar a versão 0.2. Vocês podem continuar na chamada de voz do Discord.

1. Quem vai compartilhar clica em **Criar convite para transmitir**.
2. Clica em **Copiar texto** e envia o texto pelo chat privado ao amigo.
3. O amigo cola em **Convite recebido** e clica em **Assistir / gerar resposta**.
4. O amigo copia a resposta gerada e a devolve pelo chat.
5. Quem criou o convite cola a resposta em **Convite recebido** e clica em **Aplicar resposta**.
6. Quando aparecer a conexão estabelecida, quem transmite seleciona o monitor e clica em **Transmitir tela**.

Não há transmissão automática ao criar ou aceitar um convite. Os convites expiram após 20 minutos para iniciar a conexão e podem conter endereços de rede. Envie apenas à pessoa com quem quer se conectar.

Se não conectar: confirme que as duas janelas continuam abertas, que a resposta pertence ao convite atual e que o aplicativo não está bloqueado pelo firewall. Depois desconecte e crie outro convite. Não é necessário desativar o firewall.

## Qualidade desta etapa
- Perfil leve: 640 × 352, até 10 fps.
- Perfil de detalhe: 1280 × 720, até 10 fps.
- A proporção do monitor é preservada com faixas quando necessário.
- Sem áudio do computador ou microfone nesta versão.
- Sem captura de janela individual, múltiplos espectadores, adaptação automática de bitrate ou aceleração de vídeo por GPU.
- 1080p60 ainda não está implementado. O encoder VP8 em C# é usado para validar a conexão; será necessário trocar o caminho de vídeo para buscar alta qualidade com baixo consumo.

## Infraestrutura
Sem conta, LiveKit, servidor contratado, hospedagem própria ou retransmissor TURN. Na internet, STUN público (`stun.cloudflare.com:3478`) ajuda a descobrir o endereço externo. Esse serviço não retransmite a tela. O teste no mesmo PC dispensa STUN.

O vídeo passa diretamente entre os participantes. Algumas combinações de CGNAT, NAT restritivo ou firewall impedem a conexão direta; sem um retransmissor, não há garantia de funcionar em todas as redes.

## Organização e consumo
- `Models`: quadros e perfis.
- `Services/InviteCodec`: validação, compactação e expiração de convites.
- `Services/PeerSession`: conexão, codec e sinais de pausa/retomada.
- `Services/ScreenCaptureService`: captura e redução da imagem fora da interface.
- `MainWindow`: ações e apresentação da janela.

Uma captura e uma codificação por vez, bitmaps de captura reutilizados, apenas o quadro mais recente aguardando exibição e interrupção dos temporizadores de imagem quando a prévia para. A interface reutiliza o bitmap exibido, e os recursos gráficos e conexões são descartados ao encerrar.

## Verificação
Compilação Release sem avisos ou erros. Testes de integração verificam convites inválidos e expirados, rejeição de resposta de outra sessão, conexão local, vídeo enviado/recebido com pixels conferidos, os dois perfis, pausa, retomada, bloqueio de envio após parar, encerramento e descoberta STUN pública. O fluxo visual de teste local também foi exercitado com captura real da tela. Ainda falta testar dois computadores em redes diferentes e medir desempenho durante jogos.

## Desenvolvimento
Projeto: `src/MoabLive/MoabLive.csproj` (.NET 10, C#, WPF).
Verificações: `tests/MoabLive.Checks/MoabLive.Checks.csproj`.
Dependências fixadas: SIPSorcery 10.0.17 e SIPSorcery.VP8 10.0.17. Consulte os avisos de terceiros em `app/0.2/licenses`.


