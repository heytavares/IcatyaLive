# Verificação da 0.6.0 — 28/10/2026

- Compilação Release e publicação Windows x64 concluídas.
- Worker `icatya-rooms` publicado na conta autorizada, separado do RPG. Versão Cloudflare: `73cdf1dd-b7c2-4a23-a02f-db97d384e9fa`.
- Endpoint `/health` retornou serviço `icatya-rooms`, protocolo 1.
- Testes do Worker passaram: presença, roteamento, remetente não falsificável pelo cliente, isolamento entre salas, saída/reentrada, resposta automática de conexão, JSON inválido, limite de 12 conexões e mensagem acima do tamanho permitido.
- Testes C# locais passaram: convite e nick, preferências ausentes/corrompidas, três participantes, seis direções de vídeo, três transmissões simultâneas, saída do criador, continuação do vídeo, reentrada com convite permanente e pausa.
- O mesmo teste de três participantes passou usando o serviço público na Cloudflare e descoberta STUN. As três instâncias rodaram no mesmo computador: isso valida o fluxo online de sinalização e o vídeo local, sem comprovar travessia de redes residenciais distintas.
- A suíte anterior de vídeo passou: captura/apresentação, convites manuais, quatro ciclos de conexão H.264, GPU, CPU, pausa/retomada, descarte e STUN.
- Inspeção visual: formulário inicial, tela principal, fonte, títulos, engrenagem, configurações e dois vídeos sintéticos com nicks/LIVE. A distribuição automática posterior escolhe linhas/colunas conforme a área disponível. Nenhuma tela real foi capturada nos testes de salas.
- Pacote verificado pelo validador do próprio atualizador: extração, manifesto 0.6.0, hash SHA256 e URL pública. ZIP sem fontes, PDB, preferências pessoais, credenciais ou dependências de desenvolvimento do servidor.
- Iniciadores apontam para 0.6.0. Pacote, aplicativo e fontes da 0.5.3 foram preservados em Backups.
- Verificação do envio ao GitHub passou. Nenhum commit, push ou Release do GitHub foi criado nesta etapa.

Ainda é necessário testar com os amigos em casas diferentes. Não há TURN nem áudio. Os testes simultâneos usaram vídeo sintético 720p; não são um ensaio de dez usuários ou de 1080p60 em jogos.
