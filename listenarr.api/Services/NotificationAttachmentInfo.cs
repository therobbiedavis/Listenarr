namespace Listenarr.Api.Services
{
    /// <summary>
    /// Public DTO used by DI-friendly payload builders to describe an attachment prepared for notifications.
    /// Kept minimal and immutable for easy testing.
    /// </summary>
    public sealed class NotificationAttachmentInfo
    {
        public required byte[] ImageData { get; init; }
        public required string Filename { get; init; }
        public required string ContentType { get; init; }
    }
}
