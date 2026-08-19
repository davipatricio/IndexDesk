Visão Geral da Estratégia de Sincronização
A arquitetura de ingestão e sincronização do IndexDesk foi desenhada com foco em desempenho de milissegundos para o usuário e alta resiliência contra falhas em APIs externas.

Aqui está a explicação conceitual de como todo esse ecossistema funciona na prática:

1. A Filosofia "Local-First"
   O princípio fundamental é que nenhuma requisição de usuário faz chamadas para APIs externas em tempo real.

Quando alguém roda um backtest ou compara dois ETFs no site, o sistema consulta exclusivamente o banco de dados local e o cache em memória. Isso garante que a simulação responda em menos de 10 milissegundos e previne que o seu site caia caso a API do Banco Central, da CVM ou de cotações fique indisponível.

2. O Ciclo de Vida da Sincronização
   A atualização dos dados ocorre em segundo plano através do Worker .NET (`IndexDesk.Worker`):

Agendamento Inteligente (Quartz.NET):
O worker possui tarefas agendadas em horários estratégicos. Por exemplo: as cotações da B3 são buscadas logo após o fechamento do mercado; os dados de inflação do IPCA são lidos uma vez ao mês; e os arquivos de patrimônio líquido da CVM são baixados de madrugada.

Ingestão e Validação em Streaming:
O worker aciona os provedores externos de forma assíncrona, sanitiza os dados e garante que as datas e valores estejam no formato correto. Para a CVM, utiliza leitura em stream e descarte de fundos não mapeados.

Escrita Idempotente no Banco:
Os dados são gravados no PostgreSQL com TimescaleDB via `NpgsqlBinaryImporter` (protocolo COPY). Se a rotina rodar duas vezes no mesmo dia, o sistema reconhece que o registro já existe e apenas atualiza as informações necessárias em vez de duplicar linhas.

3. Resiliência e Tolerância a Falhas (Polly)
   APIs de terceiros oscilam, entram em manutenção ou aplicam limites de requisição. Para lidar com isso sem travar a aplicação, o worker utiliza políticas automáticas de resiliência:

Tentativas Automáticas (Retry): Se uma requisição falhar por instabilidade de rede, o sistema tenta novamente após alguns segundos com intervalos crescentes.

Disjuntor (Circuit Breaker): Se um provedor ficar completamente fora do ar por sucessivas tentativas, o sistema "abre o circuito" e para de fazer chamadas por um tempo determinado, evitando sobrecarregar o provedor e gastar recursos da sua máquina.

4. Atualização do Cache Orientada a Eventos
   Para que a plataforma seja ultra-rápida, os resultados de consultas frequentes e backtests ficam salvos no Redis.

Assim que o worker conclui a inserção de novas cotações no PostgreSQL, ele publica uma mensagem no barramento do RabbitMQ (via MassTransit). O módulo `Modules.Analytics` escuta esse evento e limpa imediatamente do Redis os dados que ficaram desatualizados. A próxima vez que um usuário consultar aquele ativo, o sistema busca o dado novo no banco e reaquece o cache automaticamente.
