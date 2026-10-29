# iCatya 1.0.0

Número da versão definido pelo autor em 29/10/2026. Consolida o trabalho desenvolvido como 0.7.3: nova interface, fechamento individual de lives, melhorias na apresentação de vídeo, cursor, seletor de salas salvas, nomes compartilhados, nova logo e ajuda pelo Discord fvckjax.

60 fps continuam identificados como experimentais. A conexão direta depende das redes entre os participantes; o app não inclui retransmissor TURN.

As notas e verificações anteriores permanecem em [NOTAS-0.7.3.md](NOTAS-0.7.3.md) e nos documentos vinculados. A mudança de número não representa uma nova validação em PCs/redes dos usuários.

Nesta etapa foram atualizados os fontes, o texto da versão, os diagnósticos e o iniciador local. O iniciador abre os fontes atuais com o SDK .NET 10, sem preferir um pacote antigo. O envio ao GitHub mostra a versão e a pasta antes de criar um commit.

O instalador, o ZIP portátil e o pacote técnico do atualizador foram gerados e conferidos localmente em `releases/1.0.0/`. A tag e a Release ainda precisam ser publicadas pelo autor. Enviar os fontes ao GitHub não altera a versão oferecida pelo atualizador. Veja [o guia de lançamento](LANCAMENTO-1.0.0.md) e confira uma atualização de instalação antiga após publicar.

O pacote local completo está em `app/1.0.0/` e o `iCatya.exe` iniciador da raiz o abre neste PC, conforme [INICIADOR-LOCAL.md](INICIADOR-LOCAL.md). Nenhum desses diretórios precisa ser enviado ao código do GitHub.

## Ajustes finais da live

- Volume e selo AO VIVO sobre o vídeo aparecem com o movimento do mouse na área das transmissões. Somem após dois segundos de inatividade ou ao sair da área. Um único temporizador atende a interface e para quando fica ocioso; o controle de volume permanece acessível enquanto é arrastado ou usado pelo teclado.
- A borda da transmissão não muda para roxo ao passar o mouse. Clicar na imagem focada volta à grade; clicar numa imagem da grade a coloca em foco.
- Fechar uma live no X interrompe somente sua recepção local, mantendo um cartão com avatar do app, nick e aviso “está em live”. O cartão inteiro permite reabrir, também com Enter/Espaço quando selecionado pelo teclado. O selo AO VIVO da lista de participantes permanece enquanto a pessoa transmite.
- Conferência WPF fora da tela: inatividade/saída do mouse, clique foco/grade, cartão reabrindo e selo preservado, mantendo a assinatura da outra live. Capturas de conferência em `work/ui-new-design/`, fora do Git. Sem controle do mouse do usuário.
