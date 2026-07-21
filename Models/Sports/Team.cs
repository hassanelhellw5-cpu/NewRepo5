using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace QemmaProject.Models.Sports
{
    public class Team
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }
        public string? LogoUrl { get; set; }
        // العلاقات: الفريق ممكن يكون صاحب الأرض أو الضيف في ماتشات كتير
        public virtual ICollection<Match> HomeMatches { get; set; }
        public virtual ICollection<Match> AwayMatches { get; set; }
        public int? ApiTeamId { get; set; } // الـ ID الرسمي من API-Football
    }
}
