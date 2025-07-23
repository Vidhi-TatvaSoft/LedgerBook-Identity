using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net.Mail;

namespace DataAccessLayer.Models;

public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [StringLength(100)]
    public string? FirstName { get; set; }

    [StringLength(100)]
    public string? LastName { get; set; }

    [Required]
    [StringLength(400)]
    public string Email { get; set; }

    [Required]
    public string Password { get; set; }

    public long? MobileNumber { get; set; }

    public int? ProfileAttachmentId { get; set; }

    public bool IsUserRegistered { get; set; }

    public string? VerificationToken { get; set; }

    public bool IsEmailVerified { get; set; } = false;

    public DateTime CreatedAt { get; set; }
    public int? UpdatedById { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? DeletedById { get; set; }
    public DateTime? DeletedAt { get; set; }

    public int? ApplicationUserId { get; set; } = null;

    [ForeignKey("ProfileAttachmentId")]
    public virtual Attachment Attachments { get; set; } = null!;
     [ForeignKey("ApplicationUserId")]
    public virtual ApplicationUser ApplicationUser { get; set; } = null!;
}
