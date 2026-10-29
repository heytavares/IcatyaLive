# Abrir o projeto com dois cliques

O `iCatya.exe` da raiz é um iniciador pequeno com o ícone do app. Abre a versão local mais recente e completa em `app/<versão>/Icatya.exe`, mantendo runtime, bibliotecas e arquivos internos dentro de `app/`. Não precisa executar um `.cmd` nem abrir um terminal.

Mantenha esse EXE na raiz, junto de `app/`. Para abrir de outro lugar, crie um atalho do Windows para ele. Não envie apenas esse iniciador a um amigo: os pacotes para distribuição continuam sendo o instalador ou o ZIP portátil.

O iniciador pode ser recriado com `scripts/Gerar-Iniciador-Local.ps1`, usando o compilador do .NET Framework do Windows. O executável gerado e a pasta `app/` são locais e ignorados pelo Git. O código do iniciador permanece em `scripts/Iniciador-Local.cs`.

O atalho externo `Atualizar Icatya.cmd` é opcional: removê-lo não afeta o aplicativo nem o botão de atualização das configurações. Já `app/<versão>/Atualizar-Icatya.ps1` é usado pelo botão para instalar as atualizações; mantenha esse arquivo interno e as demais dependências do pacote.

Em 29/10/2026 foi gerado o pacote local completo da 1.0.0 em `app/1.0.0`, com runtime incluído, bibliotecas de vídeo, licenças, manifesto de versão e script interno do atualizador. Nenhum instalador, ZIP ou Release foi gerado nesta etapa. O iniciador foi conferido sem abrir uma segunda instância do app.
