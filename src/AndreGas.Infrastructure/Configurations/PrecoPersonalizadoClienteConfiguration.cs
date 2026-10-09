using AndreGas.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AndreGas.Infrastructure.Configurations;

public class PrecoPersonalizadoClienteConfiguration : IEntityTypeConfiguration<PrecoPersonalizadoCliente>
{
    public void Configure(EntityTypeBuilder<PrecoPersonalizadoCliente> builder)
    {
        builder.ToTable("PrecosPersonalizadosClientes");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Preco).HasPrecision(18, 2);

        // Um cliente tem no máximo um acordo por produto.
        builder.HasIndex(p => new { p.ClienteId, p.ProdutoId }).IsUnique();

        // Restrict por filosofia conservadora: nenhuma exclusão em cascata em domínio. Como o app
        // não exclui clientes nem produtos (apenas desativa), a restrição nunca bloqueia um fluxo real.
        builder.HasOne(p => p.Cliente)
            .WithMany()
            .HasForeignKey(p => p.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Produto)
            .WithMany()
            .HasForeignKey(p => p.ProdutoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}