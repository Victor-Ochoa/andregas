using Mediator;

namespace RevendaApp.Web.Features.Central.EditarEmpresa;

/// <summary>
/// Edita os dados de cadastro de uma empresa pela central. O <see cref="Identificador"/> é
/// <b>imutável</b> e por isso nem aparece no comando — a UI o mostra somente leitura e o domínio
/// (<see cref="RevendaApp.Domain.Entities.Empresa.AtualizarDados"/>) nunca o altera.
/// </summary>
/// <param name="EmpresaId">Empresa a editar.</param>
/// <param name="Nome">Nome exibido (obrigatório).</param>
/// <param name="Cnpj">CNPJ (opcional).</param>
/// <param name="Endereco">Logradouro (opcional).</param>
/// <param name="Numero">Número (opcional).</param>
/// <param name="Cep">CEP (opcional).</param>
/// <param name="Bairro">Bairro (opcional).</param>
/// <param name="Cidade">Cidade (opcional).</param>
/// <param name="Estado">UF (opcional).</param>
/// <param name="Telefones">Telefones de contato (opcional).</param>
/// <param name="CorPrimaria">Cor primária em hex (opcional).</param>
/// <param name="Logo">Logo como data-uri base64 (opcional).</param>
public sealed record EditarEmpresaCommand(
    Guid EmpresaId,
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