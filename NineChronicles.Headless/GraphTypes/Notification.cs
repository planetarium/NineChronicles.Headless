namespace NineChronicles.Headless.GraphTypes
{
    public class Notification
    {
        public NotificationEnum Type { get; set; }
        public string Message { get; set; }

        public Notification(NotificationEnum type)
        {
            Type = type;
        }

        public Notification(NotificationEnum type, string msg) : this(type)
        {
            Message = msg;
        }

    }
}
