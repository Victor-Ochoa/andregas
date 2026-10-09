using Mediator;

namespace AndreGas.Web.Features.Clientes.Cadastrar;

/// <summary>Cadastra um novo cliente. O telefone é a chave natural — deve ser único.</summary>
public sealed record CadastrarClienteCommand(string Nome, string Telefone, string Endereco) : ICommand<Guid>;