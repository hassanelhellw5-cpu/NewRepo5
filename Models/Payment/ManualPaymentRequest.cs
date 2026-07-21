using System;
using System.ComponentModel.DataAnnotations;

namespace QemmaProject.Models.Payment
{
    public enum PaymentStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }

    public class ManualPaymentRequest
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }
        public virtual ApplicationUser User { get; set; }

        public decimal AmountPaid { get; set; } // المبلغ الفعلي المدفوع (مثلاً بالجنيه)
        public decimal RequestedCoins { get; set; } // عدد عملات القمة المقابلة للمبلغ

        public string PaymentMethod { get; set; } // مثلاً: فودافون كاش، إنستا باي
        public string PhoneNumberUsed { get; set; } // رقم الموبايل المحول منه
        public string ReceiptImageUrl { get; set; } // مسار صورة إثبات التحويل (Screenshot)

        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; } // وقت الاعتماد أو الرفض من الأدمن
        public string AdminNotes { get; set; } // ملاحظات الإدارة في حالة الرفض
    }
}