# Migração segura de dados — Windows beta

Este procedimento é **experimental** e não substitui a homologação funcional.

## Operações disponíveis
1. **Verificar bancos**: lista bancos IndexedDB e quantidade de registros, sem alterar dados.
2. **Copiar dados locais para SQLite**: cria cópias criptografadas dos registros localStorage.
3. **Copiar IndexedDB para SQLite**: copia registros compatíveis com JSON e confere a leitura posterior. Mostra progresso sem expor informações pessoais.

As cópias não alteram o armazenamento original. Não exclua dados do WebView2, não desinstale apagando o perfil e não remova os backups existentes.

## Limitações críticas
- Os módulos de pacientes, avaliações, cobranças e PDFs **ainda não utilizam o SQLite como armazenamento principal**.
- Registros IndexedDB que contenham Blob, ArrayBuffer, Date, Map, Set, referências circulares ou outros valores não compatíveis com JSON são recusados, não convertidos silenciosamente.
- Uma migração parcialmente concluída pode ser retomada quando os registros forem idênticos. Dados antigos modificados após a cópia geram conflito e exigem reconciliação; não são sobrescritos.
- A contagem de registros antes e depois ajuda a identificar alterações concorrentes, mas não substitui uma migração transacional entre bancos.
- A proteção automática DPAPI está vinculada à conta Windows. Uma cópia do arquivo SQLite, isoladamente, não constitui backup portátil entre computadores.
- A interface de senha mestre e o mecanismo de recuperação ainda não estão homologados. Não ativar modo de senha mestre sem migração e recuperação testadas.
- A verificação final deve incluir pacientes, avaliações, histórico, imagens/anexos, cobrança, geração de PDF, backup/restauração e reinicialização do aplicativo.

## Critérios mínimos antes da versão estável
- Inventário real dos esquemas e formatos de IndexedDB.
- Exportação/migração completa e verificável, inclusive anexos e tipos binários.
- Transição explícita de cada módulo para SQLite com rollback e prevenção de escrita divergente.
- Senha mestre com troca, desbloqueio, recuperação e testes de falha.
- Backup restaurável em outra instalação autorizada.
- QA funcional e instalação/atualização em Windows limpo.

A versão web estável e o projeto Linux não devem ser alterados durante este processo.

## Descoberta do esquema real (revisão de código)
- O arquivo `assets/js/05-app-runtime.js` contém um cofre de dados legado AES-256-GCM/PBKDF2 e rotinas IndexedDB que podem substituir registros em uma transação. A cópia para SQLite deve preservar os envelopes e exigir desbloqueio autorizado antes de ativar os módulos.
- O módulo `assets/js/24-billing.js` usa `localStorage` como fonte atual de cobranças; não realizar troca automática antes de confirmar a compatibilidade do cofre e dos dados de cobrança.
- A migração para SQLite permanece **snapshot sem corte**. Evitar ativar armazenamento duplo de escrita, pois pode causar divergência entre bases.
- Não declarar homologação estável com base apenas em verificações estáticas ou no resultado verde do GitHub Actions.

## Backup nativo SQLite (novo)
- Em Configurações, use **Salvar backup SQLite** para escolher o destino em uma janela nativa do Windows.
- O arquivo é criado como snapshot SQLite consistente, passa por conferência estrutural e autenticação criptográfica dos registros antes de substituir o destino.
- **Atenção:** o backup do SQLite **não** inclui automaticamente os registros que continuam somente no IndexedDB/localStorage; exporte também um backup do sistema legado.
- Os registros protegidos por DPAPI não são portáteis para outra conta Windows. Ainda não há restauração portátil homologada nem interface nativa para senha mestre.
