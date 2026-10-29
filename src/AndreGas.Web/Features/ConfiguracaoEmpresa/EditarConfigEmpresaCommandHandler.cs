using AndreGas.Domain.Entities;
using AndreGas.Infrastructure;
using AndreGas.Infrastructure.Tenancy;
using AndreGas.Web.Common;
using AndreGas.Web.Common.Authorization;

using Mediator;

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace AndreGas.Web.Features.ConfiguracaoEmpresa;

/// <summary>
/// Edita os dados de cadastro da <b>própria</b> empresa do usuário e grava um
/// <see cref="RegistroAuditoria"/> com o resumo "antes → depois".
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant:</b> a empresa vem de <see cref="ITenantContext.EmpresaIdRequerida"/> — é a primeira
/// instrução do handler. No modo central (super admin sem empresa) ela lança e nada é gravado: esta
/// tela não edita empresa por id.
/// </para>
/// <para>
/// <b>Autorização:</b> além da política de rota (<c>ApenasAdmin</c>), o handler chama
/// <see cref="IUsuarioAutenticado.RequerAdminAsync"/> — este é o limite real de segurança.
/// </para>
/// <para>
/// <b>Atomicidade da auditoria:</b> o registro é adicionado ao <b>mesmo</b>
/// <see cref="AppDbContext"/> e persistido no <b>mesmo</b> <c>SaveChangesAsync</c> da edição. Uma
/// edição que falhe (domínio ou banco) não grava nem a alteração nem a auditoria.
/// </para>
/// <para>
/// <b>Identificador imutável:</b> <see cref="Empresa.AtualizarDados"/> não o altera e o comando não
/// tem o campo. <b>Sem inativação:</b> o comando não expõe <c>Ativo</c>.
/// </para>
/// </remarks>
public sealed class EditarConfigEmpresaCommandHandler(
    AppDbContext db,
    ITenantContext tenant,
    IUsuarioAutenticado usuarioAutenticado,
    AuthenticationStateProvider? authStateProvider = null)
    : ICommandHandler<EditarConfigEmpresaCommand, bool>
{
    public async ValueTask<bool> Handle(EditarConfigEmpresaCommand command, CancellationToken cancellationToken)
    {
        // Primeira instrução: exige empresa selecionada (modo central não edita empresa).
        var empresaId = tenant.EmpresaIdRequerida("editar a empresa");

        // Limite real de segurança: a política da rota não basta.
        await usuarioAutenticado.RequerAdminAsync(cancellationToken);

        // `Empresas` não tem filtro global, mas a busca é pelo id do próprio tenant.
        var empresa = await db.Set<Empresa>()
            .FirstOrDefaultAsync(e => e.Id == empresaId, cancellationToken);

        if (empresa is null)
        {
            return false;
        }

        var antes = RetratoDe(empresa);

        // Identificador não é passado de propósito: é imutável após a criação.
        // A UF é normalizada AQUI (único ponto de escrita desta tela): o formulário e a API não
        // impõem caixa, e "sp" gravado como veio viraria uma UF diferente de "SP" em qualquer
        // comparação/relatório futuro. A coluna é `character varying(2)`; a regra do validador já
        // garante 2 letras ASCII, então `ToUpperInvariant` não muda o tamanho.
        empresa.AtualizarDados(
            command.Nome,
            command.Cnpj,
            command.Endereco,
            command.Numero,
            command.Cep,
            command.Bairro,
            command.Cidade,
            NormalizarUf(command.Estado),
            command.Telefones,
            command.CorPrimaria,
            command.Logo);

        var detalhes = DescreverMudancas(antes, RetratoDe(empresa));
        var (usuarioNome, usuarioId) = await UsuarioAtual.ObterAsync(authStateProvider, db, cancellationToken);

        db.RegistrosAuditoria.Add(new RegistroAuditoria(
            empresaId,
            usuarioId,
            usuarioNome,
            RegistroAuditoria.Acoes.ConfigEmpresaEditada,
            detalhes,
            RegistroAuditoria.Origens.App));

        // Um único SaveChanges: edição + auditoria na mesma transação implícita.
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Copia os campos editáveis da empresa para comparar antes/depois. Feito <b>antes</b> da
    /// mutação porque <see cref="Empresa.AtualizarDados"/> sobrescreve os valores no lugar.
    /// </summary>
    private static RetratoEmpresa RetratoDe(Empresa empresa) => new(
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
    /// Monta o resumo em pt-BR no formato <c>Campo: antes → depois</c> por campo alterado — o mesmo
    /// espírito do <c>HistoricoEstoque</c>. O logo é descrito como "definido"/"removido" (nunca o
    /// base64). O texto é truncado em <see cref="RegistroAuditoria.TamanhoMaximoDetalhes"/>.
    /// </summary>
    private static string DescreverMudancas(RetratoEmpresa antes, RetratoEmpresa depois)
    {
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

        // O logo é base64 (centenas de KB): registra só a presença, nunca o conteúdo.
        if (antes.Logo != depois.Logo)
        {
            linhas.Add($"Logo: {DescreverLogo(antes.Logo)} → {DescreverLogo(depois.Logo)}");
        }

        if (linhas.Count == 0)
        {
            return "Nenhuma alteração nos dados da empresa.";
        }

        // O resumo pode passar de 2000 chars (Logradouro 300 + Telefones 2000 + o resto): corta pelo
        // helper surrogate-safe da entidade, não por `texto[..2000]` — um corte cru poderia partir
        // um par surrogate (emoji no nome/bairro) e a entidade não teria como consertar.
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

    /// <summary>UF em maiúsculas (ex.: <c>"sp"</c> → <c>"SP"</c>); vazio continua vazio.</summary>
    private static string? NormalizarUf(string? estado) =>
        string.IsNullOrWhiteSpace(estado) ? estado : estado.Trim().ToUpperInvariant();

    private static string DescreverLogo(string? logo) => string.IsNullOrEmpty(logo) ? "removido" : "definido";

    /// <summary>Valores dos campos editáveis no momento da leitura (comparação antes/depois).</summary>
    private sealed record RetratoEmpresa(
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