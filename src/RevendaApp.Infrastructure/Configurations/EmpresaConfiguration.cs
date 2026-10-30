using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using RevendaApp.Domain.Entities;

namespace RevendaApp.Infrastructure.Configurations;

public class EmpresaConfiguration : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> builder)
    {
        builder.ToTable("Empresas");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        // O identificador é o slug amigável e único (ex.: "andre"), imutável após a criação.
        builder.Property(e => e.Identificador).IsRequired().HasMaxLength(100);
        builder.HasIndex(e => e.Identificador).IsUnique();

        builder.Property(e => e.Nome).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Cnpj).HasMaxLength(20);
        builder.Property(e => e.Endereco).HasMaxLength(300);
        builder.Property(e => e.Numero).HasMaxLength(20);
        builder.Property(e => e.Cep).HasMaxLength(10);
        builder.Property(e => e.Bairro).HasMaxLength(100);
        builder.Property(e => e.Cidade).HasMaxLength(100);

        // UF — será usado em features futuras (ex.: entrega, geolocalização).
        builder.Property(e => e.Estado).HasMaxLength(2);

        // Cor primária (hex) e logo (base64 data-uri, ~500KB) são opcionais.
        builder.Property(e => e.CorPrimaria).HasMaxLength(9);
        builder.Property(e => e.Logo).HasMaxLength(700_000);

        builder.Property(e => e.Ativo).HasDefaultValue(true);
        builder.Property(e => e.CriadaEm).IsRequired();

        // Telefones é uma coleção de leitura sem setter; o EF Core persiste via campo de apoio
        // (_telefones) como string única com os telefones separados por ";".
        var telefones = builder.Property(e => e.Telefones)
            .HasConversion(
                v => string.Join(";", v),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList())
            .HasMaxLength(2000)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Comparador por CONTEÚDO, obrigatório aqui: Empresa.SetTelefones altera a lista no lugar
        // (Clear + Add) e o acesso é por campo, então o snapshot do EF e o valor atual são a MESMA
        // instância — com o comparador padrão (referência) o EF conclui que nada mudou e a edição
        // de telefones nunca é persistida. O snapshot é uma cópia (ToList), o que faz a comparação
        // sequencial detectar a alteração.
        telefones.Metadata.SetValueComparer(new ValueComparer<IReadOnlyCollection<string>>(
            (a, b) => a!.SequenceEqual(b!),
            v => v.Aggregate(0, (hash, telefone) => HashCode.Combine(hash, telefone.GetHashCode(StringComparison.Ordinal))),
            v => v.ToList()));
    }
}