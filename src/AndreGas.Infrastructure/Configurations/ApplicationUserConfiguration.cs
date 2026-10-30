using AndreGas.Domain.Entities;
using AndreGas.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AndreGas.Infrastructure.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // Usuário inativo não loga; o padrão é ativo para não surpreender cadastros novos.
        builder.Property(u => u.Ativo).IsRequired().HasDefaultValue(true);

        // Empresa do usuário (null = usuário da central/super admin). Restrict: excluir a empresa
        // não pode apagar em cascata os usuários dela — desative a empresa, não a remova.
        builder.HasOne<Empresa>()
            .WithMany()
            .HasForeignKey(u => u.EmpresaId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}