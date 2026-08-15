using Mediator;

namespace AndreGas.Web.Features.Clientes.Detalhe;

/// <summary>Atualiza nome, telefone e endereço de um cliente existente.</summary>
public sealed record AtualizarClienteCommand(Guid Id, string Nome, string Telefone, string Endereco) : ICommand<bool>;
