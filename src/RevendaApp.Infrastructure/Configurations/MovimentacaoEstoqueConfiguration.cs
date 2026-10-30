using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using RevendaApp.Domain.Entities;

namespace RevendaApp.Infrastructure.Configurations;

public class MovimentacaoEstoqueConfiguration : IEntityTypeConfiguration<MovimentacaoEstoque>
{
    public void Configure(EntityTypeBuilder<MovimentacaoEstoque> builder)
    {
        builder.ToTable("MovimentacoesEstoque");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.EmpresaId).IsRequired();
        builder.HasIndex(m => m.EmpresaId);

        builder.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Motivo).HasMaxLength(300);

        builder.HasOne(m => m.Produto)
            .WithMany()
            .HasForeignKey(m => m.ProdutoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}