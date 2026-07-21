using Microsoft.AspNetCore.Identity;
using QemmaProject.Models.Sports;

namespace QemmaProject.Models.Payment
{
    public class ApplicationUser : IdentityUser
    {
        // رصيد عملات القمة الخاص بالمستخدم
        public decimal QemmaCoinsBalance { get; set; } = 0m;

        // اشتراك دوري النخبة
        public bool IsEliteSubscriber { get; set; } = false;
        public DateTime? EliteSubscriptionEndDate { get; set; }

        // التخصيص التجميلي للمشجع
        public string CustomThemePalette { get; set; } = "Default";
        public string ActiveBadgeUrl { get; set; } = string.Empty;

        // العلاقات (Navigation Properties)
        public virtual ICollection<Transaction> Transactions { get; set; }
        public virtual ICollection<Prediction> Predictions { get; set; }
    }

    // جدول لتسجيل أي حركة مالية (شحن عملات، شراء تأثيرات، دعم طوعي)
    public class Transaction
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public virtual ApplicationUser User { get; set; }

        public decimal Amount { get; set; }
        public string TransactionType { get; set; } // e.g., "Deposit", "BuyFrame", "SuperChat", "Donation"
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string Notes { get; set; }
    }
}