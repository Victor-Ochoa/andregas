using AndreGas.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AndreGas.Infrastructure.Configurations;

public class VendaConfiguration : IEntityTypeConfiguration<Venda>
{
    public void Configure(EntityTypeBuilder<Venda> builder)
    {
        builder.ToTable("Vendas");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.FormaPagamento).HasConversion<string>().HasMaxLength(30);
        builder.Property(v => v.Desconto).HasPrecision(18, 2);
        builder.Property(v => v.ValorEntrega).HasPrecision(18, 2);
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(30);

        builder.Ignore(v => v.ValorBruto);
        builder.Ignore(v => v.ValorTotal);
        builder.Ignore(v => v.LucroTotal);

        builder.HasOne(v => v.Cliente)
            .WithMany()
            .HasForeignKey(v => v.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(v => v.Itens)
            .WithOne()
            .HasForeignKey(i => i.VendaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(v => v.Itens).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
