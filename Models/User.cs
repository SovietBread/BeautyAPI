namespace Models
{
    public class User
    {
        public int Id { get; set; }
        public string Login { get; set; }
        public string PasswordHash { get; set; }
        public string UserType { get; set; }
        public DateTime CreatedAt { get; set; }
        public string RefreshToken { get; set; }
        public string AccessToken { get; set; }
    }
}
