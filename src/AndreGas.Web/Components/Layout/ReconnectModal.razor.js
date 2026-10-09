// Resiliência de rede do Blazor Server (Interactive Server).
//
// Este arquivo é o module carregado por ReconnectModal.razor.
// Responsabilidades:
//   1. Iniciar o Blazor manualmente (App.razor usa autostart="false") com retry/backoff quando o
//      start/negotiate falhar por rede — em vez de desistir (que deixava "Circuit host not
//      initialized" e modais/menus sem funcionar).
//   2. Mostrar um banner discreto "Sem conexão — tentando reconectar..." enquanto o start falha.
//   3. Reagir ao estado do circuito (components-reconnect-state-changed do blazor.web.js), controlando
//      o <dialog> do ReconnectModal e reconectando automaticamente quando o circuito conectado cair.
//
// O <dialog> (ReconnectModal.razor) é controlado por eventos dispatchados pelo runtime do Blazor.

(() => {
    const reconnectModal = document.getElementById("components-reconnect-modal");
    const retryButton = document.getElementById("components-reconnect-button");
    const resumeButton = document.getElementById("components-resume-button");

    // ----- Banner leve de rede (injetado via JS; não depende do CSS do template) -----
    let networkBanner = null;
    function ensureBanner() {
        if (networkBanner) return networkBanner;
        networkBanner = document.createElement("div");
        networkBanner.id = "andrega-network-banner";
        networkBanner.style.cssText = "position:fixed;bottom:12px;left:50%;transform:translateX(-50%);z-index:9999;background:#0b3d6f;color:#fff;padding:8px 16px;border-radius:20px;font:600 13px/1.4 -apple-system,'Segoe UI',Roboto,sans-serif;box-shadow:0 2px 8px rgba(0,0,0,.25);display:none;";
        networkBanner.textContent = "Sem conexão — tentando reconectar...";
        document.body.appendChild(networkBanner);
        return networkBanner;
    }
    function setBanner(on) {
        const b = ensureBanner();
        b.style.display = on ? "block" : "none";
    }

    // ----- Backoff para start/reconnect -----
    const BACKOFFS = [2000, 5000, 10000, 20000, 30000];
    const delay = (ms) => new Promise(r => setTimeout(r, ms));
    const nextBackoff = (n) => BACKOFFS[Math.min(n, BACKOFFS.length - 1)];

    // ----- 1. Start manual do Blazor com retry no início -----
    async function startBlazorWithRetry() {
        let attempt = 0;
        for (;;) {
            try {
                await Blazor.start();
                setBanner(false);
                return;
            } catch (err) {
                attempt++;
                setBanner(true);
                // Não inundar o console: loga a 1ª e a cada ~5 tentativas.
                if (attempt === 1 || attempt % 5 === 0) {
                    console.warn(`[andregas] Blazor.start falhou (rede?). Tentativa ${attempt}`, err);
                }
                if (document.hidden) {
                    // aba em background: aguarda voltar a ser visível antes de retry
                    await new Promise(resolve => {
                        const onVis = () => {
                            document.removeEventListener("visibilitychange", onVis);
                            resolve();
                        };
                        document.addEventListener("visibilitychange", onVis);
                    });
                } else {
                    await delay(nextBackoff(attempt));
                }
            }
        }
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", () => startBlazorWithRetry());
    } else {
        startBlazorWithRetry();
    }

    // ----- 2. Estado do circuito (dispatchado pelo runtime do Blazor) -----
    // Durante o reconnect, o runtime re-emite components-reconnect-state-changed; tratamos
    // os mesmos estados do template original (show/hide/failed/rejected) + reconexão automática.
    function handleReconnectStateChanged(event) {
        const st = event.detail && event.detail.state;
        if (st === "show") {
            reconnectModal?.showModal();
        } else if (st === "hide") {
            reconnectModal?.close();
            setBanner(false);
        } else if (st === "failed") {
            setBanner(true);
            // Tenta reconectar automaticamente com backoff (é o desejado p/ rede instável).
            autoReconnect();
        } else if (st === "rejected") {
            location.reload();
        }
    }
    reconnectModal?.addEventListener("components-reconnect-state-changed", handleReconnectStateChanged);

    let autoReconnecting = false;
    async function autoReconnect() {
        if (autoReconnecting) return;
        autoReconnecting = true;
        try {
            let attempt = 0;
            for (;;) {
                try {
                    const successful = await Blazor.reconnect();
                    if (successful) {
                        setBanner(false);
                        return;
                    }
                    // Falso = servidor respondeu, mas o circuito expirou.
                    const resumeSuccessful = await Blazor.resumeCircuit();
                    if (resumeSuccessful) {
                        setBanner(false);
                        return;
                    }
                    location.reload();
                    return;
                } catch (err) {
                    attempt++;
                    // Servidor indisponível — retry com backoff (respeitando visibilidade).
                    await delay(nextBackoff(attempt));
                }
            }
        } finally {
            autoReconnecting = false;
        }
    }

    // ----- 3. Botões do <dialog> (fallback do usuário) -----
    retryButton?.addEventListener("click", () => autoReconnect());
    resumeButton?.addEventListener("click", async () => {
        try {
            const ok = await Blazor.resumeCircuit();
            if (!ok) location.reload();
        } catch {
            // servidor ainda indisponível
        }
    });
})();