using System.ComponentModel.DataAnnotations;

namespace QemmaProject.Models.Payment
{
    public class RewardedVideoView
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser User { get; set; }

        [Required]
        public string Provider { get; set; } = string.Empty;
        public string PlacementId { get; set; } = string.Empty;

        [Required]
        public string ExternalRewardId { get; set; } = string.Empty;
        public decimal CoinsAwarded { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
