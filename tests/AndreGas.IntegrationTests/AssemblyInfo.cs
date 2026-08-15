using Xunit;

// Cada classe de teste desta suíte sobe sua própria instância do Aspire AppHost (com um
// container Postgres real). Rodar essas classes em paralelo sobrecarrega o Docker/CPU do
// ambiente e causa falhas transitórias de conexão — por isso a suíte roda sequencialmente.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
