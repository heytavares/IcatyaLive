# Requisito de áudio por aplicativo

Pedido do usuário em 28/10/2026: selecionar quais áudios entram na live e evitar vazamento do Discord e de outros aplicativos.

## Comportamento esperado

- Sem áudio por padrão, até o usuário selecionar explicitamente a fonte.
- Seletor com aplicativos identificados por nome; seleção positiva das fontes permitidas.
- Exemplo: apenas o jogo ou apenas o navegador, mantendo o Discord fora da transmissão.
- Microfone separado e desligado por padrão.
- Indicação visível das fontes de áudio ativas e medidor para conferência antes de transmitir.
- Se a captura por aplicativo não funcionar no Windows/dispositivo, manter o envio sem áudio e explicar o motivo.
- Se o aplicativo fechar, parar sua fonte de áudio; não substituir automaticamente pelo áudio geral do computador.
- Validar separadamente jogo, navegador, Discord, notificações, mudança de dispositivo, reabertura do aplicativo e pausa/retomada.
- Escolher a API de captura compatível com a versão real do Windows antes de implementar. Não presumir que excluir um processo equivale a capturar somente os processos selecionados.

## Estado

Requisito registrado. Nenhuma captura de áudio está implementada na versão 0.3.
