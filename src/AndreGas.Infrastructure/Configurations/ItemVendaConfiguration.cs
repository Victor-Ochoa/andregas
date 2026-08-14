using AndreGas.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AndreGas.Infrastructure.Configurations;

public class ItemVendaConfiguration : IEntityTypeConfiguration<ItemVenda>
{
    public void Configure(EntityTypeBuilder<ItemVenda> builder)
    {
        builder.ToTable("ItensVenda");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.PrecoUnitario).HasPrecision(18, 2);
        builder.Property(i => i.PrecoCustoUnitario).HasPrecision(18, 2);
        builder.Property(i => i.DescontoRateado).HasPrecision(18, 2);

        builder.Ignore(i => i.LucroBruto);
        builder.Ignore(i => i.Lucro);
        builder.Ignore(i => i.ValorTotal);

        builder.HasOne(i => i.Produto)
            .WithMany()
            .HasForeignKey(i => i.ProdutoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
