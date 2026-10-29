# iCatya 1.0.0

Número da versão definido pelo autor em 29/10/2026. Consolida o trabalho desenvolvido como 0.7.3: nova interface, fechamento individual de lives, melhorias na apresentação de vídeo, cursor, seletor de salas salvas, nomes compartilhados, nova logo e ajuda pelo Discord fvckjax.

60 fps continuam identificados como experimentais. A conexão direta depende das redes entre os participantes; o app não inclui retransmissor TURN.

As notas e verificações anteriores permanecem em [NOTAS-0.7.3.md](NOTAS-0.7.3.md) e nos documentos vinculados. A mudança de número não representa uma nova validação em PCs/redes dos usuários.

Nesta etapa foram atualizados os fontes, o texto da versão, os diagnósticos e o iniciador local. O iniciador abre os fontes atuais com o SDK .NET 10, sem preferir um pacote antigo. O envio ao GitHub mostra a versão e a pasta antes de criar um commit.

Ainda não foram gerados ou publicados instalador, ZIP, tag ou Release 1.0.0. Enviar os fontes ao GitHub não altera a versão oferecida pelo atualizador. Antes da distribuição, conferir os pacotes e a atualização de uma instalação antiga, preservando o perfil.

## Ajustes finais da live

- Volume e selo AO VIVO sobre o vídeo aparecem com o movimento do mouse na área das transmissões. Somem após dois segundos de inatividade ou ao sair da área. Um único temporizador atende a interface e para quando fica ocioso; o controle de volume permanece acessível enquanto é arrastado ou usado pelo teclado.
- A borda da transmissão não muda para roxo ao passar o mouse. Clicar na imagem focada volta à grade; clicar numa imagem da grade a coloca em foco.
- Fechar uma live no X interrompe somente sua recepção local, mantendo um cartão com avatar do app, nick e aviso “está em live”. O cartão inteiro permite reabrir, também com Enter/Espaço quando selecionado pelo teclado. O selo AO VIVO da lista de participantes permanece enquanto a pessoa transmite.
- Conferência WPF fora da tela: inatividade/saída do mouse, clique foco/grade, cartão reabrindo e selo preservado, mantendo a assinatura da outra live. Capturas de conferência em `work/ui-new-design/`, fora do Git. Sem controle do mouse do usuário.
