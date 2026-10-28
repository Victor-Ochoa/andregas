using AndreGas.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AndreGas.Infrastructure.Configurations;

public class RecorrenciaCompraConfiguration : IEntityTypeConfiguration<RecorrenciaCompra>
{
    public void Configure(EntityTypeBuilder<RecorrenciaCompra> builder)
    {
        builder.ToTable("RecorrenciasCompra");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.EmpresaId).IsRequired();
        builder.HasIndex(r => r.EmpresaId);

        builder.Property(r => r.IntervaloMedioDias).HasPrecision(10, 2);

        // Um cliente tem no máximo um resumo de recorrência por produto.
        builder.HasIndex(r => new { r.ClienteId, r.ProdutoId }).IsUnique();

        // Restrict por filosofia conservadora: nenhuma exclusão em cascata em domínio. Como o app
        // não exclui clientes nem produtos (apenas desativa), a restrição nunca bloqueia um fluxo real.
        builder.HasOne(r => r.Cliente)
            .WithMany()
            .HasForeignKey(r => r.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Produto)
            .WithMany()
            .HasForeignKey(r => r.ProdutoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}