using AndreGas.Domain.Entities;

namespace AndreGas.Web.Common.Empresas;

/// <summary>
/// Monta o resumo "antes → depois" das alterações de cadastro de uma <see cref="Empresa"/> para o
/// <c>Detalhes</c> de um <see cref="RegistroAuditoria"/>.
/// </summary>
/// <remarks>
/// <para>
/// Existe porque as duas telas que editam empresa (a aba "Minha Empresa" do admin e a edição pela
/// central do super admin) precisam do <b>mesmo</b> formato de auditoria. Duplicar as ~60 linhas de
/// comparação faria os dois resumos divergirem no primeiro campo novo. Não é uma camada genérica de
/// auditoria: só o retrato/comparação dos campos de <see cref="Empresa"/>.
/// </para>
/// <para>
/// O texto é truncado por <see cref="RegistroAuditoria.Truncar"/> (surrogate-safe): o resumo pode
/// passar de 2000 chars (Logradouro 300 + Telefones) e um corte cru poderia partir um par surrogate.
/// </para>
/// </remarks>
public static class DescritorDeAlteracoesEmpresa
{
    /// <summary>
    /// Copia os campos editáveis da empresa. Feito <b>antes</b> da mutação porque
    /// <see cref="Empresa.AtualizarDados"/> sobrescreve os valores no lugar.
    /// </summary>
    public static RetratoEmpresa Retratar(Empresa empresa) => new(
        empresa.Nome,
        empresa.Cnpj,
        empresa.Endereco,
        empresa.Numero,
        empresa.Cep,
        empresa.Bairro,
        empresa.Cidade,
        empresa.Estado,
        empresa.Telefones.ToList(),
        empresa.CorPrimaria,
        empresa.Logo);

    /// <summary>
    /// Monta o resumo em pt-BR a partir de dois retratos. O logo é descrito como
    /// "definido"/"removido" (nunca o base64).
    /// </summary>
    /// <returns>
    /// <c>null</c> quando <b>nada</b> mudou. Quem grava auditoria deve pular a linha nesse caso:
    /// um registro "Nenhuma alteração nos dados da empresa" é um evento que não aconteceu, e
    /// trilha com ruído esconde os eventos que importam.
    /// </returns>
    public static string? Descrever(RetratoEmpresa antes, RetratoEmpresa depois)
    {
        ArgumentNullException.ThrowIfNull(antes);
        ArgumentNullException.ThrowIfNull(depois);

        var linhas = new List<string>();

        Adicionar(linhas, "Nome", antes.Nome, depois.Nome);
        Adicionar(linhas, "CNPJ", antes.Cnpj, depois.Cnpj);
        Adicionar(linhas, "Logradouro", antes.Endereco, depois.Endereco);
        Adicionar(linhas, "Número", antes.Numero, depois.Numero);
        Adicionar(linhas, "CEP", antes.Cep, depois.Cep);
        Adicionar(linhas, "Bairro", antes.Bairro, depois.Bairro);
        Adicionar(linhas, "Cidade", antes.Cidade, depois.Cidade);
        Adicionar(linhas, "UF", antes.Estado, depois.Estado);
        Adicionar(linhas, "Telefones",
            antes.Telefones.Count == 0 ? null : string.Join(", ", antes.Telefones),
            depois.Telefones.Count == 0 ? null : string.Join(", ", depois.Telefones));
        Adicionar(linhas, "Cor primária", antes.CorPrimaria, depois.CorPrimaria);

        if (antes.Logo != depois.Logo)
        {
            linhas.Add($"Logo: {DescreverLogo(antes.Logo)} → {DescreverLogo(depois.Logo)}");
        }

        if (linhas.Count == 0)
        {
            return null;
        }

        return RegistroAuditoria.Truncar(string.Join("\n", linhas), RegistroAuditoria.TamanhoMaximoDetalhes);
    }

    private static void Adicionar(List<string> linhas, string campo, string? antes, string? depois)
    {
        if (string.Equals(antes, depois, StringComparison.Ordinal))
        {
            return;
        }

        linhas.Add($"{campo}: {DescreverValor(antes)} → {DescreverValor(depois)}");
    }

    private static string DescreverValor(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? "(vazio)" : valor.Trim();

    private static string DescreverLogo(string? logo) => string.IsNullOrEmpty(logo) ? "removido" : "definido";

    /// <summary>Valores dos campos editáveis no momento da leitura (comparação antes/depois).</summary>
    public sealed record RetratoEmpresa(
        string Nome,
        string? Cnpj,
        string? Endereco,
        string? Numero,
        string? Cep,
        string? Bairro,
        string? Cidade,
        string? Estado,
        IReadOnlyList<string> Telefones,
        string? CorPrimaria,
        string? Logo);
}