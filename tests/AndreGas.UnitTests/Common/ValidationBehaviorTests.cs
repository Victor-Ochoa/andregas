using AndreGas.Web.Common.Behaviors;

using FluentValidation;

using Mediator;

namespace AndreGas.UnitTests.Common;

/// <summary>Mensagem de teste que o pipeline pode validar (Mediator exige <see cref="IMessage"/>).</summary>
public sealed record ComandoDeTeste(string Nome, int Quantidade) : ICommand;

public sealed class ComandoValidatorUm : AbstractValidator<ComandoDeTeste>
{
    public ComandoValidatorUm()
    {
        RuleFor(x => x.Nome).NotEmpty().WithMessage("Nome é obrigatório.");
    }
}

public sealed class ComandoValidatorDois : AbstractValidator<ComandoDeTeste>
{
    public ComandoValidatorDois()
    {
        RuleFor(x => x.Quantidade).GreaterThan(0).WithMessage("Quantidade deve ser positiva.");
    }
}

public class ValidationBehaviorTests
{
    private static readonly MessageHandlerDelegate<ComandoDeTeste, string> NextFlow =
        (message, ct) => ValueTask.FromResult($"ok:{message.Nome}");

    [Fact]
    public async Task Handle_SemValidadores_DeveChamarProximo()
    {
        var behavior = new ValidationBehavior<ComandoDeTeste, string>(Array.Empty<IValidator<ComandoDeTeste>>());

        var result = await behavior.Handle(
            new ComandoDeTeste("produto", 2),
            NextFlow,
            CancellationToken.None);

        Assert.Equal("ok:produto", result);
    }

    [Fact]
    public async Task Handle_ComUmValidadorValido_DeveChamarProximo()
    {
        var behavior = new ValidationBehavior<ComandoDeTeste, string>(
            new IValidator<ComandoDeTeste>[] { new ComandoValidatorUm() });

        var result = await behavior.Handle(
            new ComandoDeTeste("produto", 2),
            NextFlow,
            CancellationToken.None);

        Assert.Equal("ok:produto", result);
    }

    [Fact]
    public async Task Handle_ComFalhaNoValidador_DeveLancarValidationException()
    {
        var behavior = new ValidationBehavior<ComandoDeTeste, string>(
            new IValidator<ComandoDeTeste>[] { new ComandoValidatorUm() });

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(new ComandoDeTeste("", 2), NextFlow, CancellationToken.None).AsTask());

        Assert.Contains(ex.Errors, e => e.PropertyName == "Nome");
    }

    [Fact]
    public async Task Handle_ComDoisValidadores_DeveAgregarFalhasDeAmbos()
    {
        var behavior = new ValidationBehavior<ComandoDeTeste, string>(
            new IValidator<ComandoDeTeste>[] { new ComandoValidatorUm(), new ComandoValidatorDois() });

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(new ComandoDeTeste("", 0), NextFlow, CancellationToken.None).AsTask());

        Assert.Contains(ex.Errors, e => e.PropertyName == "Nome");
        Assert.Contains(ex.Errors, e => e.PropertyName == "Quantidade");
    }

    [Fact]
    public async Task Handle_QuandoOProximoLanca_DevePropagarSemEngolir()
    {
        var behavior = new ValidationBehavior<ComandoDeTeste, string>(
            new IValidator<ComandoDeTeste>[] { new ComandoValidatorUm() });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(
                new ComandoDeTeste("produto", 2),
                (_, ct) => ValueTask.FromException<string>(new InvalidOperationException("erro do handler")),
                CancellationToken.None).AsTask());
    }
}