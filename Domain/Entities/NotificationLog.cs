using System;
namespace Domain.Entities
{
    public class NotificationLog
    {
        public Guid LogId { get; set; }
        public required string LicensePlate { get; set; }
        public required string Message { get; set; }
        public DateTime SentTime { get; set; }
        public bool IsSuccess { get; set; }
    }
}