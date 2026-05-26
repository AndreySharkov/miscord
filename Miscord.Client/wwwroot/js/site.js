// ─── Username → stable avatar color ───────────────────────────────
var AVATAR_COLORS = [
    '#e91e63','#9c27b0','#673ab7','#3f51b5',
    '#1976d2','#0097a7','#388e3c','#f57c00',
    '#e64a19','#c62828','#00897b','#43a047',
    '#fb8c00','#6d4c41','#8e24aa','#1e88e5'
];

function getUserColor(username) {
    var hash = 0, s = String(username || '?');
    for (var i = 0; i < s.length; i++)
        hash = (s.charCodeAt(i) + ((hash << 5) - hash)) | 0;
    return AVATAR_COLORS[Math.abs(hash) % AVATAR_COLORS.length];
}

// ─── Consistent HH:MM AM/PM (no locale surprises) ─────────────────
function formatTime(date) {
    var h = date.getHours(), m = date.getMinutes();
    return (h % 12 || 12) + ':' + (m < 10 ? '0' + m : m) + (h >= 12 ? ' PM' : ' AM');
}
function formatTimestamp(date) {
    var now = new Date();
    if (date.toDateString() === now.toDateString()) return 'Today at ' + formatTime(date);
    var yest = new Date(now); yest.setDate(now.getDate() - 1);
    if (date.toDateString() === yest.toDateString()) return 'Yesterday at ' + formatTime(date);
    var mm = date.getMonth()+1, dd = date.getDate(), yy = date.getFullYear();
    return (mm<10?'0'+mm:mm)+'/'+(dd<10?'0'+dd:dd)+'/'+yy+' '+formatTime(date);
}

// ─── Message grouping state ────────────────────────────────────────
var lastMsgUser  = null;
var lastMsgTime  = null;
var GROUP_GAP_MS = 7 * 60 * 1000;

// ─── Dedup: optimistic sends we haven't seen echoed back yet ───────
// Stored as { user, message, expireAt }
var pendingSent = [];

function pushPending(user, message) {
    pendingSent.push({ user: user, message: message, expireAt: Date.now() + 4000 });
}

function consumePending(user, message) {
    // Purge expired entries first
    var now = Date.now();
    pendingSent = pendingSent.filter(function(p){ return p.expireAt > now; });
    for (var i = 0; i < pendingSent.length; i++) {
        if (pendingSent[i].user === user && pendingSent[i].message === message) {
            pendingSent.splice(i, 1);
            return true; // was our own echo — skip rendering
        }
    }
    return false;
}

// ─── Core: append one message to the container ────────────────────
function appendMessage(user, message) {
    var container = document.getElementById('messages-container');
    if (!container) return;

    var now      = new Date();
    var color    = getUserColor(user);
    var timeStr  = formatTime(now);
    var fullTs   = formatTimestamp(now);
    var sameGroup = (
        user === lastMsgUser &&
        lastMsgTime !== null &&
        (now.getTime() - lastMsgTime.getTime()) < GROUP_GAP_MS
    );

    var div = document.createElement('div');

    if (sameGroup) {
        div.className = 'message-item continued';
        div.innerHTML =
            '<span class="msg-time-compact" title="' + escapeHtml(fullTs) + '">' + timeStr + '</span>' +
            '<div class="msg-text">' + renderMessage(message) + '</div>';
    } else {
        var initial = (user || '?').charAt(0).toUpperCase();
        div.className = 'message-item';
        div.innerHTML =
            '<div class="msg-avatar" style="background-color:' + color + '" title="' + escapeHtml(user) + '">' +
                escapeHtml(initial) +
            '</div>' +
            '<div class="msg-body">' +
                '<div class="msg-header">' +
                    '<span class="msg-username" style="color:' + color + '">' + escapeHtml(user) + '</span>' +
                    '<span class="msg-time" title="' + escapeHtml(fullTs) + '">Today at ' + timeStr + '</span>' +
                '</div>' +
                '<div class="msg-text">' + renderMessage(message) + '</div>' +
            '</div>';
    }

    container.appendChild(div);

    // Auto-scroll only if user is near the bottom (≤ 200px away)
    var gap = container.scrollHeight - container.scrollTop - container.clientHeight;
    if (gap < 200) container.scrollTop = container.scrollHeight;

    lastMsgUser = user;
    lastMsgTime = now;
}

// ─── SignalR Connection ────────────────────────────────────────────
var connection = new signalR.HubConnectionBuilder()
    .withUrl('/messages')
    .withAutomaticReconnect()
    .build();

connection.on('ReceiveMessage', function (user, message) {
    // If this is the hub echoing back our own optimistic message, skip it.
    if (consumePending(user, message)) return;

    // Otherwise it's from another user (or the hub doesn't echo — we never
    // added it to pendingSent, so consumePending returns false and we render).
    appendMessage(user, message);
});

connection.start().catch(function(err) { console.error('SignalR start error:', err); });

// ─── Server / Channel Loading ──────────────────────────────────────
function loadServer(serverId, el) {
    document.querySelectorAll('.server-icon-wrapper').forEach(function(w){ w.classList.remove('active'); });
    if (el) {
        var wr = el.closest ? el.closest('.server-icon-wrapper') : null;
        if (wr) wr.classList.add('active');
    }

    fetch('/Server/GetChannels/' + serverId)
        .then(function(r){ if(!r.ok) throw new Error('HTTP '+r.status); return r.text(); })
        .then(function(html){
            var list = document.querySelector('.channel-list');
            if (list) list.innerHTML = html;
        })
        .catch(function(err){ console.error('GetChannels:', err); });

    var main = document.querySelector('.main-content');
    if (main) main.innerHTML =
        '<div class="empty-main">' +
            '<svg width="64" height="64" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M4 9h16M4 15h16M10 3L8 21M16 3l-2 18"/></svg>' +
            '<span class="empty-main-title">No channel open</span>' +
            '<span class="empty-main-text">Select a channel on the left to start chatting.</span>' +
        '</div>';

    lastMsgUser = null; lastMsgTime = null;
}

function loadChannel(channelId) {
    // Mark active sidebar item
    document.querySelectorAll('.channel-item').forEach(function(c){ c.classList.remove('active'); });
    document.querySelectorAll('.channel-item').forEach(function(item){
        var oc = item.getAttribute('onclick') || '';
        if (oc.indexOf('loadChannel(' + channelId + ')') !== -1) item.classList.add('active');
    });

    fetch('/Server/GetChat?channelId=' + channelId)
        .then(function(r){ if(!r.ok) throw new Error('HTTP '+r.status); return r.text(); })
        .then(function(html){
            var main = document.querySelector('.main-content');
            if (main) { main.innerHTML = html; setupChatInput(channelId); }
        })
        .catch(function(err){ console.error('GetChat:', err); });

    lastMsgUser = null; lastMsgTime = null;
}

// ─── Chat Input ────────────────────────────────────────────────────
function setupChatInput(channelId) {
    var input = document.getElementById('message-input');
    if (!input) return;

    // Remove any old listeners by cloning the node
    var fresh = input.cloneNode(true);
    input.parentNode.replaceChild(fresh, input);
    input = fresh;

    setTimeout(function(){ input.focus(); }, 50);

    input.addEventListener('keydown', function(e) {
        if (e.key !== 'Enter' || e.shiftKey) return;
        e.preventDefault();

        var message = input.value.trim();
        if (!message) return;

        var userIdEl   = document.getElementById('current-user-id');
        var userNameEl = document.getElementById('current-user-name');
        if (!userIdEl) return;

        var userName = (userNameEl && userNameEl.value) ? userNameEl.value : 'Unknown';

        // ── 1. Render immediately (optimistic UI) ──────────────────
        appendMessage(userName, message);

        // ── 2. Register as pending so the hub echo is skipped ──────
        pushPending(userName, message);

        // ── 3. Send via SignalR ────────────────────────────────────
        if (connection.state === signalR.HubConnectionState.Connected) {
            connection.invoke('SendMessage', userIdEl.value, parseInt(channelId, 10), message)
                .catch(function(err){ console.error('SendMessage:', err); });
        } else {
            console.warn('SignalR not connected — message rendered locally only');
        }

        input.value = '';
    });
}

// ─── Minimal markdown renderer ─────────────────────────────────────
function renderMessage(raw) {
    var s = escapeHtml(raw);
    s = s.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
    s = s.replace(/\*(.+?)\*/g,     '<em>$1</em>');
    s = s.replace(/_(.+?)_/g,       '<em>$1</em>');
    s = s.replace(/`([^`]+)`/g,     '<code style="background:#2b2d31;padding:0 4px;border-radius:3px;font-size:.875em;font-family:monospace">$1</code>');
    s = s.replace(/~~(.+?)~~/g,     '<del>$1</del>');
    return s;
}

function escapeHtml(str) {
    return String(str)
        .replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;')
        .replace(/"/g,'&quot;').replace(/'/g,'&#39;');
}