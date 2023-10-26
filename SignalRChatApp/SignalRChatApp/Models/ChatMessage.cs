namespace SignalRChatApp
{
    public class ChatMessage
    {
        public string Username { get; set; }
        public string Message { get; set; }
        public string Room { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
