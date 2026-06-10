namespace Miscord.Services
{
    public class ChatMessageDto
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string AuthorId { get; set; } = string.Empty;
        public string? AuthorNickname { get; set; }
        public string? AuthorUserName { get; set; }
        public bool AuthorHasProfilePicture { get; set; }

        public bool HasAttachment { get; set; }
        public string? AttachmentFileName { get; set; }
        public string? AttachmentContentType { get; set; }

        public int? ReplyToMessageId { get; set; }
        public string? ParentContent { get; set; }
        public string? ParentAuthorId { get; set; }
        public string? ParentAuthorNickname { get; set; }
        public string? ParentAuthorUserName { get; set; }
        public bool ParentAuthorHasProfilePicture { get; set; }
    }
}
