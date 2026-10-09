using AndreGas.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AndreGas.Infrastructure.Configurations;

public class PagamentoConfiguration : IEntityTypeConfiguration<Pagamento>
{
    public void Configure(EntityTypeBuilder<Pagamento> builder)
    {
        builder.ToTable("Pagamentos");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Valor).HasPrecision(18, 2);
        builder.Property(p => p.Observacao).HasMaxLength(500);
        builder.Property(p => p.FormaPagamento).HasConversion<string>().HasMaxLength(30);

        builder.HasOne(p => p.Cliente)
            .WithMany()
            .HasForeignKey(p => p.ClienteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}