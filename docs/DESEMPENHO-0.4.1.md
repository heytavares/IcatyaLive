# Icatya 0.4.1 — desempenho e estabilidade

## Alterações

- Cadência da captura por prazos absolutos medidos com Stopwatch. O temporizador de alta resolução do Windows fica restrito à captura, sem espera ocupada e sem alterar globalmente a resolução de temporizadores. Se o recurso não estiver disponível, há alternativa com temporizador padrão.
- Quando uma captura demora além do período, a cadência é reajustada sem acumular capturas atrasadas em rajada. A espera pode ser cancelada imediatamente.
- Apresentação WPF com tentativa de bloqueio sem espera: se o bitmap estiver ocupado, o quadro é pulado para manter os controles responsivos. O próximo quadro recente substitui o antigo. O bitmap continua reutilizado.
- FPS processados/recebidos e FPS enviados ao bitmap WPF aparecem separados. Eles não medem diretamente o que o monitor físico exibiu.
- Copiar diagnóstico inclui média/máximo de captura, compressão, envio, decodificação e apresentação; contadores de quadros substituídos antes de codificar e exibir. Não grava imagens nem adiciona endereços IP ou convites ao diagnóstico.
- Fechamento de janela adiado até sair do evento Closing, evitando a tentativa de fechar recursivamente uma janela ociosa.

## Medições

Na versão 0.4.0, uma execução local de quatro segundos com captura GDI + AMD AMF + WebRTC + decodificação mediu 24,1 fps no perfil 720p30. Após a correção da cadência, uma execução de oito segundos mediu 29,9 fps, recebendo 239 quadros, sem descarte antes de codificar. O tempo médio da captura ficou em 21,86 ms, codificação em 3,31 ms, envio em 0,35 ms e decodificação em 1,12 ms.

São execuções na área de trabalho deste PC, com carga que pode variar, não um teste controlado em dois computadores ou durante jogos. O teste de captura não inclui a apresentação WPF. Uma hipótese de usar buffers de 32 bits foi experimentada e descartada por não mostrar ganho consistente no conjunto dos perfis; a versão entregue mantém BGR24.

Os perfis de 60 fps continuam experimentais. O caminho GDI ainda apresentou picos de captura e não demonstrou 60 fps estáveis no percurso completo. Não houve mudança de bitrate ou redução de resolução para alcançar a meta de 30 fps.

## Verificações reproduzíveis

```powershell
dotnet build icatyalive.slnx -c Release
dotnet run --project tests/Icatya.Checks/Icatya.Checks.csproj -c Release
dotnet run --project tests/Icatya.Checks/Icatya.Checks.csproj -c Release -- --capture-benchmark
dotnet run --project tests/Icatya.Checks/Icatya.Checks.csproj -c Release -- --capture-stability
```

A suíte verifica pixels do bitmap WPF, padding de linhas, troca de resolução, reutilização, cadência e cancelamento, propriedade dos buffers, conexão local, cores H.264, pausa/retomada e codificação por GPU e CPU. O teste prolongado exercita consumidor lento, falha do consumidor e três períodos de vinte segundos em 720p30 → 1080p30 → 720p30.

## Conferência em dois PCs

Atualize as duas pontas para 0.4.1. Comece em 720p30 e mantenha a transmissão durante pelo menos dois minutos. Compare os fps processados do transmissor com os recebidos e exibidos do receptor. Depois repita em 1080p30 e teste parar/iniciar três vezes. Se cair muito, copie o diagnóstico nas duas pontas enquanto a falha estiver acontecendo. A comparação permite distinguir gargalo de captura, rede, decodificação ou apresentação. O ganho entre PCs ainda precisa dessa confirmação.
Resultado do teste prolongado: 29,9 fps recebidos em cada período de vinte segundos, em 720p30, 1080p30 e novamente 720p30; zero quadros substituídos antes de codificar e zero erros de decodificação. Uma execução anterior não concluiu a conexão local; após restabelecer a permissão de rede do ambiente, a repetição acima passou. Isso não equivale a uma validação da estabilidade em redes diferentes.

Compilação concluída sem erros. Uma restauração do NuGet apresentou o aviso NU1900 por indisponibilidade da consulta de vulnerabilidades; a publicação posterior restaurou normalmente. Nenhuma dependência foi adicionada ou atualizada nesta etapa.

Conferência visual do pacote publicado: duas janelas conectadas localmente, imagem recebida correta em 720p30. O contador do receptor mostrou 29,8 fps recebidos e 29,8 fps enviados ao bitmap WPF. Os controles de início/parada foram exercitados. Isso valida uma amostra visual local e não substitui o teste entre dois PCs.
