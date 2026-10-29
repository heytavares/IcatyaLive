> Este pacote agora inclui a revisão **video-fix.1**, mantida na 0.7.1. Veja [a correção de imagem embaralhada](CORRECAO-VIDEO-0.7.1.md) e a orientação de instalação manual para quem já usa a 0.7.1.

# iCatya 0.7.1 — exibição e atualizador

## Framerate

- A apresentação de vídeo acompanha o evento de composição do WPF, no lugar de dois temporizadores independentes de 16 ms. Isso evita agendar a cópia da imagem em momentos sem relação com o desenho da janela.
- Se a imagem estiver ocupada pelo desenho, o quadro permanece disponível para a próxima tentativa. Um quadro mais novo sempre tem preferência; não há fila de vídeo acumulando atraso.
- Lives cujos cartões foram ocultados, como miniaturas recolhidas no modo de foco, deixam de copiar imagens para bitmaps invisíveis. O recebimento permanece disponível para voltar ao vídeo rapidamente.
- Removidas pequenas alocações repetidas na conversão de cada quadro H.264.
- Copiar diagnóstico agora inclui médias/máximas de compressão e envio compartilhados da sala, quadros apresentados e tentativas com a apresentação ocupada.

### Medições sem acessar a tela do usuário

Teste sintético de blocos em movimento, três segundos por perfil, conexão WebRTC local, codificação por CPU/libopenh264. O processo do benchmark usou prioridade abaixo do normal.

| Perfil | Envio | Recepção | Compressão média | Decodificação média |
| --- | ---: | ---: | ---: | ---: |
| 720p30 | 30,0 fps | 30,0 fps | 3,71 ms | 1,06 ms |
| 720p60 | 60,0 fps | 60,0 fps | 3,62 ms | 0,96 ms |
| 1080p30 | 30,0 fps | 30,0 fps | 8,40 ms | 2,27 ms |
| 1080p60 | 60,0 fps | 60,0 fps | 8,00 ms | 2,08 ms |

Esses resultados verificam a codificação, o envio e a decodificação do conteúdo sintético. **Não comprovam 60 fps numa live real**: não incluem captura de monitor/janela, apresentação visual WPF, jogos ou internet entre casas. As alternativas de GPU não estavam disponíveis nesse teste; a seleção automática usou CPU. A confirmação visual foi adiada a pedido do usuário. Não foram abertas janelas, capturada a área de trabalho ou usados controle de tela e mouse nesta etapa.

## Atualizador

Na engrenagem, clique em **Verificar atualizações**. A consulta ocorre dentro do app e informa se ele está atualizado, se o GitHub está indisponível ou se a Release está sem o pacote/verificação necessários. Quando encontra uma versão superior válida, o botão passa a **Instalar <versão>**. Esse segundo clique inicia a atualização; o app será fechado e reaberto ao instalar. O download e a preparação acontecem antes do fechamento.

A instalação usa o script que acompanha o pacote, em processo sem janela de comandos. Verifica SHA256, origem, caminhos e versão do ZIP. Confere se a Release continua sendo a que foi oferecida e impede duas instalações simultâneas na mesma pasta. Se houver falha enquanto a janela original continuar aberta, o erro aparece nas configurações e o botão permite verificar novamente.

O backup de arquivos substituídos agora fica em `%LOCALAPPDATA%\Icatya\UpdateBackups`, preservado mesmo após sucesso. Nick, sala, avatar e preferências continuam fora da pasta do programa. Quando ICATYA_DATA_DIRECTORY estiver definido para testes, o backup respeita essa pasta isolada. O comando Atualizar Icatya.cmd continua disponível como alternativa.

## Verificações

- Compilação Release sem avisos nem erros.
- Testes de propriedade e substituição dos quadros, manutenção do quadro após apresentação ocupada e descarte após encerrar passaram.
- Testes C# de versão nova, igualdade, downgrade, pacote ausente, origem incorreta e SHA256 ausente passaram.
- Consulta real identificou a Release pública v0.7.0 e seu ZIP de 136.579.478 bytes, com digest SHA256. Nenhum pacote foi instalado nessa consulta.
- O evento do botão foi acionado em instância WPF sem mostrar a janela; consulta assíncrona, desabilitação durante a consulta e mensagem inline passaram.
- Instalação completa com Release/pacote simulados passou no PowerShell 7 e no Windows PowerShell 5.1: download simulado, hash, extração, substituição, manifesto e backup persistente. Fechamento e reabertura foram simulados, sem interferir no programa do usuário.
- Regressões de ZIP inválido, arquivo ocupado, restauração parcial, vídeo H.264 local, pausa/retomada, buffers, apresentação e cadência passaram.

## Publicar

Para disponibilizar esta versão aos amigos, publique uma Release estável **v0.7.1** e anexe **Icatya-0.7.1-Windows-x64.zip**, exatamente com esse nome. Não substitua o ZIP de uma versão anterior. Enviar apenas os fontes não publica atualização. A 0.7.0 pode instalar esse pacote pelo atualizador antigo; o botão novo fica disponível na 0.7.1.

Nenhuma Release, commit, push ou alteração na Cloudflare foi realizada nesta etapa.
