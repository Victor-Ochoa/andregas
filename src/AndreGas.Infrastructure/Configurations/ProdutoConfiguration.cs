using AndreGas.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AndreGas.Infrastructure.Configurations;

public class ProdutoConfiguration : IEntityTypeConfiguration<Produto>
{
    public void Configure(EntityTypeBuilder<Produto> builder)
    {
        builder.ToTable("Produtos");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Nome).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Tipo).HasConversion<string>().HasMaxLength(50);
        builder.Property(p => p.PrecoVenda).HasPrecision(18, 2);
        builder.Property(p => p.PrecoCusto).HasPrecision(18, 2);
        builder.Property(p => p.PrecoGasDoPovo).HasPrecision(18, 2);
        builder.Property(p => p.Ativo).HasDefaultValue(true);

        builder.Ignore(p => p.EstoqueBaixo);
    }
}