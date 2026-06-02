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
function appendMessage(user, message, pfp, messageId, attachmentFileName, attachmentContentType, parentMessageId, parentContent, parentAuthorName, parentAuthorPfp) {
    const container = document.getElementById('messages-container');
    if (!container) return;

    const now = new Date();
    const color = getUserColor(user);
    const timeStr = formatTime(now);
    const fullTs = formatTimestamp(now);

    // Do not group if it's a reply
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
                    <img src="/Server/GetAttachment?messageId=${messageId}" class="attachment-image" alt="${escapeHtml(attachmentFileName)}" />
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
                        <a href="/Server/GetAttachment?messageId=${messageId}" class="attachment-download-btn" download="${escapeHtml(attachmentFileName)}" title="Download">
                            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3"/>
                            </svg>
                        </a>
                    </div>
                </div>`;
        }
    }

    const div = document.createElement('div');
    if (messageId) {
        div.id = `msg-${messageId}`;
    } else {
        div.setAttribute('data-temp', 'true');
        div.setAttribute('data-content', message); // To find it later
    }

    const currentUserName = document.getElementById('current-user-name')?.value || 'Unknown';
    const isMine = user === currentUserName;

    // Actions bar html
    const actionsBarHtml = messageId ? `
        <div class="message-actions-bar">
            <button class="action-bar-btn reaction-btn" onclick="showQuickReactions(${messageId}, this)" title="Add Reaction">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <circle cx="12" cy="12" r="10"/><path d="M8 14s1.5 2 4 2 4-2 4-2"/><line x1="9" y1="9" x2="9.01" y2="9"/><line x1="15" y1="9" x2="15.01" y2="9"/>
                </svg>
            </button>
            <button class="action-bar-btn reply-btn" onclick="stageReply(${messageId}, '${escapeHtml(user)}')" title="Reply">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <polyline points="9 17 4 12 9 7"/><path d="M20 18v-2a4 4 0 0 0-4-4H4"/>
                </svg>
            </button>
            <button class="action-bar-btn copy-btn" onclick="copyMessageText(${messageId})" title="Copy Text">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <rect x="9" y="9" width="13" height="13" rx="2" ry="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/>
                </svg>
            </button>
            ${isMine ? `
            <button class="action-bar-btn delete-btn" onclick="deleteMessage(${messageId})" title="Delete Message">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/><line x1="10" y1="11" x2="10" y2="17"/><line x1="14" y1="11" x2="14" y2="17"/>
                </svg>
            </button>` : ''}
        </div>` : '';

    // Reply parent row html
    let replyHtml = '';
    if (isReply) {
        const parentAvatarHtml = parentAuthorPfp 
            ? `<img class="reply-parent-avatar" src="data:image/png;base64,${parentAuthorPfp}" />` 
            : `<div class="reply-parent-avatar" style="background-color: ${getUserColor(parentAuthorName)}; width:16px; height:16px; border-radius:50%; display:inline-block;"></div>`;

        replyHtml = `
            <div class="reply-parent-reference" onclick="scrollToMessage(${parentMessageId})">
                ${parentAvatarHtml}
                <span class="reply-parent-username" style="color: ${getUserColor(parentAuthorName)}">${escapeHtml(parentAuthorName)}</span>
                <span class="reply-parent-text">${escapeHtml(parentContent)}</span>
            </div>`;
    }

    if (sameGroup) {
        div.className = 'message-item continued';
        div.innerHTML = `
            ${actionsBarHtml}
            <div class="msg-avatar-spacer">
                <span class="msg-time-compact" title="${escapeHtml(fullTs)}">${timeStr}</span>
            </div>
            <div class="msg-body">
                <div class="msg-text">${renderMessage(message)}</div>
                <div class="reactions-container" id="reactions-${messageId}"></div>
                ${attachmentHtml}
            </div>`;
    } else {
        const initial = (user || '?').charAt(0).toUpperCase();
        const avatarHtml = pfp 
            ? `<img src="data:image/png;base64,${pfp}" />` 
            : `<span>${escapeHtml(initial)}</span>`;

        div.className = 'message-item' + (isReply ? ' has-reply' : '');
        div.innerHTML = `
            ${actionsBarHtml}
            ${replyHtml}
            <div class="msg-avatar" style="background-color: ${pfp ? 'transparent' : color}" title="${escapeHtml(user)}">
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
            </div>`;
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

connection.on('ReceiveMessage', function (user, message, channelId, pfp, messageId, attachmentFileName, attachmentContentType, parentMessageId, parentContent, parentAuthorName, parentAuthorPfp) {
    // Only render if it's for the current channel
    if (channelId && channelId != currentChannelId) return;

    // If this is the hub echoing back our own optimistic message, update the existing element.
    if (consumePending(user, message)) {
        // Find the specific temp message matching the content
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

            // Add the rich actions bar now that we have an ID
            const currentUserName = document.getElementById('current-user-name')?.value || 'Unknown';
            const isMine = user === currentUserName;

            const actionsBar = document.createElement('div');
            actionsBar.className = 'message-actions-bar';
            actionsBar.innerHTML = `
                <button class="action-bar-btn reaction-btn" onclick="showQuickReactions(${messageId}, this)" title="Add Reaction">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <circle cx="12" cy="12" r="10"/><path d="M8 14s1.5 2 4 2 4-2 4-2"/><line x1="9" y1="9" x2="9.01" y2="9"/><line x1="15" y1="9" x2="15.01" y2="9"/>
                    </svg>
                </button>
                <button class="action-bar-btn reply-btn" onclick="stageReply(${messageId}, '${escapeHtml(user)}')" title="Reply">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <polyline points="9 17 4 12 9 7"/><path d="M20 18v-2a4 4 0 0 0-4-4H4"/>
                    </svg>
                </button>
                <button class="action-bar-btn copy-btn" onclick="copyMessageText(${messageId})" title="Copy Text">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <rect x="9" y="9" width="13" height="13" rx="2" ry="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/>
                    </svg>
                </button>
                ${isMine ? `
                <button class="action-bar-btn delete-btn" onclick="deleteMessage(${messageId})" title="Delete Message">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/><line x1="10" y1="11" x2="10" y2="17"/><line x1="14" y1="11" x2="14" y2="17"/>
                    </svg>
                </button>` : ''}`;
            tempMsg.insertBefore(actionsBar, tempMsg.firstChild);

            // Add reactions list placeholder
            const body = tempMsg.querySelector('.msg-body');
            if (body) {
                const rList = document.createElement('div');
                rList.className = 'reactions-container';
                rList.id = `reactions-${messageId}`;
                body.insertBefore(rList, body.querySelector('.msg-attachment') || null);
            }
        }
        return;
    }

    // Otherwise it's from another user (or the hub doesn't echo — we never
    // added it to pendingSent, so consumePending returns false and we render).
    appendMessage(user, message, pfp, messageId, attachmentFileName, attachmentContentType, parentMessageId, parentContent, parentAuthorName, parentAuthorPfp);
});

connection.on('ReceiveError', function (error) {
    alert('Error: ' + error);
    // Remove the optimistic message if it exists
    const tempMsgs = document.querySelectorAll('.message-item[data-temp="true"]');
    if (tempMsgs.length > 0) {
        tempMsgs[tempMsgs.length - 1].remove();
    }
});

connection.start()
    .then(function() {
        console.log('SignalR connected');
        if (currentChannelId) {
            connection.invoke('JoinChannel', parseInt(currentChannelId, 10))
                .catch(function(err){ console.error('JoinChannel error:', err); });
        }
    })
    .catch(function(err) { console.error('SignalR start error:', err); });

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

    currentChannelId = channelId;
    stagedReplyToId = null;
    const replyBar = document.getElementById('reply-preview-bar');
    if (replyBar) replyBar.style.display = 'none';

    fetch('/Server/GetChat?channelId=' + channelId)
        .then(function(r){ if(!r.ok) throw new Error('HTTP '+r.status); return r.text(); })
        .then(function(html){
            var main = document.querySelector('.main-content');
            if (main) { 
                main.innerHTML = html; 
                setupChatInput(channelId);
                if (connection.state === signalR.HubConnectionState.Connected) {
                    connection.invoke('JoinChannel', parseInt(channelId, 10))
                        .catch(function(err){ console.error('JoinChannel error:', err); });
                }
            }
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

    const attachmentInput = document.getElementById('attachment-input');
    const uploadPreviewArea = document.getElementById('upload-preview-area');
    const previewContainer = document.getElementById('preview-container');

    let selectedFile = null;

    if (attachmentInput) {
        // Reset file input in case it was dirty
        attachmentInput.value = '';
        selectedFile = null;
        if (uploadPreviewArea) uploadPreviewArea.style.display = 'none';

        attachmentInput.addEventListener('change', function() {
            if (attachmentInput.files && attachmentInput.files[0]) {
                selectedFile = attachmentInput.files[0];
                renderUploadPreview(selectedFile);
            }
        });
    }

    // Drag & Drop Event Handling
    const mainContent = document.querySelector('.main-content');
    const dragDropOverlay = document.getElementById('drag-drop-overlay');

    if (mainContent && dragDropOverlay) {
        let dragCounter = 0;

        mainContent.addEventListener('dragenter', function(e) {
            e.preventDefault();
            dragCounter++;
            if (dragCounter === 1) {
                dragDropOverlay.style.display = 'flex';
            }
        });

        mainContent.addEventListener('dragover', function(e) {
            e.preventDefault();
        });

        mainContent.addEventListener('dragleave', function(e) {
            e.preventDefault();
            dragCounter--;
            if (dragCounter === 0) {
                dragDropOverlay.style.display = 'none';
            }
        });

        mainContent.addEventListener('drop', function(e) {
            e.preventDefault();
            dragCounter = 0;
            dragDropOverlay.style.display = 'none';

            if (e.dataTransfer.files && e.dataTransfer.files[0]) {
                selectedFile = e.dataTransfer.files[0];
                renderUploadPreview(selectedFile);
                if (attachmentInput) {
                    attachmentInput.value = ''; // Clear file input in case it was set
                }
            }
        });
    }

    function renderUploadPreview(file) {
        if (!uploadPreviewArea || !previewContainer) return;
        previewContainer.innerHTML = '';

        const isImage = file.type.startsWith('image/');
        const fileSizeKB = (file.size / 1024).toFixed(1);

        const previewItem = document.createElement('div');
        previewItem.style.position = 'relative';
        previewItem.style.display = 'flex';
        previewItem.style.alignItems = 'center';
        previewItem.style.gap = '12px';

        if (isImage) {
            const img = document.createElement('img');
            img.src = URL.createObjectURL(file);
            img.className = 'preview-thumbnail';
            img.onload = function() {
                URL.revokeObjectURL(img.src); // release memory
            };
            previewItem.appendChild(img);
        } else {
            const iconDiv = document.createElement('div');
            iconDiv.className = 'preview-file-icon';
            iconDiv.innerHTML = `
                <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                    <polyline points="14 2 14 8 20 8" />
                </svg>`;
            previewItem.appendChild(iconDiv);
        }

        const infoDiv = document.createElement('div');
        infoDiv.className = 'preview-info';
        infoDiv.innerHTML = `
            <span class="preview-filename" title="${escapeHtml(file.name)}">${escapeHtml(file.name)}</span>
            <span class="preview-filesize">${fileSizeKB} KB</span>`;
        previewItem.appendChild(infoDiv);

        const removeBtn = document.createElement('button');
        removeBtn.className = 'preview-remove-btn';
        removeBtn.innerHTML = '×';
        removeBtn.type = 'button';
        removeBtn.addEventListener('click', function() {
            selectedFile = null;
            attachmentInput.value = '';
            uploadPreviewArea.style.display = 'none';
            previewContainer.innerHTML = '';
            input.focus();
        });
        previewItem.appendChild(removeBtn);

        previewContainer.appendChild(previewItem);
        uploadPreviewArea.style.display = 'flex';
        
        // Auto-expand preview area in case chat scrolls
        const chatContainer = document.getElementById('messages-container');
        if (chatContainer) {
            chatContainer.scrollTop = chatContainer.scrollHeight;
        }
    }

    // Auto-expand logic
    function autoResize() {
        // Collapse to measure true content height
        input.style.height = '0px';
        var scrollH = input.scrollHeight;
        var maxH = Math.floor(window.innerHeight * 0.5);
        if (scrollH > maxH) {
            input.style.height = maxH + 'px';
            input.style.overflowY = 'auto';
        } else {
            input.style.height = scrollH + 'px';
            input.style.overflowY = 'hidden';
        }
    }
    input.addEventListener('input', autoResize);

    input.addEventListener('keydown', function(e) {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();

            var message = input.value.trim();
            if (!message && !selectedFile) return;

            var userIdEl   = document.getElementById('current-user-id');
            var userNameEl = document.getElementById('current-user-name');
            var userPfpEl  = document.getElementById('current-user-pfp');
            if (!userIdEl) return;

            var userName = (userNameEl && userNameEl.value) ? userNameEl.value : 'Unknown';
            var userPfp  = (userPfpEl && userPfpEl.value) ? userPfpEl.value : null;

            if (selectedFile || stagedReplyToId) {
                // If there is a file or a reply, we upload via HTTP POST instead of SignalR direct call
                var formData = new FormData();
                formData.append('content', message);
                formData.append('channelId', parseInt(channelId, 10));
                formData.append('userId', userIdEl.value);
                
                if (selectedFile) {
                    formData.append('attachment', selectedFile);
                }
                if (stagedReplyToId) {
                    formData.append('parentMessageId', stagedReplyToId);
                }

                // Show visual feedback that it is uploading
                if (uploadPreviewArea && selectedFile) {
                    uploadPreviewArea.style.opacity = '0.5';
                }

                fetch('/Server/PostMessage', {
                    method: 'POST',
                    body: formData
                })
                .then(function(r) {
                    if (!r.ok) {
                        return r.text().then(function(err) { throw new Error(err || 'Failed to send message'); });
                    }
                    return r.json();
                })
                .then(function(data) {
                    // Success! Clear state
                    selectedFile = null;
                    stagedReplyToId = null;
                    if (attachmentInput) attachmentInput.value = '';
                    if (uploadPreviewArea) {
                        uploadPreviewArea.style.display = 'none';
                        uploadPreviewArea.style.opacity = '1';
                    }
                    const replyBar = document.getElementById('reply-preview-bar');
                    if (replyBar) replyBar.style.display = 'none';
                })
                .catch(function(err) {
                    alert('Error sending message: ' + err.message);
                    if (uploadPreviewArea) {
                        uploadPreviewArea.style.opacity = '1';
                    }
                });

            } else {
                // ── 1. Render immediately (optimistic UI) ──────────────────
                appendMessage(userName, message, userPfp);

                // ── 2. Register as pending so the hub echo is skipped ──────
                pushPending(userName, message);

                // ── 3. Send via SignalR ────────────────────────────────────
                if (connection.state === signalR.HubConnectionState.Connected) {
                    connection.invoke('SendMessage', userIdEl.value, parseInt(channelId, 10), message)
                        .catch(function(err){ console.error('SendMessage:', err); });
                } else {
                    console.warn('SignalR not connected — message rendered locally only');
                }
            }

            input.value = '';
            input.style.height = 'auto';
            input.style.overflowY = 'hidden';
        }
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


function deleteMessage(messageId){
    if (confirm("Are you sure you want to delete this message?")){
        connection.invoke("DeleteMessage", messageId)
            .catch(err=> console.error(err));
    }
}

connection.on("MessageDeleted", function (messageId){
    const msgElement = document.getElementById("msg-" + messageId);
    if(msgElement){
        msgElement.remove();
    }
});

// ─── Clipboard Copy Helper ─────────────────────────────────────────
function copyMessageText(messageId) {
    const msgEl = document.querySelector(`#msg-${messageId} .msg-text`);
    if (msgEl) {
        const text = msgEl.innerText;
        navigator.clipboard.writeText(text).then(function() {
            console.log('Copied message text:', text);
        }).catch(function(err) {
            console.error('Failed to copy text: ', err);
        });
    }
}

// ─── Reply Helpers ─────────────────────────────────────────────────
function stageReply(messageId, username) {
    stagedReplyToId = messageId;
    const replyBar = document.getElementById('reply-preview-bar');
    const targetUser = document.getElementById('reply-target-username');
    if (replyBar && targetUser) {
        targetUser.innerText = '@' + username;
        replyBar.style.display = 'flex';
    }
    const input = document.getElementById('message-input');
    if (input) input.focus();
}

function cancelReply() {
    stagedReplyToId = null;
    const replyBar = document.getElementById('reply-preview-bar');
    if (replyBar) {
        replyBar.style.display = 'none';
    }
    const input = document.getElementById('message-input');
    if (input) input.focus();
}

// ─── Reactions Helpers ─────────────────────────────────────────────
function showQuickReactions(messageId, btn) {
    // Remove any existing quick emoji popovers
    const existing = document.querySelector('.quick-emoji-popover');
    if (existing) {
        existing.remove();
        if (existing.getAttribute('data-message-id') === String(messageId)) return;
    }

    const popover = document.createElement('div');
    popover.className = 'quick-emoji-popover';
    popover.setAttribute('data-message-id', messageId);

    const emojis = ['👍', '❤️', '😂', '🔥'];
    emojis.forEach(function(emoji) {
        const btnEmoji = document.createElement('button');
        btnEmoji.className = 'quick-emoji-btn';
        btnEmoji.innerText = emoji;
        btnEmoji.type = 'button';
        btnEmoji.addEventListener('click', function() {
            toggleReaction(messageId, emoji);
            popover.remove();
        });
        popover.appendChild(btnEmoji);
    });

    document.body.appendChild(popover);

    // Position popover perfectly above the button
    const rect = btn.getBoundingClientRect();
    popover.style.left = (rect.left + window.scrollX - 40) + 'px';
    popover.style.top = (rect.top + window.scrollY - 46) + 'px';

    // Close when clicking outside
    setTimeout(function() {
        const closeHandler = function(e) {
            if (!popover.contains(e.target) && e.target !== btn) {
                popover.remove();
                document.removeEventListener('click', closeHandler);
            }
        };
        document.addEventListener('click', closeHandler);
    }, 50);
}

function toggleReaction(messageId, emoji) {
    const formData = new FormData();
    formData.append('messageId', messageId);
    formData.append('emoji', emoji);

    // Invoke via HTTP endpoint (to teach backend implementation)
    fetch('/Server/ToggleReaction', {
        method: 'POST',
        body: formData
    })
    .then(function(r) {
        if (!r.ok) {
            console.warn('Reaction endpoint not yet handled by backend.');
        }
    })
    .catch(function(err) {
        console.error('Reaction toggle error:', err);
    });
}

function updateReactionInUI(messageId, emoji, count, hasReacted) {
    const container = document.getElementById(`reactions-${messageId}`);
    if (!container) return;

    let bubble = null;
    const bubbles = container.querySelectorAll('.reaction-bubble');
    for (let b of bubbles) {
        if (b.getAttribute('data-emoji') === emoji) {
            bubble = b;
            break;
        }
    }

    if (count <= 0) {
        if (bubble) bubble.remove();
        return;
    }

    if (!bubble) {
        bubble = document.createElement('div');
        bubble.className = 'reaction-bubble';
        bubble.setAttribute('data-emoji', emoji);
        bubble.addEventListener('click', function() {
            toggleReaction(messageId, emoji);
        });
        container.appendChild(bubble);
    }

    bubble.className = 'reaction-bubble' + (hasReacted ? ' active' : '');
    bubble.innerHTML = emoji + ' <span class="reaction-count">' + count + '</span>';
}

// ─── Real-time Reaction Sync via SignalR ───────────────────────────
connection.on('ReactionToggled', function (messageId, emoji, count, hasReacted) {
    updateReactionInUI(messageId, emoji, count, hasReacted);
});