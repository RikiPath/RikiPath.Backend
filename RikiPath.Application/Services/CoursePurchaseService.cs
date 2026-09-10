using Domain.Enums;
using RikiPath.Application.IClients;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Payments;
using RikiPath.Application.Responses.CoursePurchases;
using RikiPath.Domain;
using RikiPath.Domain.Entities;

namespace RikiPath.Application.Services
{
    public class CoursePurchaseService(
        IUnitOfWork unitOfWork,
        IPaymentGatewayClient paymentGatewayClient,
        AppSettings appSettings) : ICoursePurchaseService
    {
        public async Task<CoursePurchaseCheckoutResponse> CreateCheckoutAsync(
            int userId, int courseId, CancellationToken cancellationToken)
        {
            var course = await unitOfWork.Courses.GetByIdAsync(courseId)
                ?? throw new KeyNotFoundException("Không tìm thấy khóa học.");

            if (course.Status != ContentStatus.Published)
                throw new InvalidOperationException("Khóa học chưa được phát hành nên không thể mua.");

            if (await unitOfWork.CoursePurchases.HasPurchasedCourseAsync(userId, courseId))
                throw new InvalidOperationException("Bạn đã mua khóa học này rồi.");

            var orderCode = GenerateOrderCode();

            var purchase = new CoursePurchase
            {
                UserId = userId,
                CourseId = courseId,
                AmountPaid = course.Price,
                PaymentStatus = PaymentStatus.Pending,
                OrderCode = orderCode,
                PurchasedAt = DateTime.UtcNow,
            };

            await unitOfWork.CoursePurchases.AddAsync(purchase);
            await unitOfWork.SaveChangesAsync();

            // PayOS giới hạn "description" tối đa 25 ký tự.
            var description = Truncate($"Mua khoa hoc #{course.Id}", 25);

            var linkResult = await paymentGatewayClient.CreatePaymentLinkAsync(
                orderCode: orderCode,
                amount: (int)Math.Round(course.Price, MidpointRounding.AwayFromZero),
                description: description,
                returnUrl: appSettings.PayOs.ReturnUrl,
                cancelUrl: appSettings.PayOs.CancelUrl,
                cancellationToken: cancellationToken);

            purchase.PaymentTransactionId = linkResult.PaymentLinkId;
            unitOfWork.CoursePurchases.Update(purchase);
            await unitOfWork.SaveChangesAsync();

            return new CoursePurchaseCheckoutResponse
            {
                PurchaseId = purchase.Id,
                OrderCode = orderCode,
                CheckoutUrl = linkResult.CheckoutUrl,
                QrCode = linkResult.QrCode,
                Amount = purchase.AmountPaid,
            };
        }

        public async Task HandlePayOsWebhookAsync(PayOsWebhookRequest webhook, CancellationToken cancellationToken)
        {
            var result = paymentGatewayClient.VerifyWebhook(webhook);

            // Không tin bất kỳ nội dung nào của webhook nếu chữ ký không hợp lệ — có thể là giả mạo.
            if (!result.IsValidSignature)
                throw new UnauthorizedAccessException("Chữ ký webhook PayOS không hợp lệ.");

            var purchase = await unitOfWork.CoursePurchases.GetByOrderCodeAsync(result.OrderCode);
            if (purchase is null)
                return; // Không khớp đơn hàng nào trong hệ thống — bỏ qua thay vì lỗi, để PayOS không retry vô ích.

            // Idempotent: PayOS có thể gọi lại webhook nhiều lần cho cùng 1 đơn hàng.
            if (purchase.PaymentStatus != PaymentStatus.Pending)
                return;

            purchase.PaymentStatus = result.IsSuccess ? PaymentStatus.Paid : PaymentStatus.Failed;
            unitOfWork.CoursePurchases.Update(purchase);
            await unitOfWork.SaveChangesAsync();
        }

        public async Task<CoursePurchaseStatusResponse> GetStatusAsync(
           int userId, int purchaseId, CancellationToken cancellationToken)
        {
            var purchase = await unitOfWork.CoursePurchases.GetByIdAsync(purchaseId)
                ?? throw new KeyNotFoundException("Không tìm thấy giao dịch mua khóa học.");

            if (purchase.UserId != userId)
                throw new UnauthorizedAccessException("Bạn không có quyền xem giao dịch này.");

            var course = purchase.Course ?? await unitOfWork.Courses.GetByIdAsync(purchase.CourseId);

            return new CoursePurchaseStatusResponse
            {
                PurchaseId = purchase.Id,
                CourseId = purchase.CourseId,
                CourseTitle = course?.Title ?? string.Empty,
                PaymentStatus = purchase.PaymentStatus,
                AmountPaid = purchase.AmountPaid,
                PurchasedAt = purchase.PurchasedAt,
            };
        }

        public async Task<List<PurchasedCourseResponse>> GetMyPurchasedCoursesAsync(
            int userId, CancellationToken cancellationToken)
        {
            var purchases = await unitOfWork.CoursePurchases.GetPurchasedCoursesByUserAsync(userId);

            return purchases
                .Select(x => new PurchasedCourseResponse
                {
                    PurchaseId = x.Id,
                    CourseId = x.CourseId,
                    CourseTitle = x.Course.Title,
                    AmountPaid = x.AmountPaid,
                    PurchasedAt = x.PurchasedAt,
                })
                .ToList();
        }

        /// <summary>
        /// PayOS yêu cầu orderCode là số nguyên dương duy nhất cho mỗi payment link, và nên nằm
        /// trong khoảng Number.MAX_SAFE_INTEGER của JS (2^53-1) vì SDK phía frontend dùng JS number.
        /// Ghép unix-millis với 3 chữ số ngẫu nhiên để tránh đụng độ khi có request đồng thời.
        /// </summary>
        private static long GenerateOrderCode()
        {
            var millis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var randomSuffix = Random.Shared.Next(0, 1000);
            return millis * 1000 + randomSuffix;
        }

        private static string Truncate(string value, int maxLength)
            => value.Length <= maxLength ? value : value[..maxLength];
    }
}