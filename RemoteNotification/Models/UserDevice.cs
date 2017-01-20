namespace RemoteNotification.Models
{
    public class UserDevice
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int RoleId { get; set; }
        public int TenantId { get; set; }
        public string Name { get; set; }
        public string DevicePlatform { get; set; }
        public string DeviceToken { get; set; }
    }
}
