using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Models;

public class UnlockCode
{
    [Key]
    public int Id { get; set; }

    [ForeignKey("User")]
    public int UserId { get; set; }
    public int? SalonId { get; set; }
    public User User { get; set; }

    public string Code { get; set; }
    public string BlockType { get; set; }

    public byte[] QrCode { get; set; }
}
