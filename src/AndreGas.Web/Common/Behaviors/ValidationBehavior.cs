using FluentValidation;
using Mediator;

namespace AndreGas.Web.Common.Behaviors;

/// <summary>
/// Pipeline técnico e transversal do Mediator que valida qualquer comando/consulta com
/// FluentValidation antes de chegar ao handler. Não é uma camada de negócio compartilhada
/// entre slices — cada slice continua definindo seu próprio <see cref="AbstractValidator{T}"/>.
/// </summary>
public sealed class ValidationBehavior<TMessage, TResponse>(IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next(message, cancellationToken);
        }

        var context = new ValidationContext<TMessage>(message);
        var failures = new List<FluentValidation.Results.ValidationFailure>();

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next(message, cancellationToken);
    }
}
