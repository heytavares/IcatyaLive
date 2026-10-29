# Salas salvas, identidade e ajuda — 0.7.3

Versão em desenvolvimento. Nenhum instalador, ZIP ou Release do GitHub foi criado nesta etapa.

## Uso

- Clique no nome da sala ou pressione Ctrl+K para abrir **Suas salas**. Pesquise por nome, entre numa sala salva, crie uma sala ou entre com um convite.
- A sala atual fica destacada. O seletor consulta pessoas online e até três avatares das outras salas, sem entrar nelas. Sem resposta do serviço, exibe que a consulta não está disponível, sem inventar contagens.
- **Renomear sala atual** altera o nome para todos os participantes. O serviço guarda o nome mesmo quando todos saem; qualquer participante com o convite pode renomear, pois não há contas ou cargos.
- Os convites continuam iguais. Até 50 salas ficam salvas neste PC em `%LOCALAPPDATA%\Icatya\rooms.json`; a última sala continua em `room.json`, compatível com builds anteriores. A sala antiga é importada automaticamente.
- As consultas de presença ocorrem ao abrir o seletor e a cada 20 segundos, durante até dois minutos. Fechar o seletor interrompe as consultas; reabrir atualiza os resultados. A pesquisa usa os dados locais.
- Nas configurações, **Ajuda** mostra o Discord **fvckjax** e permite copiar o usuário. **Copiar diagnóstico** permanece acessível sem abrir **Avançado · conexão manual**. O teste de transmissão no próprio PC saiu da interface.
- A nova logo foi aplicada no cabeçalho e no ícone do executável, com sete tamanhos de 16 a 256 pixels. As margens foram reduzidas; o desenho ocupa 94% da largura na versão de 32 pixels, preservando a transparência.

## Serviço e compatibilidade

Somente o Worker `icatya-rooms` foi atualizado, mantendo o binding, a migração e o protocolo de sinalização existentes. Nome compartilhado e consulta de presença são adicionais; clientes antigos podem continuar conectando, mas precisam da versão nova para mostrar esses recursos. O vídeo e o áudio continuam entre os PCs.

Publicação conferida em 29/10/2026, versão do serviço `5a46e462-72e0-40e5-a101-b27903698af1`. O teste publicado usou uma sala aleatória, sem mídia nem acesso às salas dos usuários, e verificou entrada, renomeação, contagem e nome preservado após a saída.

## Verificação local

- Compilação sem erros ou avisos; testes de migração, persistência, Unicode, salas únicas e ordem de visita.
- Serviço local: nomes compartilhados, persistência após reiniciar o objeto, limites de entrada, contagens reais e nenhuma presença extra causada pelas consultas.
- Integração com três participantes e seis direções de vídeo/áudio: renomear preserva as conexões, além dos testes existentes de reconexão e fechamento individual.
- Renderização WPF fora da tela: busca e seletor, editor de nome, grade/foco, miniaturas, configurações e barra lateral. Sem controle de mouse/teclado ou gravação do perfil real.
- Sete tamanhos do ícone, transparência e ocupação; regressões de apresentação e consulta/botão de atualização, sem instalar atualização.

Prévia local compilada: `work/rooms-preview/Release/net10.0-windows10.0.19041.0/Icatya.exe`. Feche a cópia anterior antes de abrir esta, para evitar duas instâncias usando o mesmo perfil na sala.

Os fontes anteriores foram preservados em `../Backups/Icatya-0.7.3-antes-seletor-salas-20261029-145534.zip`. Os pacotes publicados da 0.7.2 não foram substituídos.
