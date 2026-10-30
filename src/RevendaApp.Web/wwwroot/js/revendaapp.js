// Interop mínimo usado pela aba "Minha Empresa" (Features/ConfiguracaoEmpresa/ConfigEmpresaTab.razor).
//
// O Blazor não consegue submeter um <form method="post"> nativo de dentro do circuito. Depois de
// salvar a empresa, a tela precisa recarregar o cookie (para o app bar pegar o nome/cor novos), e
// isso é um POST com antiforgery — daí este submit programático.
window.revendaapp = window.revendaapp || {};

window.revendaapp.submeterFormulario = function (id) {
    const formulario = document.getElementById(id);
    if (formulario) {
        formulario.submit();
    }
};