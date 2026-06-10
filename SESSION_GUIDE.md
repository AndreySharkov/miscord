# Miscord Development Guide: Session Summary & Implementation Steps

This document provides a comprehensive overview of the architectural and functional changes made during this development session, followed by a detailed technical guide on how to implement the newly requested features.

---

## Part 1: Session Change Log (What was changed)

During this session, we transformed the server management and security architecture. Below is a detailed breakdown of every file modified and the logic implemented.

### 1. Database & Models (`Miscord.Data`)
*   **`Models/Invite.cs`**: Created a new entity to handle secure server invitations. This moved away from simple ID-based joins to a tokenized system.
    *   **Fields**: `Token` (Random 10-char string), `ExpiresAt` (Optional expiry), `MaxUses` (Optional usage cap), `Uses` (Counter).
*   **`AppDbContext.cs`**: 
    *   Registered `DbSet<Invite>`.
    *   **Cascade Delete Fixes**: Configured `NoAction` for several relationships (`ServerMember -> User`, `ServerMemberRole -> ServerRole`, `Invite -> Creator`) to resolve SQL Server circular dependency errors.
*   **Migrations**: Generated and applied the `AddRolesAndMembers` and `AddInvites` migrations to synchronize the SQL schema.
*   **`Seed.cs`**: Updated the initial seeder to ensure the Admin user is explicitly added to the `ServerMembers` table of the default server, ensuring they can see it in the new restricted sidebar.

### 2. Backend Logic (`Miscord.Client/Controllers`)
*   **`ServerController.cs`**:
    *   **Secure Invites**: Implemented `CreateInvite` (generates random tokens) and updated `Join` to validate tokens, check for expiration, and enforce usage limits.
    *   **Member Management**: Added `GetMembers` and `UpdateMemberRoles` to support the new administrative UI.
    *   **Leave Server**: Implemented `LeaveServer` endpoint with a safety check preventing the owner from leaving (they must delete or transfer).
    *   **Permission Enforcement**: Integrated `PermissionHelper` across all POST actions (`CreateChannel`, `CreateCategory`, `PostMessage`, etc.) to ensure users have the required bitwise permissions.
    *   **Default Roles**: Updated `CreateServer` to automatically generate "Admin" (Full access) and "Member" (Standard access) roles upon server creation.

### 3. Frontend Architecture (`wwwroot/js/site.js`)
*   **`loadServer` Enhancement**: Rewrote the server loading logic to dynamically update the server header name, toggle the visibility of the "Server Settings" button based on permissions, and show/hide the "Leave Server" button.
*   **Role Management**: Implemented `createNewRole`, `editRole`, and `loadRoles` to allow real-time bitwise permission editing in the modal.
*   **Invite Logic**: Added `generateNewInvite` to interface with the new tokenized backend.
*   **Member Management**: Added `loadMembers` and `addRoleToMember` for the new "Members" tab.

### 4. UI Components (`Views`)
*   **`_Layout.cshtml`**: Added the "Leave Server" button to the sidebar dropdown and ensured correct SignalR/JS initialization.
*   **`_ServerSettingsModal.cshtml`**: 
    *   Added a **Members** tab for role management.
    *   Enhanced the **Invite** modal with dropdowns for Expiration and Max Uses.
    *   Integrated the **Roles** editor with permission checkboxes.
*   **`ServerSidebarViewComponent.cs`**: Updated to filter servers so users only see servers they have actually joined.

---

## Part 2: Implementation Guide (Next Steps)

### Task 1: Redirect to first channel on Server Click
Currently, clicking a server icon loads the "Welcome" screen. To make it feel faster, we can automatically "click" the first channel.

**Step 1: Modify `site.js`**
Locate the `loadServer` function. In the `.then(html => { ... })` block where the channel list is rendered, add logic to find the first channel link and trigger its click event.

```javascript
// Inside site.js -> loadServer success callback
fetch('/Server/GetChannels/' + serverId)
    .then(r => r.text())
    .then(html => {
        const list = document.querySelector('.channel-list');
        if (list) {
            list.innerHTML = html;
            
            // --- ADD THIS LOGIC ---
            // Find the first channel item in the newly loaded list
            const firstChannel = list.querySelector('.channel-item');
            if (firstChannel) {
                // Simulate a click or call the function directly
                // This will automatically trigger loadChannel for the first item
                firstChannel.click(); 
            }
            // ----------------------
            
            // (rest of the existing header/settings update logic...)
        }
    });
```

**Why this way?** Doing it in the frontend ensures the UI stays responsive. The channel list is already being fetched, so we just look at the DOM it created.

---

### Task 2: Refine "Leave Server" for Admins
You mentioned that admins cannot leave. In the current logic, only the **Owner** is blocked. Admins (people with the "Admin" role) should be able to leave because they are still just "Members" in the database.

**Step 1: Verify the Frontend Check (`site.js`)**
In the `loadServer` function, we set the visibility of the leave button. Ensure it only checks for the `ownerId`.

```javascript
// Inside site.js -> loadServer
const ownerId = document.getElementById('current-server-owner-id-val')?.value;
const currentUserId = document.getElementById('current-user-id')?.value;

const leaveItem = document.getElementById('leave-server-dropdown-item');
if (leaveItem) {
    // Only the owner is hidden from the "Leave" option.
    // Admins ARE members, so they will pass this check.
    leaveItem.style.display = (ownerId !== currentUserId) ? 'flex' : 'none';
}
```

**Step 2: Verify the Backend Safety (`ServerController.cs`)**
The backend `LeaveServer` method should only throw an error if the user is the `OwnerId`.

```csharp
[HttpPost]
public async Task<IActionResult> LeaveServer(int serverId)
{
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    var server = await _context.Servers.FindAsync(serverId);
    if (server == null) return NotFound();

    // Safety check: Owners are essential to the server existence.
    if (server.OwnerId == userId)
        return BadRequest("Owners cannot leave. You must delete the server or transfer ownership.");

    var member = await _context.ServerMembers.FirstOrDefaultAsync(sm => sm.ServerId == serverId && sm.UserId == userId);
    if (member != null)
    {
        _context.ServerMembers.Remove(member);
        await _context.SaveChangesAsync();
    }
    return Ok();
}
```

**Troubleshooting Admin Leave:**
If an admin still can't leave, check if they are being incorrectly identified as the owner. 
1. Open `_ChannelList.cshtml`.
2. Ensure `current-server-owner-id-val` is correctly populated from `@Model.OwnerId`.
3. If an admin is also a "Server Admin" (role), they still have a `UserId` distinct from the `OwnerId`.

---

### Summary of Bitwise Permissions Used
In this session, we utilized the following `ServerPermissions` enum flags. When adding roles, these are combined using the `|` operator:
*   `Administrator` (1): Bypasses all checks.
*   `ManageServer` (2): Allows changing name/icon.
*   `ManageRoles` (4): Access to the Roles tab.
*   `ManageChannels` (8): Access to create categories/channels.
*   `CreateInvite` (64): Permission to generate tokens.
*   `SendMessages` (512): Basic chat access.

---
**End of Guide**
