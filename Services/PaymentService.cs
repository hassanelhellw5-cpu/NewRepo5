using System;
using System.Threading.Tasks;
using QemmaProject.Data;
using QemmaProject.Models.Payment;
using Microsoft.EntityFrameworkCore;

namespace QemmaProject.Services
{
    public class PaymentService
    {
        private readonly AppDbContext _context;

        public PaymentService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> ApprovePaymentRequestAsync(int requestId, string adminNotes = "")
        {
            var request = await _context.ManualPaymentRequests
                                        .Include(r => r.User)
                                        .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null || request.Status != PaymentStatus.Pending)
                return false;

            // فتح Transaction لضمان تنفيذ العمليتين معاً أو التراجع عنهما
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. تحديث حالة الطلب
                request.Status = PaymentStatus.Approved;
                request.ProcessedAt = DateTime.UtcNow;
                request.AdminNotes = adminNotes;

                // 2. إضافة العملات لمحفظة المستخدم
                request.User.QemmaCoinsBalance += request.RequestedCoins;

                // 3. تسجيل الحركة المالية في السجل
                var newTransaction = new Transaction
                {
                    UserId = request.UserId,
                    Amount = request.RequestedCoins,
                    TransactionType = "Deposit - Manual Verification",
                    CreatedAt = DateTime.UtcNow,
                    Notes = $"Approved request #{request.Id} via {request.PaymentMethod}"
                };

                _context.Transactions.Add(newTransaction);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return false;
            }
        }
    }
}