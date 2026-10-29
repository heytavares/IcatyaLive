# Icatya 0.5.3 — layout de participantes

Esta versão altera a interface; salas, código fixo, nick compartilhado e múltiplas transmissões ainda não foram implementados.

- Lateral com cabeçalho de conexão, participantes, área reservada ao nick e engrenagem inferior.
- Na conexão manual atual, a lateral mostra Você e Amigo conectado quando o peer estiver conectado. Não há participantes fictícios.
- Indicadores LIVE vermelhos acompanham a captura local e os quadros recebidos. Volume posicionado no canto superior direito, desativado porque ainda não há áudio.
- Ícones vetoriais com traços claros e paleta do aplicativo. Sair, parar e LIVE mantêm o vermelho.
- Versão instalada e Verificar atualizações dentro das configurações.
- Convites, respostas, diagnóstico e Testar neste PC preservados na seção Conexão manual das configurações.
- Configurações fecham pelo X ou Esc. Se a lateral estiver oculta, uma engrenagem aparece no cabeçalho para manter o acesso.
- Lateral ajustável e ocultável, proporção do vídeo e fonte principal de 14 pixels preservadas.

O sistema de conexão continua sendo o mesmo da 0.5.2. Nenhum serviço foi criado, conectado ou contratado para salas. O texto Meu nick é apenas a área visual reservada; a entrada de nick será implementada com as salas.

Validação: publicação Release concluída sem erros. Conferidos visualmente lateral, engrenagem, versão/atualização, rolagem e acesso aos controles antigos. O teste local abriu a segunda janela, conectou e mostrou o participante remoto na lateral.

Também foi iniciada uma transmissão 720p30 pela janela de opções: imagem visível, selo LIVE local ativo e cerca de 29–30 fps processados/exibidos na amostra. O núcleo de vídeo não foi modificado nesta etapa.
