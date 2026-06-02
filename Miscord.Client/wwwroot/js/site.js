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
var currentChannelId = null;
var stagedReplyToId = null;

// ─── Dedup: optimistic sends we haven't seen echoed back yet ───────
var pendingSent = [];

function pushPending(user, message) {
    pendingSent.push({ user: user, message: message, expireAt: Date.now() + 4000 });
}

function consumePending(user, message) {
    var now = Date.now();
    pendingSent = pendingSent.filter(function(p){ return p.expireAt > now; });
    for (var i = 0; i < pendingSent.length; i++) {
        if (pendingSent[i].user === user && pendingSent[i].message === message) {
            pendingSent.splice(i, 1);
            return true; 
        }
    }
    return false;
}

// ─── Core: append one message to the container ────────────────────
function appendMessage(user, message, pfp, messageId, attachmentFileName, attachmentContentType, parentMessageId, parentContent, parentAuthorRaw, userIdFallback) {
    const container = document.getElementById('messages-container');
    if (!container) return;

    const now = new Date();
    const color = getUserColor(user);
    const timeStr = formatTime(now);
    const fullTs = formatTimestamp(now);

    // Split parentAuthorName|parentAuthorPfp|currentUserId
    let parentAuthorName = '';
    let parentAuthorPfp = '';
    let currentUserId = userIdFallback || '';
    
    if (parentAuthorRaw && parentAuthorRaw.includes('|')) {
        const parts = parentAuthorRaw.split('|');
        parentAuthorName = parts[0];
        parentAuthorPfp = parts[1];
        // Only take userId from packed string if it's actually there
        if (parts[2]) currentUserId = parts[2];
    }

    const isReply = !!parentMessageId;
    const sameGroup = (
        user === lastMsgUser &&
        lastMsgTime !== null &&
        (now.getTime() - lastMsgTime.getTime()) < GROUP_GAP_MS &&
        !isReply
    );

    let attachmentHtml = '';
    if (attachmentFileName) {
        if (attachmentContentType && attachmentContentType.startsWith('image/')) {
            attachmentHtml = `
                <div class="msg-attachment">
                    <img src="/Server/GetAttachment?messageId=${messageId}" class="attachment-image" alt="${escapeHtml(attachmentFileName)}" loading="lazy" />
                </div>`;
        } else {
            attachmentHtml = `
                <div class="msg-attachment">
                    <div class="attachment-file-box">
                        <svg class="file-icon" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                            <polyline points="14 2 14 8 20 8" />
                        </svg>
                        <div class="attachment-file-info">
                            <a href="/Server/GetAttachment?messageId=${messageId}" class="attachment-file-link" target="_blank">${escapeHtml(attachmentFileName)}</a>
                            <span class="attachment-file-size">Attachment</span>
                        </div>
                    </div>
                </div>`;
        }
    } else if (messageId === undefined && attachmentFileName === null) {
        attachmentHtml = `<div class="msg-attachment" style="opacity: 0.5;">Uploading file...</div>`;
    }

    const div = document.createElement('div');
    if (messageId) div.id = `msg-${messageId}`;
    else {
        div.setAttribute('data-temp', 'true');
        div.setAttribute('data-content', message);
    }

    const currentUserName = document.getElementById('current-user-name')?.value || 'Unknown';
    const isMine = user === currentUserName;

    const actionsBarHtml = messageId ? `
        <div class="message-actions-bar">
            <button class="action-bar-btn reaction-btn" onclick="showQuickReactions(${messageId}, this)" title="Add Reaction"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="M8 14s1.5 2 4 2 4-2 4-2"/><line x1="9" y1="9" x2="9.01" y2="9"/><line x1="15" y1="9" x2="15.01" y2="9"/></svg></button>
            <button class="action-bar-btn reply-btn" onclick="stageReply(${messageId}, '${escapeHtml(user)}')" title="Reply"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="9 17 4 12 9 7"/><path d="M20 18v-2a4 4 0 0 0-4-4H4"/></svg></button>
            <button class="action-bar-btn copy-btn" onclick="copyMessageText(${messageId})" title="Copy Text"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg></button>
            ${isMine ? `<button class="action-bar-btn delete-btn" onclick="deleteMessage(${messageId})" title="Delete Message"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/><line x1="10" y1="11" x2="10" y2="17"/><line x1="14" y1="11" x2="14" y2="17"/></svg></button>` : ''}
        </div>` : '';

    let replyHtml = '';
    if (isReply) {
        // Find the user ID of the parent from the UI if possible to use URL avatar
        let parentUid = '';
        if (parentAuthorRaw && parentAuthorRaw.includes('||')) {
            parentUid = parentAuthorRaw.split('||')[1];
        } else {
            const parentEl = document.getElementById('msg-' + parentMessageId);
            if (parentEl) {
                parentUid = parentEl.querySelector('.msg-avatar')?.getAttribute('data-userid') || '';
            }
        }

        const parentAvatarHtml = (parentAuthorPfp || parentUid)
            ? `<img class="reply-parent-avatar" src="/Server/GetProfilePicture?userId=${parentUid}" loading="lazy" />` 
            : `<div class="reply-parent-avatar" style="background-color: ${getUserColor(parentAuthorName)}; width:16px; height:16px; border-radius:50%; display:inline-block;"></div>`;

        replyHtml = `
            <div class="reply-parent-reference" ${parentMessageId ? `onclick="scrollToMessage(${parentMessageId})"` : ''}>
                ${parentAvatarHtml}
                <span class="reply-parent-username" style="color: ${getUserColor(parentAuthorName)}">${escapeHtml(parentAuthorName)}</span>
                <span class="reply-parent-text">${escapeHtml(parentContent)}</span>
            </div>`;
    }

    if (sameGroup) {
        div.className = 'message-item continued';
        div.innerHTML = `
            ${actionsBarHtml}
            ${replyHtml}
            <div class="message-content-row">
                <div class="msg-avatar-spacer">
                    <span class="msg-time-compact" title="${escapeHtml(fullTs)}">${timeStr}</span>
                </div>
                <div class="msg-body">
                    <div class="msg-text">${renderMessage(message)}</div>
                    <div class="reactions-container" id="reactions-${messageId}"></div>
                    ${attachmentHtml}
                </div>
            </div>`;
    } else {
        const initial = (user || '?').charAt(0).toUpperCase();
        const avatarHtml = currentUserId 
            ? `<img src="/Server/GetProfilePicture?userId=${currentUserId}" loading="lazy" />` 
            : `<span>${escapeHtml(initial)}</span>`;

        div.className = 'message-item' + (isReply ? ' has-reply' : '');
        div.innerHTML = `
            ${actionsBarHtml}
            ${replyHtml}
            <div class="message-content-row">
                <div class="msg-avatar" data-userid="${currentUserId}" style="background-color: ${currentUserId ? 'transparent' : color}" title="${escapeHtml(user)}">
                    ${avatarHtml}
                </div>
                <div class="msg-body">
                    <div class="msg-header">
                        <span class="msg-username" style="color: ${color}">${escapeHtml(user)}</span>
                        <span class="msg-time" title="${escapeHtml(fullTs)}">Today at ${timeStr}</span>
                    </div>
                    <div class="msg-text">${renderMessage(message)}</div>
                    <div class="reactions-container" id="reactions-${messageId}"></div>
                    ${attachmentHtml}
                </div>
            </div>`;
    }

    container.appendChild(div);
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

connection.on('ReceiveMessage', function (user, message, channelId, pfp, messageId, attachmentFileName, attachmentContentType, parentMessageId, parentContent, parentAuthorRaw) {
    if (channelId && channelId != currentChannelId) return;

    if (consumePending(user, message)) {
        const tempMsgs = document.querySelectorAll('.message-item[data-temp="true"]');
        let tempMsg = null;
        for (let m of tempMsgs) {
            if (m.getAttribute('data-content') === message) {
                tempMsg = m;
                break;
            }
        }

        if (tempMsg) {
            tempMsg.id = `msg-${messageId}`;
            tempMsg.removeAttribute('data-temp');
            tempMsg.removeAttribute('data-content');

            const currentUserName = document.getElementById('current-user-name')?.value || 'Unknown';
            const isMine = user === currentUserName;

            const actionsBar = document.createElement('div');
            actionsBar.className = 'message-actions-bar';
            actionsBar.innerHTML = `
                <button class="action-bar-btn reaction-btn" onclick="showQuickReactions(${messageId}, this)" title="Add Reaction"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="M8 14s1.5 2 4 2 4-2 4-2"/><line x1="9" y1="9" x2="9.01" y2="9"/><line x1="15" y1="9" x2="15.01" y2="9"/></svg></button>
                <button class="action-bar-btn reply-btn" onclick="stageReply(${messageId}, '${escapeHtml(user)}')" title="Reply"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="9 17 4 12 9 7"/><path d="M20 18v-2a4 4 0 0 0-4-4H4"/></svg></button>
                <button class="action-bar-btn copy-btn" onclick="copyMessageText(${messageId})" title="Copy Text"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg></button>
                ${isMine ? `<button class="action-bar-btn delete-btn" onclick="deleteMessage(${messageId})" title="Delete Message"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/><line x1="10" y1="11" x2="10" y2="17"/><line x1="14" y1="11" x2="14" y2="17"/></svg></button>` : ''}`;
            tempMsg.insertBefore(actionsBar, tempMsg.firstChild);

            if (parentMessageId) {
                const ref = tempMsg.querySelector('.reply-parent-reference');
                if (ref) ref.setAttribute('onclick', `scrollToMessage(${parentMessageId})`);
            }

            if (attachmentFileName) {
                const body = tempMsg.querySelector('.msg-body');
                if (body) {
                    const oldAttach = body.querySelector('.msg-attachment');
                    if (oldAttach) oldAttach.remove();

                    let attachmentHtml = `
                        <div class="msg-attachment">
                            ${attachmentContentType && attachmentContentType.startsWith('image/') 
                                ? `<img src="/Server/GetAttachment?messageId=${messageId}" class="attachment-image" alt="${escapeHtml(attachmentFileName)}" loading="lazy" />`
                                : `<div class="attachment-file-box">
                                    <svg class="file-icon" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" /><polyline points="14 2 14 8 20 8" /></svg>
                                    <div class="attachment-file-info">
                                        <a href="/Server/GetAttachment?messageId=${messageId}" class="attachment-file-link" target="_blank">${escapeHtml(attachmentFileName)}</a>
                                        <span class="attachment-file-size">Attachment</span>
                                    </div>
                                   </div>`
                            }
                        </div>`;
                    body.insertAdjacentHTML('beforeend', attachmentHtml);
                }
            }

            const bodyDiv = tempMsg.querySelector('.msg-body');
            if (bodyDiv && !document.getElementById(`reactions-${messageId}`)) {
                const rList = document.createElement('div');
                rList.className = 'reactions-container';
                rList.id = `reactions-${messageId}`;
                bodyDiv.insertBefore(rList, bodyDiv.querySelector('.msg-attachment') || null);
            }
        }
        return;
    }

    appendMessage(user, message, pfp, messageId, attachmentFileName, attachmentContentType, parentMessageId, parentContent, parentAuthorRaw);
});

connection.on('ReceiveError', function (error) {
    alert('Error: ' + error);
    const tempMsgs = document.querySelectorAll('.message-item[data-temp="true"]');
    if (tempMsgs.length > 0) tempMsgs[tempMsgs.length - 1].remove();
});

connection.start().then(function() {
    if (currentChannelId) connection.invoke('JoinChannel', parseInt(currentChannelId, 10));
});

// ─── Server / Channel Loading ──────────────────────────────────────
function loadServer(serverId, el) {
    document.querySelectorAll('.server-icon-wrapper').forEach(w => w.classList.remove('active'));
    if (el) el.closest('.server-icon-wrapper')?.classList.add('active');

    fetch('/Server/GetChannels/' + serverId)
        .then(r => r.text())
        .then(html => {
            const list = document.querySelector('.channel-list');
            if (list) list.innerHTML = html;
        });

    const main = document.querySelector('.main-content');
    if (main) main.innerHTML = '<div class="empty-main"><span class="empty-main-title">No channel open</span></div>';
    lastMsgUser = null; lastMsgTime = null;
}

function loadChannel(channelId) {
    document.querySelectorAll('.channel-item').forEach(c => c.classList.remove('active'));
    document.querySelectorAll('.channel-item').forEach(item => {
        if ((item.getAttribute('onclick') || '').includes('loadChannel(' + channelId + ')')) item.classList.add('active');
    });

    currentChannelId = channelId;
    stagedReplyToId = null;
    const rb = document.getElementById('reply-preview-bar');
    if (rb) rb.style.display = 'none';

    fetch('/Server/GetChat?channelId=' + channelId)
        .then(r => r.text())
        .then(html => {
            const main = document.querySelector('.main-content');
            if (main) { 
                main.innerHTML = html; 
                setupChatInput(channelId);
                const container = document.getElementById('messages-container');
                if (container) container.scrollTop = container.scrollHeight;
                if (connection.state === 'Connected' || connection.state === 1) connection.invoke('JoinChannel', parseInt(channelId, 10));
            }
        });
    lastMsgUser = null; lastMsgTime = null;
}

// ─── Chat Input ────────────────────────────────────────────────────
function setupChatInput(channelId) {
    var input = document.getElementById('message-input');
    if (!input) return;

    var fresh = input.cloneNode(true);
    input.parentNode.replaceChild(fresh, input);
    input = fresh;
    setTimeout(() => input.focus(), 50);

    const attachmentInput = document.getElementById('attachment-input');
    const uploadPreviewArea = document.getElementById('upload-preview-area');
    const previewContainer = document.getElementById('preview-container');

    let selectedFile = null;

    if (attachmentInput) {
        attachmentInput.addEventListener('change', () => {
            if (attachmentInput.files?.[0]) {
                selectedFile = attachmentInput.files[0];
                renderUploadPreview(selectedFile);
            }
        });
    }

    function renderUploadPreview(file) {
        previewContainer.innerHTML = '';
        const isImage = file.type.startsWith('image/');
        const previewItem = document.createElement('div');
        previewItem.style = 'display:flex;align-items:center;gap:12px;position:relative;';

        if (isImage) {
            const img = document.createElement('img');
            img.src = URL.createObjectURL(file);
            img.className = 'preview-thumbnail';
            img.onload = () => URL.revokeObjectURL(img.src);
            previewItem.appendChild(img);
        } else {
            previewItem.insertAdjacentHTML('beforeend', '<div class="preview-file-icon">FILE</div>');
        }

        previewItem.insertAdjacentHTML('beforeend', `<div class="preview-info"><span class="preview-filename">${escapeHtml(file.name)}</span></div>`);
        const removeBtn = document.createElement('button');
        removeBtn.className = 'preview-remove-btn'; removeBtn.innerText = '×';
        removeBtn.onclick = () => { selectedFile = null; uploadPreviewArea.style.display = 'none'; };
        previewItem.appendChild(removeBtn);
        previewContainer.appendChild(previewItem);
        uploadPreviewArea.style.display = 'flex';
    }

    input.addEventListener('keydown', e => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            const message = input.value.trim();
            if (!message && !selectedFile) return;

            const userId = document.getElementById('current-user-id')?.value;
            const userName = document.getElementById('current-user-name')?.value || 'Unknown';
            const userPfp = document.getElementById('current-user-pfp')?.value;

            let parentContent = null, parentAuthorRaw = null;
            if (stagedReplyToId) {
                const pEl = document.getElementById('msg-' + stagedReplyToId);
                if (pEl) {
                    parentContent = pEl.querySelector('.msg-text')?.innerText || '';
                    const parentName = pEl.querySelector('.msg-username')?.innerText || 'Unknown';
                    const parentUid = pEl.querySelector('.msg-avatar')?.getAttribute('data-userid') || '';
                    parentAuthorRaw = parentName + "||" + parentUid;
                }
            }

            appendMessage(userName, message, userPfp, undefined, selectedFile ? null : undefined, null, stagedReplyToId, parentContent, parentAuthorRaw, userId);
            pushPending(userName, message);

            if (selectedFile || stagedReplyToId) {
                const fd = new FormData();
                fd.append('content', message);
                fd.append('channelId', channelId);
                fd.append('userId', userId);
                if (selectedFile) fd.append('attachment', selectedFile);
                if (stagedReplyToId) fd.append('parentMessageId', stagedReplyToId);

                fetch('/Server/PostMessage', { method: 'POST', body: fd }).then(r => r.json()).then(() => {
                    selectedFile = null; stagedReplyToId = null;
                    if (uploadPreviewArea) uploadPreviewArea.style.display = 'none';
                    const rpb = document.getElementById('reply-preview-bar');
                    if (rpb) rpb.style.display = 'none';
                });
            } else {
                if (connection.state === 'Connected' || connection.state === 1) {
                    connection.invoke('SendMessage', userId, parseInt(channelId), message);
                }
            }
            input.value = '';
        }
    });
}

function renderMessage(raw) {
    let s = escapeHtml(raw);
    s = s.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
    s = s.replace(/\*(.+?)\*/g, '<em>$1</em>');
    s = s.replace(/`([^`]+)`/g, '<code style="background:#2b2d31;padding:0 4px;border-radius:3px;">$1</code>');
    return s;
}

function escapeHtml(str) {
    return String(str).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

function deleteMessage(id) { if(confirm("Delete?")) connection.invoke("DeleteMessage", id); }

connection.on("MessageDeleted", id => document.getElementById("msg-" + id)?.remove());

function copyMessageText(id) { navigator.clipboard.writeText(document.querySelector(`#msg-${id} .msg-text`).innerText); }

function scrollToMessage(id) {
    const el = document.getElementById('msg-' + id);
    if (el) {
        el.scrollIntoView({ behavior: 'smooth', block: 'center' });
        el.classList.add('replying-to');
        setTimeout(() => el.classList.remove('replying-to'), 2000);
    }
}

function stageReply(id, user) {
    stagedReplyToId = id;
    const tun = document.getElementById('reply-target-username');
    if (tun) tun.innerText = '@' + user;
    const rpb = document.getElementById('reply-preview-bar');
    if (rpb) rpb.style.display = 'flex';
    document.getElementById('msg-' + id)?.classList.add('replying-to');
    document.getElementById('message-input').focus();
}

function cancelReply() {
    document.getElementById('msg-' + stagedReplyToId)?.classList.remove('replying-to');
    stagedReplyToId = null;
    const rpb = document.getElementById('reply-preview-bar');
    if (rpb) rpb.style.display = 'none';
}

function showQuickReactions(id, btn) {
    const pop = document.createElement('div');
    pop.className = 'quick-emoji-popover';
    ['👍', '❤️', '😂', '🔥'].forEach(emoji => {
        const b = document.createElement('button');
        b.className = 'quick-emoji-btn'; b.innerText = emoji;
        b.onclick = () => { toggleReaction(id, emoji); pop.remove(); };
        pop.appendChild(b);
    });
    document.body.appendChild(pop);
    const r = btn.getBoundingClientRect();
    pop.style.left = (r.left + window.scrollX - 40) + 'px';
    pop.style.top = (r.top + window.scrollY - 46) + 'px';
}

function toggleReaction(id, emoji) {
    const fd = new FormData(); fd.append('messageId', id); fd.append('emoji', emoji);
    fetch('/Server/ToggleReaction', { method: 'POST', body: fd });
}

function updateReactionInUI(id, emoji, count, has) {
    const c = document.getElementById(`reactions-${id}`);
    if (!c) return;
    let b = Array.from(c.children).find(x => x.getAttribute('data-emoji') === emoji);
    if (count <= 0) { b?.remove(); return; }
    if (!b) {
        b = document.createElement('div'); b.className = 'reaction-bubble';
        b.setAttribute('data-emoji', emoji); b.onclick = () => toggleReaction(id, emoji);
        c.appendChild(b);
    }
    b.className = 'reaction-bubble' + (has ? ' active' : '');
    b.innerHTML = emoji + ' <span class="reaction-count">' + count + '</span>';
}

connection.on('ReactionToggled', (id, em, cnt, has) => updateReactionInUI(id, em, cnt, has));