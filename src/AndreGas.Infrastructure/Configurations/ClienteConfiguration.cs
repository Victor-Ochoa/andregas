using AndreGas.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AndreGas.Infrastructure.Configurations;

public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("Clientes");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.EmpresaId).IsRequired();

        builder.Property(c => c.Nome).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Telefone).IsRequired().HasMaxLength(20);
        builder.Property(c => c.Endereco).IsRequired().HasMaxLength(300);
        builder.Property(c => c.SaldoDevedor).HasPrecision(18, 2);

        // O telefone é a chave natural usada para localizar o cliente na tela de venda — único
        // apenas dentro da empresa (duas empresas podem ter o mesmo telefone cadastrado).
        builder.HasIndex(c => new { c.EmpresaId, c.Telefone }).IsUnique();

        // Índice de tenant: todas as consultas filtram por EmpresaId.
        builder.HasIndex(c => c.EmpresaId);
    }
}