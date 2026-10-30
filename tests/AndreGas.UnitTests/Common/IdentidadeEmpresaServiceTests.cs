using AndreGas.Domain.Entities;
using AndreGas.UnitTests.TestHelpers;
using AndreGas.Web.Common;
using AndreGas.Web.Theme;

using Microsoft.EntityFrameworkCore;

namespace AndreGas.UnitTests.Common;

/// <summary>
/// Testes do <see cref="IdentidadeEmpresaService"/>: identidade exibida no app bar (nome, cor e logo
/// da empresa; produto fixo <c>Revenda.App</c> no modo central).
/// </summary>
public class IdentidadeEmpresaServiceTests
{
    [Fact]
    public void Servico_sem_empresa_retorna_revenda_app()
    {
        // Modo central (super admin sem empresa): a marca exibida é o produto, sem logo.
        using var db = InMemoryDbContextFactory.Create(nomeBanco: Guid.NewGuid().ToString());
        var servico = new IdentidadeEmpresaService(new FakeTenantContext(empresaId: null), db);

        Assert.Equal("Revenda.App", servico.NomeEmpresa);
        Assert.False(servico.TemEmpresa);
        Assert.Null(servico.Logo);
        Assert.Equal(AndreGasTheme.CorPrimariaPadrao, servico.CorPrimaria);
    }

    [Fact]
    public void Servico_com_empresa_retorna_nome_e_cor_das_claims()
    {
        var empresaId = Guid.NewGuid();
        var tenant = new FakeTenantContext(empresaId, nomeEmpresa: "Gás Central", corPrimaria: "#CC0000");

        using var db = InMemoryDbContextFactory.Create(empresaId, tenant: tenant);
        var servico = new IdentidadeEmpresaService(tenant, db);

        Assert.Equal("Gás Central", servico.NomeEmpresa);
        Assert.Equal("#CC0000", servico.CorPrimaria);
        Assert.True(servico.TemEmpresa);
    }

    [Fact]
    public void Servico_carrega_logo_do_banco_da_empresa_do_tenant()
    {
        // Controle positivo: duas empresas com logos diferentes no MESMO banco. O serviço precisa
        // devolver o logo da empresa do tenant, não o de outra empresa (prova que filtra por Id).
        var banco = Guid.NewGuid().ToString();
        var empresaA = new Empresa("empresa-a", "Empresa A", logo: "data:image/png;base64,LOGO-A");
        var empresaB = new Empresa("empresa-b", "Empresa B", logo: "data:image/png;base64,LOGO-B");

        using (var dbCentral = InMemoryDbContextFactory.Create(nomeBanco: banco))
        {
            dbCentral.Set<Empresa>().AddRange(empresaA, empresaB);
            dbCentral.SaveChanges();
        }

        // O tenant é a empresa B (a segunda inserida): uma implementação que devolvesse "o primeiro
        // logo do banco" falharia aqui.
        var tenant = new FakeTenantContext(empresaB.Id, nomeEmpresa: "Empresa B", corPrimaria: "#0A1F44");
        using var db = InMemoryDbContextFactory.Create(empresaB.Id, banco, tenant);
        var servico = new IdentidadeEmpresaService(tenant, db);

        Assert.Equal("data:image/png;base64,LOGO-B", servico.Logo);
    }

    [Fact]
    public void Servico_consulta_o_banco_uma_unica_vez_por_escopo()
    {
        // O logo é lido do banco uma única vez por escopo (cache em campo memoizado). Trocar o logo
        // no banco entre as duas leituras e continuar recebendo o valor antigo prova o cache.
        var banco = Guid.NewGuid().ToString();
        var empresa = new Empresa("empresa-a", "Empresa A", logo: "data:image/png;base64,ANTIGO");

        using (var dbCentral = InMemoryDbContextFactory.Create(nomeBanco: banco))
        {
            dbCentral.Set<Empresa>().Add(empresa);
            dbCentral.SaveChanges();
        }

        var tenant = new FakeTenantContext(empresa.Id, nomeEmpresa: "Empresa A");
        using var db = InMemoryDbContextFactory.Create(empresa.Id, banco, tenant);
        var servico = new IdentidadeEmpresaService(tenant, db);

        Assert.Equal("data:image/png;base64,ANTIGO", servico.Logo);

        using (var dbCentral = InMemoryDbContextFactory.Create(nomeBanco: banco))
        {
            var persistida = dbCentral.Set<Empresa>().Single(e => e.Id == empresa.Id);
            persistida.AtualizarDados("Empresa A", logo: "data:image/png;base64,NOVO");
            dbCentral.SaveChanges();
        }

        Assert.Equal("data:image/png;base64,ANTIGO", servico.Logo);
    }

    [Fact]
    public void Titulo_compõe_tela_e_nome_da_empresa()
    {
        var empresaId = Guid.NewGuid();
        var tenant = new FakeTenantContext(empresaId, nomeEmpresa: "Gás Central", corPrimaria: "#CC0000");

        using var db = InMemoryDbContextFactory.Create(empresaId, tenant: tenant);
        var servico = new IdentidadeEmpresaService(tenant, db);

        Assert.Equal("Estoque — Gás Central", servico.Titulo("Estoque"));
    }

    [Fact]
    public void Titulo_no_modo_central_usa_o_nome_do_produto()
    {
        using var db = InMemoryDbContextFactory.Create(nomeBanco: Guid.NewGuid().ToString());
        var servico = new IdentidadeEmpresaService(new FakeTenantContext(empresaId: null), db);

        Assert.Equal("Estoque — Revenda.App", servico.Titulo("Estoque"));
    }
}