# iCatya 0.6.3 — perfil, avatares e área de vídeo

- Nome exibido atualizado para **iCatya** no título, cabeçalho, configurações e identificação do produto. Executável, nomes internos, convites e caminhos de preferências mantêm compatibilidade com as versões anteriores.
- Removida a faixa de instruções acima da área de vídeo. A transmissão em foco ocupa também o espaço dessa faixa.
- **Voltar à grade** fica em sobreposição no canto superior esquerdo do vídeo, visível junto dos controles ao passar o mouse. Esc continua disponível.
- Os 10 PNGs fornecidos foram incorporados como recursos do aplicativo: urso, gato, cachorro, dragão, gorila, suricato, panda, pinguim, tubarão e tigre. Não há busca externa nem upload de fotos.
- Engrenagem abre configurações com o seletor de avatar. A seleção fica em `%LOCALAPPDATA%\Icatya\profile.json`, separada de nick, sala e identificador de conexão.
- Avatar aparece ao lado do nick na lista, nos cartões de participantes e no cartão de perfil local. A seleção é enviada por identificador à sala; arquivos de imagem não passam pelo serviço.
- Perfil local em painel arredondado sobreposto à parte inferior da lateral, com avatar, nick, estado da conexão e engrenagem. O painel acompanha a largura da lateral e recolhe junto com ela. Com a lateral oculta, a engrenagem do cabeçalho continua disponível.
- Clientes antigos continuam conectando, mas precisam atualizar para exibir avatares e escolher seu próprio avatar. Não foi adicionado áudio, câmera ou login.

## Verificação

Compilação Release e publicação Windows x64 concluídas. Testes de preferências passaram para seleção salva, valor inválido e arquivo corrompido. Testes do Worker passaram para atualização do avatar e leitura por um participante que entra depois, além da regressão de salas/reconexão.

Testes C# locais e pelo serviço público passaram: avatar de entrada, mudança compartilhada entre participantes sem reconectar vídeo, três transmissões H.264, saída/reentrada e substituição de presença. As instâncias rodaram no mesmo PC.

Interface conferida em perfil de teste separado: marca iCatya, imagens circulares, painel de perfil sobreposto e cartões com avatar. Não houve captura da tela real no teste de salas.

Serviço `icatya-rooms` atualizado para a versão Cloudflare `dfdd2792-1c26-4e30-8e4e-b010251b473e`. O projeto de RPG não foi alterado.
