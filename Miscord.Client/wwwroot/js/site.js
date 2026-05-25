// ─── SignalR Connection ────────────────────────────────────────────
let connection = new signalR.HubConnectionBuilder()
    .withUrl("/messages")
    .withAutomaticReconnect()
    .build();

// Track last message for grouping
let lastMsgUser  = null;
let lastMsgTime  = null;
const GROUP_GAP_MS = 7 * 60 * 1000; // 7 min = new group

connection.on("ReceiveMessage", function (user, message) {
    const container = document.getElementById("messages-container");
    if (!container) return;

    const now       = new Date();
    const timeStr   = now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    
    // Discord message structure
    const messageDiv = document.createElement("div");
    messageDiv.className = "message-item";
    
    messageDiv.innerHTML = `
        <div class="contents">
            <img src="/favicon.ico" style="width: 40px; height: 40px; border-radius: 50%; position: absolute; left: 16px; top: 2px;" />
            <h3 style="margin: 0; line-height: 1.375rem; display: flex; align-items: center; gap: 8px;">
                <span class="username-text" style="font-weight: 600; color: #ffffff; font-size: 1rem; cursor: pointer;">${user}</span>
                <span style="font-size: 0.75rem; color: #949ba4; font-weight: 400;">Today at ${timeStr}</span>
            </h3>
            <div style="color: #dbdee1; font-size: 1rem; line-height: 1.375rem; white-space: pre-wrap; word-wrap: break-word;">${message}</div>
        </div>
    `;

    container.appendChild(messageDiv);
    container.scrollTop = container.scrollHeight;

    lastMsgUser = user;
    lastMsgTime = now;
});

connection.start().catch(function(err) { console.error("SignalR error:", err); });

// ─── Server Loading ────────────────────────────────────────────────
function loadServer(serverId, el) {
    document.querySelectorAll('.server-icon-wrapper').forEach(function(w) { w.classList.remove('active'); });
    if (el) {
        var wrapper = el.closest('.server-icon-wrapper');
        if (wrapper) wrapper.classList.add('active');
    }

    fetch('/Server/GetChannels/' + serverId)
        .then(function(r) { return r.text(); })
        .then(function(html) {
            var list = document.querySelector('.channel-list');
            if (list) list.innerHTML = html;
        })
        .catch(console.error);

    var main = document.querySelector('.main-content');
    if (main) {
        main.innerHTML =
            '<div class="empty-main">' +
              '<svg width="72" height="72" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M4 9h16M4 15h16M10 3L8 21M16 3l-2 18"/></svg>' +
              '<span class="empty-main-title">No channel selected</span>' +
              '<span class="empty-main-text">Pick a channel from the sidebar to start chatting.</span>' +
            '</div>';
    }

    lastMsgUser = null;
    lastMsgTime = null;
}

// ─── Channel Loading ───────────────────────────────────────────────
function loadChannel(channelId) {
    document.querySelectorAll('.channel-item').forEach(function(c) { c.classList.remove('active'); });
    var clicked = document.querySelector('.channel-item[onclick="loadChannel(' + channelId + ')"]');
    if (clicked) clicked.classList.add('active');

    fetch('/Server/GetChat?channelId=' + channelId)
        .then(function(r) { return r.text(); })
        .then(function(html) {
            var main = document.querySelector('.main-content');
            if (main) {
                main.innerHTML = html;
                setupChatInput(channelId);
                // Join SignalR Group
                connection.invoke("JoinChannel", parseInt(channelId)).catch(err => console.error(err.toString()));
            }
        })
        .catch(console.error);

    lastMsgUser = null;
    lastMsgTime = null;
}

// ─── Chat Input Binding ────────────────────────────────────────────
function setupChatInput(channelId) {
    var input = document.getElementById("message-input");
    if (!input) return;

    input.focus();

    input.addEventListener("keydown", function(e) {
        if (e.key !== "Enter" || e.shiftKey) return;
        e.preventDefault();

        var message = input.value.trim();
        if (!message) return;

        var userId = document.getElementById("current-user-id");
        if (!userId) return;

        connection.invoke("SendMessage", userId.value, parseInt(channelId), message)
            .catch(function(err) { console.error("Send error:", err); });

        input.value = "";
    });
}

// ─── Utility ──────────────────────────────────────────────────────
function escapeHtml(str) {
    return String(str)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}