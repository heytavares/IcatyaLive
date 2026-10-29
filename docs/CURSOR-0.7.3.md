# Cursor na transmissão e indicação experimental — 0.7.3

A captura de monitor passa a desenhar o cursor visível do Windows antes de entregar o quadro à codificação. A captura de aplicativo já tinha `GraphicsCaptureSession.IsCursorCaptureEnabled = true`; foi mantida, sem desenhar um segundo cursor sobre ela.

`GdiCursorOverlay` consulta `GetCursorInfo`, obtém tamanho e ponto ativo do desenho por `GetIconInfo` e usa `DrawIconEx` no DIB reutilizado da captura. A posição acompanha o monitor selecionado, incluindo coordenadas negativas, escala de 1080p para 720p, cursores com ponto ativo centralizado e barras de proporção. O recorte evita desenhar sobre as barras pretas. O cursor não aparece se o Windows o informa como oculto/suprimido ou se ele está fora do monitor compartilhado.

Os metadados são reaproveitados enquanto o handle do cursor não muda. Os bitmaps temporários criados por `GetIconInfo` são liberados com `DeleteObject`; o handle compartilhado do cursor do sistema não é destruído. Não são criados overlays, janelas ou gráficos por frame. As APIs e o gerenciamento de recursos seguem a documentação da Microsoft: [GetCursorInfo](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getcursorinfo), [GetIconInfo](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-geticoninfo) e [DrawIconEx](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-drawiconex).

A opção de FPS agora mostra **60 fps — experimental** no próprio seletor, além do aviso que já existia na janela. Os nomes dos perfis 720p/1080p também indicam experimental. Os valores de FPS e bitrate não foram alterados; 30 fps continua como padrão. Novas otimizações para atingir 60 fps estáveis foram adiadas a pedido do usuário.

## Verificação e disponibilidade

- Compilação Release sem avisos/erros.
- `--cursor-checks`: projeção com monitor em coordenadas negativas, ponto ativo, escala e barras; cursor nativo do Windows desenhado em bitmap de teste, recorte correto, cache e ausência de crescimento dos objetos GDI em mudanças repetidas de forma. Não captura o desktop nem move o mouse.
- `--presentation-checks` e `--layout-headless` passaram com as mudanças anteriores de framerate/interface.

Cópia local para testar: `work/cursor-preview/Release/net10.0-windows10.0.19041.0/Icatya.exe`. Inclui também a correção de apresentação de vídeo. Não substitui os arquivos da instância que está transmitindo: fechar a versão anterior antes de abrir essa cópia evita duas janelas competindo pela mesma presença na sala.

Não foi gerado instalador, ZIP ou Release no GitHub. Fontes anteriores foram preservados em `../Backups/Icatya-0.7.3-antes-cursor-20261029-142223`.
