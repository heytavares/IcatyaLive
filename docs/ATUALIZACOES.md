> Atualização: a versão 0.7.1 consulta e informa o resultado dentro do app; confira [as notas atuais](NOTAS-0.7.1.md). O texto abaixo registra a implementação inicial.

# Icatya 0.5.1 — atualizador e aparência

O desenho do ícone agora usa cerca de 94% da largura, em vez de aproximadamente 73%. A transparência e o PNG original foram preservados. A área reservada pelo Windows na barra de tarefas permanece a mesma; o que aumentou foi o desenho dentro dela.

O texto principal passou de 13 para 16 pixels. A família preferida é gg sans, se já estiver instalada, com alternativas Segoe UI Variable Text e Segoe UI do Windows. Nenhum arquivo de fonte do Discord é distribuído no pacote.

## Usar o atualizador

Abra **Atualizar Icatya.cmd** na pasta que contém Icatya.exe, ou use o botão **Atualizar** no aplicativo. Ele consulta a última Release estável pública de heytavares/IcatyaLive. Se houver uma versão superior, baixa o pacote, confere o SHA256 fornecido pelo GitHub, valida os caminhos e a versão interna e prepara a cópia antes de fechar o programa. Fecha somente instâncias abertas dessa mesma pasta; não encerra processos à força. Ao concluir, abre o Icatya atualizado.

As preferências da barra lateral ficam fora do pacote e são preservadas. Uma falha de cópia tenta restaurar os arquivos anteriores; se um arquivo ocupado impedir a restauração, o atualizador informa onde está o backup. Outros arquivos locais não são removidos. Não há consulta contínua nem serviço rodando em segundo plano.

## Publicar uma versão para os amigos

Enviar código com **Enviar para GitHub.cmd** não publica o aplicativo compilado. Para os amigos receberem uma atualização:

1. Prepare a próxima versão do pacote, com o mesmo número no programa, no arquivo version.json e no nome do ZIP.
2. Abra https://github.com/heytavares/IcatyaLive/releases/new .
3. Use uma tag como **v0.5.2**, substituindo pelo número real da próxima versão.
4. Anexe o pacote **Icatya-0.5.2-Windows-x64.zip**, substituindo pelo mesmo número. Não anexe apenas os fontes nem coloque o ZIP em uma pasta extra dentro do arquivo.
5. Publique como Release estável, sem marcar pré-lançamento. O GitHub calcula o SHA256 do arquivo enviado.
6. Quem já recebeu a 0.5.1 poderá executar o atualizador; não precisará receber a pasta novamente.

Em 28/10/2026 o repositório foi confirmado público, com nome IcatyaLive, mas a consulta da última Release respondeu 404: nenhuma versão estável estava publicada. O mecanismo local está implementado; o download e a aplicação de uma atualização real dependem dessa publicação. Nenhuma Release foi publicada por este trabalho.

## Verificações

Compilação Release do aplicativo. Testes do atualizador em Windows PowerShell: pacote válido, hash diferente, versão diferente, caminhos absolutos e travessia no ZIP, backup, aplicação e restauração após falha de cópia com arquivo ocupado. O fluxo completo pela internet ainda depende de uma Release real.
