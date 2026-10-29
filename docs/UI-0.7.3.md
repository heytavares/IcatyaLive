# Nova interface — 0.7.3 em desenvolvimento

Implementação baseada na referência enviada pelo usuário: superfícies quase pretas, lilás nas ações principais, cabeçalho sem slogan, sala em cartão, participantes com avatares, destaque do participante focado, título da transmissão e seletor Foco/Grade.

Os três controles inferiores continuam sendo câmera indisponível, compartilhar/configurar tela e parar transmissão. Receberam formas arredondadas e vetores consistentes com o restante da interface. O volume recebido continua funcional no canto superior direito da área de vídeo. A lateral continua ajustável com o mouse, recolhe junto com o perfil e preserva a largura escolhida anteriormente. Novos perfis começam com 300 pixels de largura.

O botão de reticências da sala abre a troca de sala/nick. Convidar copia o convite permanente. Versão, atualizador, escolha de avatar e detalhes de áudio/compressão continuam nas configurações. Os indicadores de FPS permanecem disponíveis no rodapé durante a transmissão.

## Fechar somente uma live

1. Focar uma transmissão recebida, pelo vídeo ou pelo participante.
2. Passar o mouse sobre ela. O X aparece ao lado da seta que oculta/mostra as miniaturas.
3. Clicar no X: fecha somente essa live para este espectador, incluindo seu áudio. Não encerra o compartilhamento do amigo nem altera a sala ou a transmissão para outras pessoas.
4. Clicar no participante marcado com **ABRIR** para reabrir e focar a live.

A própria prévia não apresenta o X: para interromper o compartilhamento local existe o botão de parar. Recolher as miniaturas continua sendo uma ação diferente de fechar uma live.

O estado local impede recriar o cartão em atualizações de presença. O receptor também para de decodificar os pacotes da live fechada e remove seu áudio da reprodução. Entre clientes atualizados, as mensagens `watch:on` / `watch:off` pelo canal WebRTC existente suspendem o envio para aquele espectador, poupando rede. A reabertura pede um quadro completo. O remetente continua atendendo os demais espectadores e o encoder compartilhado pode descansar quando ninguém está assistindo.

O estado é reaplicado se apenas aquela conexão de vídeo for recriada. A inicialização do canal de controle lida também com canais entregues pelo SIPSorcery já abertos. Clientes antigos não entendem a suspensão no remetente; nesses casos o cliente atualizado ainda fecha o cartão e interrompe a decodificação/reprodução local. Não houve alteração do serviço da Cloudflare.

## Ícones e implementação

Ícones selecionados do [Lucide](https://github.com/lucide-icons/lucide), convertidos das fontes SVG oficiais em geometrias WPF em `Themes/Lucide.xaml`. O controle `LucideIcon` reutiliza geometrias congeladas e herda a cor do botão. Não há navegador, fonte de ícones, pacote NuGet adicional nem acesso à internet para desenhá-los. A licença ISC/MIT completa está em `docs/LICENCAS/Lucide.txt` e acompanha futuras publicações pelo mecanismo de licenças já existente.

## Verificações

- Compilação Release sem avisos/erros.
- `--layout-headless`: controles WPF reais, sem abrir janela. Testa Foco/Grade, título e seleção, X/retorno pela lista, manutenção do fechamento em atualização da sala, miniaturas, 1440×900 e 1040×720, lateral/perfil recolhidos e configurações. Gera prévias com imagem sintética em `work/ui-new-design`.
- `--rooms`: três participantes com seis direções reais de vídeo/áudio WebRTC no serviço local. Confere suspensão seletiva, ausência de vídeo/áudio da live fechada, continuidade para o outro espectador, reabertura com imagem decodificada e manutenção do fechamento durante a recriação da conexão. Inclui os testes anteriores de reconexão e presença.
- Regressões de vídeo e atualização: `--video-recovery` e `--background-checks`.

Nenhum desses testes movimenta o mouse, captura o desktop ou abre a janela do aplicativo. As prévias usam participantes e imagens de teste. Não foi gerado novo instalador, ZIP de distribuição ou Release; os pacotes 0.7.2 existentes permanecem inalterados, conforme solicitado.
