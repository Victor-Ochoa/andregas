using Mediator;

namespace AndreGas.Web.Features.ConfiguracaoEmpresa;

/// <summary>
/// Edição dos dados de cadastro da <b>própria</b> empresa pela aba "Minha Empresa".
/// </summary>
/// <remarks>
/// <para>
/// <b>Não</b> recebe <c>EmpresaId</c>: a empresa vem sempre do <see cref="AndreGas.Infrastructure.Tenancy.ITenantContext"/>
/// (<c>EmpresaIdRequerida</c>), o que impede um administrador de uma empresa editar outra passando
/// um id forjado.
/// </para>
/// <para>
/// <b>Não</b> expõe <c>Identificador</c> (imutável após a criação) nem permite ativar/inativar a
/// empresa — essas operações são exclusivas da central.
/// </para>
/// </remarks>
/// <param name="Nome">Nome exibido (obrigatório).</param>
/// <param name="Cnpj">CNPJ (opcional; 14 dígitos, com ou sem máscara).</param>
/// <param name="Endereco">Logradouro (opcional).</param>
/// <param name="Numero">Número (opcional).</param>
/// <param name="Cep">CEP (opcional; 8 dígitos, com ou sem máscara).</param>
/// <param name="Bairro">Bairro (opcional).</param>
/// <param name="Cidade">Cidade (opcional).</param>
/// <param name="Estado">UF (opcional; 2 letras).</param>
/// <param name="Telefones">Telefones de contato (opcional; o domínio normaliza para dígitos).</param>
/// <param name="CorPrimaria">Cor primária em hex (opcional; <c>#RGB</c> ou <c>#RRGGBB</c>).</param>
/// <param name="Logo">Logo como data-uri base64 (opcional; ~500KB).</param>
public sealed record EditarConfigEmpresaCommand(
    string Nome,
    string? Cnpj = null,
    string? Endereco = null,
    string? Numero = null,
    string? Cep = null,
    string? Bairro = null,
    string? Cidade = null,
    string? Estado = null,
    IReadOnlyList<string>? Telefones = null,
    string? CorPrimaria = null,
    string? Logo = null) : ICommand<bool>;