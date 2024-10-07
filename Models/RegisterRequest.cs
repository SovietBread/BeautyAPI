using System.Text.Json.Serialization;

namespace Models
{
    public class RegisterRequest
    {
        public int Id { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }

        [JsonPropertyName("user_type")]
        public string UserType { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
