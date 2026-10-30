using Bunit;

using Mediator;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using MudBlazor;
using MudBlazor.Services;

using RevendaApp.Web.Features.Central.EditarEmpresa;
using RevendaApp.Web.Features.Central.Empresas;

namespace RevendaApp.UnitTests.Features.Central;

/// <summary>
/// Testes do diálogo de criar/editar empresa da central. O foco é a <b>robustez do
/// <c>SalvarAsync</c></b>: qualquer falha inesperada da gravação (coluna estourada → 22001, banco
/// indisponível, etc.) precisa virar mensagem na tela, e <b>não</b> escapar pelo circuito — no
/// Blazor Server uma exceção não tratada derruba a conexão e o operador perde o formulário inteiro.
/// </summary>
/// <remarks>
/// O diálogo é renderizado dentro de um <c>MudDialogProvider</c> real (é o contexto que fornece o
/// <see cref="IMudDialogInstance"/> por cascata); só o <see cref="ISender"/> é falso, para
/// controlar exatamente qual exceção a gravação lança.
/// </remarks>
public class EmpresaEditorDialogTests
{
    /// <summary>
    /// Regressão do finding 1 da Task 11 (que só tinha sido corrigido em "Minha Empresa"): um erro
    /// de banco na edição da central — por exemplo o <c>22001</c> de um valor maior que a coluna —
    /// derrubava o circuito porque o diálogo só capturava <c>ValidationException</c>.
    /// </summary>
    [Fact]
    public async Task SalvarAsync_mostra_mensagem_amigavel_quando_a_gravacao_falha_com_erro_inesperado()
    {
        await using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        ctx.Services.AddSingleton<ISender>(new SenderQueFalhaAoGravar());

        var cut = RenderDialogo(ctx);

        cut.Find("button.mud-button-filled").Click();

        // A exceção não escapou: o diálogo continua renderizado com a mensagem de erro visível.
        cut.WaitForAssertion(() => Assert.Contains("Não foi possível salvar", cut.Markup, StringComparison.Ordinal));
        Assert.Contains("mud-alert", cut.Markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// Controle positivo do teste acima: um <c>ValidationException</c> continua mostrando as
    /// mensagens de validação específicas (o catch genérico não pode engolir esse caso).
    /// </summary>
    [Fact]
    public async Task SalvarAsync_mostra_as_mensagens_do_validador_quando_a_validacao_falha()
    {
        await using var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        ctx.Services.AddSingleton<ISender>(new SenderQueFalhaAoGravar(
            new FluentValidation.ValidationException(
                [new FluentValidation.Results.ValidationFailure("Endereco", "O logradouro deve ter no máximo 300 caracteres.")])));

        var cut = RenderDialogo(ctx);

        cut.Find("button.mud-button-filled").Click();

        cut.WaitForAssertion(() => Assert.Contains("O logradouro deve ter no máximo 300 caracteres.", cut.Markup, StringComparison.Ordinal));
    }

    private static IRenderedComponent<EmpresaEditorDialog> RenderDialogo(BunitContext ctx)
    {
        var provider = ctx.Render<DialogHost>();
        var dialogService = ctx.Services.GetRequiredService<IDialogService>();

        var parametros = new DialogParameters<EmpresaEditorDialog>
        {
            { x => x.EmpresaId, Guid.NewGuid() },
        };

        _ = provider.InvokeAsync(() => dialogService.ShowAsync<EmpresaEditorDialog>("Configurar empresa", parametros));

        provider.WaitForState(() => provider.Markup.Contains("Salvar", StringComparison.Ordinal), TimeSpan.FromSeconds(5));

        return provider.FindComponent<EmpresaEditorDialog>();
    }

    /// <summary>Hospeda o <c>MudDialogProvider</c> (fornece o <see cref="IMudDialogInstance"/>).</summary>
    private sealed class DialogHost : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<MudDialogProvider>(0);
            builder.CloseComponent();
        }
    }

    /// <summary>
    /// <see cref="ISender"/> de teste: responde a <see cref="ObterEmpresaQuery"/> com um detalhe
    /// válido (o diálogo precisa carregar antes de salvar) e lança na gravação.
    /// </summary>
    private sealed class SenderQueFalhaAoGravar(Exception? excecaoAoGravar = null) : ISender
    {
        private readonly Exception _excecao = excecaoAoGravar
            ?? new DbUpdateException(
                "An error occurred while saving the entity changes.",
                new InvalidOperationException("22001: value too long for type character varying(300)"));

        public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("As sobrecargas usadas pelo diálogo são IQuery e ICommand.");

        public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default) =>
            throw _excecao;

        public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
        {
            if (query is ObterEmpresaQuery consulta)
            {
                var detalhe = new EmpresaDetalhe(
                    consulta.EmpresaId, "empresa-a", "Empresa A",
                    Cnpj: null, Endereco: null, Numero: null, Cep: null, Bairro: null, Cidade: null, Estado: null,
                    [], CorPrimaria: null, Logo: null, Ativo: true);

                return ValueTask.FromResult((TResponse)(object)detalhe);
            }

            throw _excecao;
        }

        public ValueTask<object?> Send(object message, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("O diálogo só usa o Send tipado.");

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamQuery<TResponse> query, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamCommand<TResponse> command, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object message, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}