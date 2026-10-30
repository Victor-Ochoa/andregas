using AndreGas.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AndreGas.Infrastructure.Configurations;

/// <summary>
/// Mapeamento de <see cref="RegistroAuditoria"/>: tabela <c>RegistrosAuditoria</c>, <b>global</b>.
/// </summary>
/// <remarks>
/// Não há FK para <c>Empresas</c>: o registro sobrevive à empresa (uma inativação futura não deve
/// apagar a trilha) e <c>EmpresaId</c> nulo representa ação da central. Também não há filtro global
/// — a central lê tudo, a empresa filtra explicitamente pelos seus registros.
/// </remarks>
public class RegistroAuditoriaConfiguration : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> builder)
    {
        builder.ToTable("RegistrosAuditoria");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.EmpresaId);
        builder.Property(r => r.UsuarioId);

        builder.Property(r => r.UsuarioNome).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Acao).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Detalhes).IsRequired().HasMaxLength(RegistroAuditoria.TamanhoMaximoDetalhes);
        builder.Property(r => r.Origem).IsRequired().HasMaxLength(20);
        builder.Property(r => r.DataHora).IsRequired();

        // Consultas: "últimos eventos" (lista da central) e "últimos eventos desta empresa"
        // (lista por empresa). Índices descendentes em DataHora casam com o ORDER BY DESC.
        builder.HasIndex(r => r.DataHora).IsDescending();
        builder.HasIndex(r => new { r.EmpresaId, r.DataHora }).IsDescending(false, true);
    }
}