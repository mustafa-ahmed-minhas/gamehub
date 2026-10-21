// Real-time ERP notifications for the CRM ("New Website Inquiries").
//
// Loaded on every ERP page so a new website inquiry raises a toast even when the
// user is working in another module or another browser tab. The SignalR hub
// (CrmNotificationsHub) is the primary channel; a lightweight polling endpoint
// keeps the header counter honest if the socket cannot be established.
(function () {
    if (window.GameHubCrmNotifications) {
        return;
    }
    window.GameHubCrmNotifications = true;

    const countSelector = "[data-website-inquiry-count]";
    const pillSelector = "[data-website-inquiry-pill]";

    const hasCounter = () => document.querySelectorAll(countSelector).length > 0;

    const setCount = (value) => {
        const count = Number(value);
        if (!Number.isFinite(count)) {
            return;
        }

        const safe = Math.max(0, Math.trunc(count));
        document.querySelectorAll(countSelector).forEach((element) => {
            if (element.textContent.trim() !== String(safe)) {
                element.textContent = String(safe);
            }
        });

        document.querySelectorAll(pillSelector).forEach((pill) => {
            pill.classList.toggle("has-items", safe > 0);
            pill.classList.toggle("is-empty", safe === 0);
        });
    };

    // Per-tab duplicate suppression: the same lead is only ever announced once,
    // even if an event is somehow delivered twice across a reconnection.
    const announced = new Set();
    const rememberAnnouncement = (leadId) => {
        announced.add(leadId);
        if (announced.size > 100) {
            announced.delete(announced.values().next().value);
        }
    };

    const showNewInquiry = (payload) => {
        const leadId = Number(payload && payload.leadId);
        if (!Number.isInteger(leadId) || leadId <= 0 || announced.has(leadId)) {
            return;
        }
        rememberAnnouncement(leadId);

        const name = typeof payload.customerName === "string" && payload.customerName.trim()
            ? payload.customerName.trim()
            : "A website visitor";
        const leadNumber = typeof payload.leadNumber === "string" && payload.leadNumber.trim()
            ? ` (${payload.leadNumber.trim()})`
            : "";

        if (window.GameHubToast) {
            window.GameHubToast.show({
                type: "info",
                title: "New Website Inquiry",
                message: `${name} submitted an inquiry${leadNumber}.`,
                duration: 12000,
                action: { label: "View Lead", href: `/CRM/LeadDetails/${leadId}` }
            });
        }
    };

    // ---- Counter fallback (polling) ---------------------------------------
    let pollTimer = null;

    const stopPolling = () => {
        if (pollTimer !== null) {
            window.clearInterval(pollTimer);
            pollTimer = null;
        }
    };

    const refreshCount = async () => {
        try {
            const response = await fetch("/CRM/WebsiteInquiryCount", {
                headers: { "X-Requested-With": "XMLHttpRequest" },
                credentials: "same-origin"
            });
            if (!response.ok) {
                return;
            }
            const data = await response.json();
            setCount(data && data.count);
        } catch {
            // Offline, or the page is unloading; the next poll will retry.
        }
    };

    const startPolling = () => {
        if (!hasCounter() || pollTimer !== null) {
            return;
        }
        pollTimer = window.setInterval(refreshCount, 60000);
    };

    // ---- Real-time channel -------------------------------------------------
    if (!window.signalR) {
        // The CDN client did not load: fall back to polling only.
        if (hasCounter()) {
            refreshCount();
            startPolling();
        }
        return;
    }

    let retryDelay = 8000;
    let starting = false;

    const connection = new window.signalR.HubConnectionBuilder()
        .withUrl("/hubs/crm-notifications")
        .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
        .configureLogging(window.signalR.LogLevel.Warning)
        .build();

    const start = async () => {
        if (starting) {
            return;
        }
        starting = true;
        try {
            await connection.start();
            retryDelay = 8000;
            stopPolling();
            if (hasCounter()) {
                // Re-sync in case an event was dispatched while starting.
                await refreshCount();
            }
        } catch (error) {
            const status = Number(error && error.statusCode);
            if (status >= 400 && status < 500) {
                // The server denied the CRM channel (not signed in, or the role
                // is not allowed to see CRM data). Retrying cannot help.
                return;
            }
            startPolling();
            window.setTimeout(start, retryDelay);
            retryDelay = Math.min(retryDelay * 2, 60000);
        } finally {
            starting = false;
        }
    };

    connection.on("newWebsiteInquiry", (payload) => {
        if (payload && typeof payload.newWebsiteInquiryCount !== "undefined") {
            setCount(payload.newWebsiteInquiryCount);
        }
        showNewInquiry(payload);
    });

    connection.on("websiteInquiryCountChanged", (payload) => {
        setCount(payload && payload.count);
    });

    connection.onreconnecting(() => {
        // Connection lost: keep the counter alive with polling until it returns.
        startPolling();
    });

    connection.onreconnected(() => {
        stopPolling();
        if (hasCounter()) {
            refreshCount();
        }
    });

    connection.onclose(() => {
        // Automatic reconnect gave up; poll now and try a fresh start shortly.
        startPolling();
        window.setTimeout(start, retryDelay);
    });

    start();
})();
