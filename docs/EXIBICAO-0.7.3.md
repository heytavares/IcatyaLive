# Correção da apresentação de vídeo — 0.7.3 em desenvolvimento

O usuário relatou travamento com 44,3 fps processados e 16,7 fps exibidos na prévia local 1080p. O problema foi reproduzido sem captura e sem rede, usando quadros sintéticos no Windows do usuário.

## Causa reproduzida e correção

O apresentador usava uma única `WriteableBitmap` BGR24 e `TryLock` com espera zero. Enquanto o compositor mantinha essa superfície ocupada, a aplicação pulava a atualização. No teste anterior, cerca de metade dos ciclos de composição não conseguia atualizar a imagem.

- Duas superfícies BGR32, alocadas de forma preguiçosa, permitem tentar uma alternativa sem bloquear o thread da interface. Continuam limitadas a duas; se ambas estiverem ocupadas, a fila guarda somente o quadro mais recente.
- A conversão BGR24 → BGR32 usa SSSE3 quando disponível e fallback escalar, com leitura limitada à linha de origem e respeito ao stride. O buffer da captura/codec e o vídeo enviado continuam BGR24/H.264; não há mudança de resolução, bitrate, codec ou captura.
- Miniaturas de lives no modo foco usam superfícies de até 320 pixels de largura e cadência de até 12 fps. Voltar a focar restaura a resolução original e a cadência normal. Miniaturas recolhidas não são atualizadas. A imagem principal continua em resolução completa.
- O redimensionamento visual usa o caminho de interpolação rápida do WPF. A Microsoft descreve essa opção para reduzir o custo de imagens em movimento em sua [documentação de desempenho](https://learn.microsoft.com/dotnet/desktop/wpf/advanced/optimizing-performance-2d-graphics-and-imaging).
- A interface atualiza os detalhes de áudio no máximo dez vezes por segundo. O contador passa a acompanhar a live em foco, em vez de medir a própria prévia quando ela está reduzida a uma miniatura. Há indicação de FPS recebidos/exibidos também quando o usuário apenas assiste na sala.
- O diagnóstico inclui capacidade gráfica, modo de renderização, ciclos/custo da composição, superfície usada, bloqueios evitados e custo de upload por live.

Os contadores de exibição medem quadros entregues ao WPF, não a varredura física do monitor. Não foram introduzidos timers para inflar a contagem sem composição real.

## Comparação sintética no mesmo PC

`tests/Icatya.PresentationProbe` abre uma janela de teste, gera imagens sintéticas a 60 fps, aquece por 1,2 segundo e mede seis segundos por cenário. Não captura tela, não entra em salas e fecha automaticamente.

| Cenário | Antes | Depois | Tentativas sem superfície livre, antes/depois |
|---|---:|---:|---:|
| 720p, imagem principal | 15,2 fps | 29,5 fps | 90 / 0 |
| 1080p, imagem principal | 14,5 fps | 30,0 fps | 86 / 0 |
| 1080p, principal + 3 miniaturas | 16,5 fps | 30,0 fps | 399 / 0 |

Os ciclos de composição dessa janela ficaram perto de 30/s; por isso esse teste não comprova 60 fps. A carga do PC e as outras aplicações permaneceram livres para variar. Registros completos em `work/fps-before/.../presentation-results.json` e `work/fps-after/.../presentation-results.json`, fora do Git.

## Conferência no aplicativo real

A cópia local em `work/new-ui/Release/net10.0-windows10.0.19041.0` foi atualizada com hashes conferidos, após o aplicativo anterior fechar. Os dez arquivos antigos do app/testes foram preservados em `../Backups/Icatya-0.7.3-executavel-antes-fps-20261029-141057`. Captura/codec/apresentador também receberam um backup de fontes antes das alterações.

O app voltou à sala salva e o contador da live recebida 1920×1080 passou a mostrar aproximadamente 51–56 fps. Essa observação ocorreu na grade, com o compartilhamento local parado após o reinício; não é uma comparação de captura/encode antes e depois. Ainda é necessário retomar a transmissão do usuário para confirmar o comportamento completo sob essa carga e medir estabilidade prolongada. Não houve autorização automatizada de firewall: o usuário respondeu pessoalmente ao aviso do Windows.

Depois o usuário retomou o compartilhamento local. Uma nova leitura com imagem e acessibilidade mostrou 48,8 fps processados / 45,8 fps exibidos; a imagem do mesmo instante mostrava 41,6 / 39,6 fps, pois o contador varia entre consultas. O próprio usuário enviou posteriormente uma captura com 46,6 / 46,6 fps. Isso confirma melhora também na própria prévia sob transmissão, mas não comprova 60 fps estáveis nem estabilidade prolongada. A otimização adicional de 60 fps foi adiada explicitamente; a opção permanece experimental.

## Verificação

- Release compilada sem erros/avisos.
- `--presentation-checks`: pixels e cores preservados, alpha, padding de origem/destino, larguras pequenas/ímpares, guardas de memória, SIMD versus fallback, resolução de miniaturas e restauração ao focar, reutilização de bitmap e cadência/cancelamento.
- `--layout-headless`: fluxo de foco/grade, fechar/reabrir, miniaturas, configurações e tamanhos mínimo/amplo, sem janela ou captura.
- Comparação com o aplicativo de teste visível, contendo apenas imagens geradas.

Não foram gerados instalador, ZIP ou Release. Os pacotes da 0.7.2 continuam inalterados. A biblioteca, licenças e dependências nativas não mudaram.
