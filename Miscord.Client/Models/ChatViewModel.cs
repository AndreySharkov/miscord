using System;
using System.Collections.Generic;

namespace Miscord.Client.Models
{
    /// <summary>
    /// Lightweight projection used by GetChat – never loads AttachmentData bytes.
    /// </summary>
    public class ChatMessageViewModel
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string AuthorId { get; set; } = string.Empty;
        public string? AuthorDisplayName { get; set; }
        public bool AuthorHasProfilePicture { get; set; }

        // Attachment metadata only – no binary data
        public bool HasAttachment { get; set; }
        public string? AttachmentFileName { get; set; }
        public string? AttachmentContentType { get; set; }

        // Reply / parent
        public int? ReplyToMessageId { get; set; }
        public string? ParentContent { get; set; }
        public string? ParentAuthorId { get; set; }
        public string? ParentAuthorDisplayName { get; set; }
        public bool ParentAuthorHasProfilePicture { get; set; }
    }

    public class ChatChannelViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<ChatMessageViewModel> Messages { get; set; } = new();
        public Miscord.Data.Models.Server Server { get; set; } = null!;
    }
}
