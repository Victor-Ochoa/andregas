using Mediator;

namespace RevendaApp.Web.Features.Vendas.NovaVenda;

/// <summary>
/// Busca um cliente pelo Id para pré-selecionar na Nova Venda (ex.: acionada pelo aviso de
/// recompra da Home). Retorna os mesmos dados do <see cref="ClienteSugestao"/> do autocomplete,
/// incluindo o valor de entrega da última venda.
/// </summary>
public sealed record ObterClienteParaVendaQuery(Guid ClienteId) : IQuery<ClienteSugestao?>;