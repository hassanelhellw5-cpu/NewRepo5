using System.ComponentModel.DataAnnotations;

namespace QemmaProject.Models.Sports
{
    public class ArchiveVideo
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } // مثلاً: ملخص مباريات ريال مدريد وبرشلونة

        [Required]
        public string VideoUrl { get; set; } // رابط الفيديو على السيرفر أو التخزين السحابي

        public string Quality { get; set; } // مثلاً: 1080p, 4K

        // ربط الفيديو بمباراة معينة (اختياري، لأن ممكن يكون فيديو مجمع أو وثائقي)
        public int? MatchId { get; set; }
        public virtual Match Match { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public int DownloadsCount { get; set; } = 0; // عشان نعرف أكتر فيديوهات مطلوبة
    }
}
