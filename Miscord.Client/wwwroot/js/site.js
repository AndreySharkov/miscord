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

        const hasParentPfp = parentAuthorPfp && parentAuthorPfp.trim().length > 0;
        const parentAvatarHtml = hasParentPfp
            ? `<img class="reply-parent-avatar" src="data:image/png;base64,${parentAuthorPfp}" loading="lazy" />` 
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
        const hasPfp = pfp && pfp.trim().length > 0;
        const avatarHtml = hasPfp 
            ? `<img src="data:image/png;base64,${pfp}" loading="lazy" />` 
            : `<span>${escapeHtml(initial)}</span>`;

        div.className = 'message-item' + (isReply ? ' has-reply' : '');
        div.innerHTML = `
            ${actionsBarHtml}
            ${replyHtml}
            <div class="message-content-row">
                <div class="msg-avatar" data-userid="${currentUserId}" style="background-color: ${hasPfp ? 'transparent' : color}" title="${escapeHtml(user)}">
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
    if (!serverId) return;
    document.querySelectorAll('.server-icon-wrapper').forEach(w => w.classList.remove('active'));
    if (el) el.closest('.server-icon-wrapper')?.classList.add('active');

    fetch('/Server/GetChannels/' + serverId)
        .then(r => r.text())
        .then(html => {
            const list = document.querySelector('.channel-list');
            if (list) {
                list.innerHTML = html;
                
                // Update Server Header
                const newName = document.getElementById('current-server-name-val')?.value;
                const headerDropdown = document.getElementById('server-header-dropdown');
                if (headerDropdown) {
                    headerDropdown.style.display = 'block';
                    const headerName = headerDropdown.querySelector('.sidebar-header-name');
                    if (headerName && newName) headerName.innerText = newName;
                }

                // Show/Hide Server Settings and Leave Server based on Ownership or Admin status
                const ownerId = document.getElementById('current-server-owner-id-val')?.value;
                const currentUserId = document.getElementById('current-user-id')?.value;
                const isAdmin = document.getElementById('current-server-is-admin-val')?.value === 'true';
                
                const settingsItem = document.getElementById('server-settings-dropdown-item');
                if (settingsItem) {
                    settingsItem.style.display = (ownerId === currentUserId || isAdmin) ? 'flex' : 'none';
                }

                const leaveItem = document.getElementById('leave-server-dropdown-item');
                if (leaveItem) {
                    leaveItem.style.display = 'flex';
                }

                // Automatically click the first channel if available
                const firstChannel = list.querySelector('.channel-item');
                if (firstChannel) {
                    firstChannel.click();
                }
            }
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

    // ─── Member Context Menu Logic ─────────────────────────────────────────
    let currentMemberId = null;
    let currentMemberName = null;
    let currentServerIdForMember = null;

// ─── UI Utility: Close all floating menus ─────────────────────────
function closeAllMenus(exceptElement) {
    // 1. Member context menu
    const memberMenu = document.getElementById('member-context-menu');
    if (memberMenu && memberMenu !== exceptElement) memberMenu.style.display = 'none';

    // 2. Server dropdown
    const serverMenu = document.getElementById('server-dropdown-menu');
    const chevron = document.getElementById('header-chevron');
    if (serverMenu && serverMenu !== exceptElement && serverMenu.classList.contains('open')) {
        serverMenu.classList.remove('open');
        if (chevron) chevron.style.transform = 'rotate(0deg)';
    }

    // 3. Role picker
    const rolePicker = document.getElementById('member-role-picker');
    if (rolePicker && rolePicker !== exceptElement) rolePicker.remove();

    // 4. Quick reactions
    const emojiPop = document.querySelector('.quick-emoji-popover');
    if (emojiPop && emojiPop !== exceptElement) emojiPop.remove();
}

// Global click listener to close menus when clicking outside
document.addEventListener('click', (e) => {
    // Check if we clicked inside a menu or its trigger
    const isMemberMenu = e.target.closest('#member-context-menu');
    const isServerHeader = e.target.closest('.sidebar-header');
    const isServerMenu = e.target.closest('#server-dropdown-menu');
    const isRolePicker = e.target.closest('#member-role-picker');
    const isEmojiPop = e.target.closest('.quick-emoji-popover');
    const isAddRoleBtn = e.target.closest('.ss-add-role-btn');
    const isReactionBtn = e.target.closest('.reaction-btn');

    if (!isMemberMenu && !isServerHeader && !isServerMenu && !isRolePicker && !isEmojiPop && !isAddRoleBtn && !isReactionBtn) {
        closeAllMenus();
    }
});

function showMemberContextMenu(e, userId, displayName, serverId, isOwner) {
    e.preventDefault();
    closeAllMenus(); // Close others first
    currentMemberId = userId;
    currentMemberName = displayName;
    currentServerIdForMember = serverId;

    const menu = document.getElementById('member-context-menu');
    if (!menu) return;

    menu.style.display = 'block';
    
    // Use clientX/Y for fixed position
    let x = e.clientX;
    let y = e.clientY;
    
    const menuWidth = 188; 
    const menuHeight = menu.offsetHeight || 200;

    // Boundary checks: keep menu in viewport
    if (x + menuWidth > window.innerWidth) x = window.innerWidth - menuWidth - 5;
    if (y + menuHeight > window.innerHeight) y = window.innerHeight - menuHeight - 5;
    if (x < 5) x = 5;
    if (y < 5) y = 5;

    menu.style.left = x + 'px';
    menu.style.top = y + 'px';

    const kickName = document.getElementById('mcm-kick-name');
    if (kickName) kickName.innerText = displayName;
    const banName = document.getElementById('mcm-ban-name');
    if (banName) banName.innerText = displayName;

    // Owner protection
    const kickItem = document.getElementById('mcm-kick-item');
    const banItem = document.getElementById('mcm-ban-item');
    if (isOwner) {
        if (kickItem) kickItem.style.display = 'none';
        if (banItem) banItem.style.display = 'none';
    } else {
        if (kickItem) kickItem.style.display = 'flex';
        if (banItem) banItem.style.display = 'flex';
    }

    // Populate roles submenu
    populateRolesSubmenu(serverId, userId);
}

    function populateRolesSubmenu(serverId, userId) {
    const submenu = document.getElementById('mcm-roles-submenu');
    if (!submenu) return;
    submenu.innerHTML = '<div class="mcm-item">Loading...</div>';

    fetch('/Server/GetRoles?serverId=' + serverId)
        .then(r => r.json())
        .then(roles => {
            fetch('/Server/GetMembers?serverId=' + serverId)
                .then(r => r.json())
                .then(members => {
                    const member = members.find(m => m.userId === userId);
                    const memberRoleIds = member ? member.roles.map(r => r.id) : [];

                    submenu.innerHTML = '';
                    roles.forEach(role => {
                        const hasRole = memberRoleIds.includes(role.id);
                        const item = document.createElement('div');
                        item.className = 'mcm-item';
                        item.innerHTML = `
                            <div style="display:flex;align-items:center;pointer-events:none;">
                                <div class="member-role-dot" style="background-color:${role.color || '#99aab5'}"></div>
                                ${role.name}
                            </div>
                            <input type="checkbox" ${hasRole ? 'checked' : ''} style="pointer-events:none;" />
                        `;
                        item.onclick = (e) => {
                            e.stopPropagation();
                            toggleMemberRole(serverId, userId, role.id, hasRole);
                        };
                        submenu.appendChild(item);
                    });
                });
        });
    }

    function toggleMemberRole(serverId, userId, roleId, currentHas) {
    fetch('/Server/GetMembers?serverId=' + serverId)
        .then(r => r.json())
        .then(members => {
            const member = members.find(m => m.userId === userId);
            if (!member) return;

            let roleIds = member.roles.map(r => r.id);
            if (currentHas) roleIds = roleIds.filter(id => id !== roleId);
            else roleIds.push(roleId);

            const fd = new FormData();
            fd.append('serverId', serverId);
            fd.append('userId', userId);
            fd.append('roleIds', roleIds.join(','));

            fetch('/Server/UpdateMemberRoles', { method: 'POST', body: fd })
                .then(r => {
                    if (r.ok) {
                        location.reload(); 
                    }
                });
        });
    }

function changeNicknamePrompt() {
    const modal = document.getElementById('nickname-modal');
    const targetName = document.getElementById('nick-modal-target-name');
    const input = document.getElementById('nick-modal-input');
    if (modal && targetName && input) {
        targetName.innerText = currentMemberName;
        input.value = ''; // Reset
        modal.classList.add('open');
    }
}

function closeNicknameModal() { document.getElementById('nickname-modal')?.classList.remove('open'); }

function submitNicknameChange() {
    const input = document.getElementById('nick-modal-input');
    const newNick = input?.value;

    const fd = new FormData();
    fd.append('serverId', currentServerIdForMember);
    fd.append('userId', currentMemberId);
    fd.append('nickname', newNick);

    fetch('/Server/UpdateNickname', { method: 'POST', body: fd })
        .then(r => {
            if (r.ok) {
                closeNicknameModal();
                location.reload();
            } else r.text().then(alert);
        });
}

function kickMemberAction() {
    const modal = document.getElementById('kick-modal');
    const targetName = document.getElementById('kick-modal-target-name');
    if (modal && targetName) {
        targetName.innerText = currentMemberName;
        modal.classList.add('open');
    }
}

function closeKickModal() { document.getElementById('kick-modal')?.classList.remove('open'); }

function submitKickAction() {
    const fd = new FormData();
    fd.append('serverId', currentServerIdForMember);
    fd.append('userId', currentMemberId);

    fetch('/Server/KickMember', { method: 'POST', body: fd })
        .then(r => {
            if (r.ok) {
                closeKickModal();
                location.reload();
            } else r.text().then(alert);
        });
}

function banMemberAction() {
    const modal = document.getElementById('ban-modal');
    const targetName = document.getElementById('ban-modal-target-name');
    if (modal && targetName) {
        targetName.innerText = currentMemberName;
        document.getElementById('ban-modal-reason').value = '';
        modal.classList.add('open');
    }
}

function closeBanModal() { document.getElementById('ban-modal')?.classList.remove('open'); }

function submitBanAction() {
    const reason = document.getElementById('ban-modal-reason').value;
    const fd = new FormData();
    fd.append('serverId', currentServerIdForMember);
    fd.append('userId', currentMemberId);
    fd.append('reason', reason);

    fetch('/Server/BanMember', { method: 'POST', body: fd })
        .then(r => {
            if (r.ok) {
                closeBanModal();
                location.reload();
            } else r.text().then(alert);
        });
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
    closeAllMenus();
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
    
    let x = r.left - 40;
    let y = r.top - 46;
    
    if (x + 200 > window.innerWidth) x = window.innerWidth - 210;
    if (x < 0) x = 10;
    if (y < 0) y = r.bottom + 10;
    
    pop.style.left = x + 'px';
    pop.style.top = y + 'px';
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

connection.on("UserStatusChanged", (userId, isOnline) => {
    refreshMembersSidebar();
});

function refreshMembersSidebar() {
    const sidebar = document.querySelector('.members-sidebar');
    if (!sidebar) return;
    
    // Get server ID from ss-server-id or URL
    let serverId = document.getElementById('ss-server-id')?.value;
    if (!serverId || serverId === "0") {
        const parts = window.location.pathname.split('/');
        serverId = parts[parts.length - 1];
    }
    
    if (serverId && !isNaN(serverId)) {
        fetch('/Server/GetMembersSidebar?serverId=' + serverId)
            .then(r => r.text())
            .then(html => {
                const sidebar = document.querySelector('.members-sidebar');
                if (sidebar) {
                    const parent = sidebar.parentElement;
                    const temp = document.createElement('div');
                    temp.innerHTML = html;
                    const newSidebar = temp.querySelector('.members-sidebar');
                    if (newSidebar) {
                        parent.replaceChild(newSidebar, sidebar);
                    }
                }
            });
    }
}

//Server Creation
function openCreateServerModal(){
    document.getElementById('create-server-modal').classList.add('open');
}
function closeCreateServerModal() {
    const modal = document.getElementById('create-server-modal');
    modal.classList.remove('open');

    goToStep('cs-step-type');
}
function goToStep(stepId){
    document.querySelectorAll('.cs-step ').forEach(s => s.classList.remove('cs-step--active'));
    document.getElementById(stepId).classList.add('cs-step--active');
}
document.addEventListener('DOMContentLoaded', () => {
    document.getElementById('cs-close-btn').addEventListener('click', closeCreateServerModal)
    document.getElementById('create-server-modal').addEventListener('click', (e) => {
        if (e.target === e.currentTarget) closeCreateServerModal();
    });
    document.addEventListener('keydown', (e) =>{
        if(e.key === 'Escape'){
            closeCreateServerModal()
        }
    })
    document.querySelectorAll('.cs-type-option').forEach(btn => {
        btn.addEventListener('click', () => {
            const serverTypeInput = document.getElementById('cs-server-type');
            if (serverTypeInput) {
                serverTypeInput.value = btn.dataset.type || '';
            }
            goToStep('cs-step-customize');
        })
    })
    document.getElementById('cs-back-btn').addEventListener('click', () => goToStep('cs-step-type'));

    document.getElementById('cs-cancel-btn').addEventListener('click', () => goToStep('cs-step-type'));

    document.getElementById('cs-icon-input').addEventListener('change', (e) => {                                                                                                         
        const file = e.target.files[0];                                                                                                                                                  
        if (!file){
            return;     
        }                                                                                                                                                             
        const preview = document.getElementById('cs-icon-preview');                                                                                                                      
        const placeholder = document.getElementById('cs-icon-placeholder');                                                                                                              
        preview.src = URL.createObjectURL(file);                                                                                                                                         
        preview.hidden = false;                                                                                                                                                          
        placeholder.style.display = 'none';                                                                                                                                              
    }); 

    // Handle AJAX form submission for Server Creation
    const serverForm = document.getElementById('create-server-form');
    if (serverForm) {
        serverForm.addEventListener('submit', function (e) {
            e.preventDefault();
            const nameInput = document.getElementById('cs-server-name');
            if (!nameInput || !nameInput.value.trim()) {
                alert('Please enter a server name.');
                return;
            }

            const formData = new FormData(this);

            fetch('/Server/CreateServer', {
                method: 'POST',
                body: formData
            })
            .then(res => {
                if (!res.ok) throw new Error('Failed to create server.');
                return res.json();
            })
            .then(data => {
                closeCreateServerModal();
                // Redirect to the newly created server detail page
                window.location.href = '/Server/Details/' + data.serverId;
            })
            .catch(err => {
                console.error('Error creating server:', err);
                alert('Failed to create server. Please check your inputs.');
            });
        });
    }
});
// Server Settings & Dropdown
function toggleServerDropdown() {
    const menu = document.getElementById('server-dropdown-menu');
    const chevron = document.getElementById('header-chevron');
    if (menu) {
        const wasOpen = menu.classList.contains('open');
        closeAllMenus(); // Close all including self
        
        if (!wasOpen) {
            menu.classList.add('open');
            if (chevron) chevron.style.transform = 'rotate(180deg)';
        } else {
            // Already closed by closeAllMenus
        }
    }
}

function openServerSettingsModal() {
    const modal = document.getElementById('server-settings-modal');
    if (modal) {
        const serverName = document.getElementById('current-server-name-val')?.value || document.querySelector('.sidebar-header-name')?.innerText;
        const serverId = document.getElementById('current-server-id-val')?.value;
        
        if (serverId) {
            document.getElementById('ss-server-id').value = serverId;
            document.getElementById('ss-server-name').value = serverName || '';
            modal.classList.add('open');
            switchSSTab('overview', document.querySelector('.ss-tab'));
        }
    }
    const menu = document.getElementById('server-dropdown-menu');
    if (menu) menu.classList.remove('open');
}

function closeServerSettingsModal() {
    document.getElementById('server-settings-modal')?.classList.remove('open');
}

function switchSSTab(tabId, el) {
    document.querySelectorAll('.ss-tab').forEach(t => t.classList.remove('active'));
    if (el) el.classList.add('active');
    
    document.querySelectorAll('.ss-tab-pane').forEach(p => p.classList.remove('active'));
    document.getElementById('ss-tab-' + tabId).classList.add('active');
    
    if (tabId === 'roles') loadRoles();
    if (tabId === 'members') loadMembers();
}

const PERMISSIONS = [
    { name: 'Administrator', flag: 1, desc: 'Gives all permissions and bypasses channel restrictions. Dangerous.' },
    { name: 'Manage Server', flag: 2, desc: 'Allows changing server name and icon.' },
    { name: 'Manage Roles', flag: 4, desc: 'Allows creating and editing roles.' },
    { name: 'Manage Channels', flag: 8, desc: 'Allows creating and deleting channels and categories.' },
    { name: 'Kick Members', flag: 16, desc: 'Allows kicking members from the server.' },
    { name: 'Ban Members', flag: 32, desc: 'Allows banning members from the server.' },
    { name: 'Create Invite', flag: 64, desc: 'Allows creating invite links.' },
    { name: 'Change Nickname', flag: 128, desc: 'Allows changing own nickname.' },
    { name: 'Manage Nicknames', flag: 256, desc: 'Allows changing other members\' nicknames.' },
    { name: 'Send Messages', flag: 512, desc: 'Allows sending messages in text channels.' },
    { name: 'Embed Links', flag: 1024, desc: 'Allows messages to have rich content.' },
    { name: 'Attach Files', flag: 2048, desc: 'Allows uploading files and images.' },
    { name: 'Add Reactions', flag: 4096, desc: 'Allows adding new reactions to messages.' },
    { name: 'Mention Everyone', flag: 8192, desc: 'Allows using @everyone and @here.' },
    { name: 'Manage Messages', flag: 16384, desc: 'Allows deleting and pinning messages.' },
    { name: 'Read Message History', flag: 32768, desc: 'Allows reading past messages.' }
];

let serverRoles = [];

function loadRoles() {
    const serverId = document.getElementById('ss-server-id').value;
    if (!serverId || serverId === "0") return Promise.resolve();
    
    return fetch('/Server/GetRoles?serverId=' + serverId)
        .then(async r => {
            const contentType = r.headers.get("content-type");
            if (!r.ok) {
                const text = await r.text().catch(() => "");
                throw new Error(`Failed to load roles: ${r.status} ${text}`);
            }
            if (!contentType || !contentType.includes("application/json")) {
                throw new Error("Expected JSON response from server but got " + contentType);
            }
            return r.json();
        })
        .then(roles => {
            serverRoles = roles;
            const container = document.getElementById('roles-list-container');
            if (!container) return;
            container.innerHTML = '';
            roles.forEach(role => {
                const div = document.createElement('div');
                div.className = 'role-item';
                div.innerHTML = `<div class="role-color-dot" style="background-color: ${role.color || '#99aab5'}"></div><span>${escapeHtml(role.name)}</span>`;
                div.onclick = () => editRole(role.id);
                container.appendChild(div);
            });
            document.getElementById('role-edit-container').style.display = 'none';
        })
        .catch(err => {
            console.error("Error loading roles:", err);
            // Don't alert here to avoid spamming the user if something minor goes wrong, but log it.
        });
}

function editRole(roleId) {
    const role = serverRoles.find(r => r.id == roleId); // Use == for loose comparison in case of string/int mismatch
    if (!role) {
        console.warn("Role not found for editing:", roleId);
        return;
    }
    document.querySelectorAll('.role-item').forEach(item => {
        item.classList.toggle('active', item.querySelector('span').innerText === role.name);
    });
    document.getElementById('role-edit-container').style.display = 'block';
    document.getElementById('edit-role-id').value = role.id;
    document.getElementById('edit-role-name').value = role.name;
    document.getElementById('edit-role-color').value = role.color || '#99aab5';
    const pList = document.getElementById('permissions-list');
    pList.innerHTML = '';
    PERMISSIONS.forEach(p => {
        const has = (BigInt(role.permissions) & BigInt(p.flag)) !== 0n;
        pList.insertAdjacentHTML('beforeend', `<div class="permission-item"><div class="permission-info"><span class="permission-name">${p.name}</span><span class="permission-desc">${p.desc}</span></div><input type="checkbox" class="p-checkbox" data-flag="${p.flag}" ${has ? 'checked' : ''} /></div>`);
    });
}

function createNewRole() {
    const serverId = document.getElementById('ss-server-id').value;
    if (!serverId || serverId === "0") {
        console.error("No valid server ID found for role creation. ID:", serverId);
        alert("Error: No server selected.");
        return;
    }
    const fd = new FormData();
    fd.append('serverId', serverId);
    fd.append('name', "new role");

    fetch('/Server/CreateRole', { method: 'POST', body: fd })
        .then(async r => {
            const contentType = r.headers.get("content-type");
            let data = {};
            if (contentType && contentType.includes("application/json")) {
                data = await r.json().catch(() => ({}));
            }
            if (!r.ok) {
                throw new Error(data.message || "Failed to create role: " + r.status);
            }
            return data;
        })
        .then(async role => {
            await loadRoles();
            setTimeout(() => {
                if (role && role.id) {
                    editRole(role.id);
                }
            }, 300);
        })
        .catch(err => {
            console.error("Error creating role:", err);
            alert("Error: " + err.message);
        });
}

function deleteCurrentRole() {
    const roleId = document.getElementById('edit-role-id').value;
    const serverId = document.getElementById('ss-server-id').value;
    if (confirm("Delete this role?")) fetch(`/Server/DeleteRole?serverId=${serverId}&roleId=${roleId}`, { method: 'POST' }).then(r => { if (r.ok) loadRoles(); });
}

function loadMembers() {
    const serverId = document.getElementById('ss-server-id').value;
    if (!serverId) return;
    const ownerId = document.getElementById('current-server-owner-id-val')?.value;

    fetch('/Server/GetMembers?serverId=' + serverId)
        .then(r => {
            if (!r.ok) throw new Error("Failed to load members");
            return r.json();
        })
        .then(members => {
            const container = document.getElementById('members-list-container');
            if (!container) return;
            container.innerHTML = '';
            members.forEach(m => {
                const isOwner = m.userId === ownerId;
                const div = document.createElement('div');
                div.className = 'ss-member-item';
                div.oncontextmenu = (e) => showMemberContextMenu(e, m.userId, m.displayName, serverId, isOwner);
                div.innerHTML = `
                    <div class="ss-member-info">
                        <div class="ss-member-avatar" style="background-color: ${getUserColor(m.displayName)}">
                            ${m.hasPfp ? `<img src="/Server/GetProfilePicture?userId=${m.userId}" />` : m.displayName[0]}
                        </div>
                        <div class="ss-member-names">
                            <div style="display:flex;align-items:center;gap:4px;">
                                <span class="ss-member-display">${escapeHtml(m.displayName)}</span>
                                ${isOwner ? `
                                    <svg title="Server Owner" width="14" height="14" viewBox="0 0 24 24" fill="#f1c40f">
                                        <path d="M12 2L15.09 8.26L22 9.27L17 14.14L18.18 21.02L12 17.77L5.82 21.02L7 14.14L2 9.27L8.91 8.26L12 2Z"/>
                                    </svg>` : ''}
                            </div>
                            <span class="ss-member-user">${escapeHtml(m.userName)}</span>
                        </div>
                    </div>
                    <div class="ss-member-roles">
                        ${m.roles.map(r => `<span class="ss-role-tag" style="border-color: ${r.color}">${escapeHtml(r.name)}</span>`).join('')}
                        <button class="ss-add-role-btn" onclick="openMemberRolePicker('${m.userId}', this)">+</button>
                    </div>
                `;
                container.appendChild(div);
            });
        })
        .catch(err => console.error("Error loading members:", err));
}

function openMemberRolePicker(userId, btn) {
    closeAllMenus();
    let picker = document.getElementById('member-role-picker');
    if (picker) picker.remove();
    picker = document.createElement('div');
    picker.id = 'member-role-picker';
    picker.className = 'role-picker-popover';
    
    serverRoles.forEach(role => {
        const item = document.createElement('div');
        item.className = 'role-picker-item';
        item.innerHTML = `<div class="role-color-dot" style="background-color: ${role.color}"></div><span>${escapeHtml(role.name)}</span>`;
        item.onclick = () => addRoleToMember(userId, role.id);
        picker.appendChild(item);
    });
    document.body.appendChild(picker);
    const r = btn.getBoundingClientRect();
    
    let x = r.left - 150;
    let y = r.top;
    
    if (x < 0) x = r.right + 10;
    const pickerHeight = picker.offsetHeight || 200;
    if (y + pickerHeight > window.innerHeight) {
        y = window.innerHeight - pickerHeight - 10;
    }
    
    picker.style.left = x + 'px';
    picker.style.top = y + 'px';
}

function addRoleToMember(userId, roleId) {
    const serverId = document.getElementById('ss-server-id').value;
    // For simplicity, we just toggle or add. Real implementation would be more complex.
    // Fetch current roles first would be better, but let's just use a comma-separated list.
    fetch('/Server/GetMembers?serverId=' + serverId).then(r => r.json()).then(members => {
        const member = members.find(m => m.userId === userId);
        let roleIds = member.roles.map(r => r.id);
        if (roleIds.includes(roleId)) roleIds = roleIds.filter(id => id !== roleId);
        else roleIds.push(roleId);
        
        const fd = new FormData();
        fd.append('serverId', serverId);
        fd.append('userId', userId);
        fd.append('roleIds', roleIds.join(','));
        fetch('/Server/UpdateMemberRoles', { method: 'POST', body: fd }).then(r => {
            if (r.ok) { loadMembers(); document.getElementById('member-role-picker')?.remove(); }
        });
    });
}

function deleteServer() {
    const serverId = document.getElementById('ss-server-id').value;
    if (confirm("DANGER: Delete this server?")) {
        fetch('/Server/DeleteServer?serverId=' + serverId, { method: 'POST' }).then(r => {
            if (r.ok) window.location.href = '/';
        });
    }
}

function leaveServer() {
    const serverId = document.getElementById('current-server-id-val')?.value;
    if (!serverId) return;
    
    const ownerId = document.getElementById('current-server-owner-id-val')?.value;
    const currentUserId = document.getElementById('current-user-id')?.value;

    if (ownerId === currentUserId) {
        alert("Notice: As the owner, leaving this server will transfer ownership to the next most senior member.");
    }
    
    if (confirm("Are you sure you want to leave this server?")) {
        fetch('/Server/LeaveServer?serverId=' + serverId, { method: 'POST' })
            .then(r => {
                if (r.ok) window.location.href = '/';
                else r.text().then(alert);
            });
    }
}

function openInviteModal() {
    const modal = document.getElementById('invite-modal');
    const serverId = document.getElementById('current-server-id-val')?.value;
    if (modal && serverId) {
        generateNewInvite();
        modal.classList.add('open');
    } else {
        alert('Please select a server first.');
    }
    document.getElementById('server-dropdown-menu')?.classList.remove('open');
}

function generateNewInvite() {
    const serverId = document.getElementById('current-server-id-val')?.value;
    if (!serverId) return;

    const expiration = document.getElementById('invite-expiration').value;
    const maxUses = document.getElementById('invite-max-uses').value;

    const fd = new FormData();
    fd.append('serverId', serverId);
    if (expiration) fd.append('expirationDays', expiration);
    if (maxUses) fd.append('maxUses', maxUses);

    fetch('/Server/CreateInvite', { method: 'POST', body: fd })
        .then(r => r.json())
        .then(data => {
            document.getElementById('invite-link-input').value = window.location.origin + '/Server/Join/' + data.token;
        });
}

function closeInviteModal() { document.getElementById('invite-modal')?.classList.remove('open'); }
function copyInviteLink() { const input = document.getElementById('invite-link-input'); input.select(); document.execCommand('copy'); alert('Copied!'); }

document.addEventListener('DOMContentLoaded', () => {
    document.getElementById('edit-role-form')?.addEventListener('submit', function(e) {
        e.preventDefault();
        const fd = new FormData(this);
        fd.append('serverId', document.getElementById('ss-server-id').value);
        let permissions = 0n;
        document.querySelectorAll('.p-checkbox:checked').forEach(cb => permissions |= BigInt(cb.dataset.flag));
        fd.set('permissions', permissions.toString());
        fetch('/Server/UpdateRole', { method: 'POST', body: fd }).then(r => { if (r.ok) { loadRoles(); alert('Updated!'); } });
    });

    document.getElementById('update-server-form')?.addEventListener('submit', function(e) {
        e.preventDefault();
        fetch('/Server/UpdateServer', { method: 'POST', body: new FormData(this) }).then(r => { if (r.ok) location.reload(); });
    });

    document.getElementById('ss-icon-input')?.addEventListener('change', (e) => {
        const file = e.target.files[0]; if (!file) return;
        const preview = document.getElementById('ss-icon-preview');
        preview.src = URL.createObjectURL(file); preview.hidden = false;
        document.getElementById('ss-icon-placeholder').style.display = 'none';
    });
});

