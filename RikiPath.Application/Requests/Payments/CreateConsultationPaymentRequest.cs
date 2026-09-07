namespace RikiPath.Application.Requests.Payments
{
    public class CreateConsultationPaymentRequest
    {
        public int ConsultationPackageId { get; set; }

        /// <summary>URL PayOS redirect về sau khi thanh toán thành công.</summary>
        public string ReturnUrl { get; set; } = string.Empty;

        /// <summary>URL PayOS redirect về nếu người dùng huỷ thanh toán.</summary>
        public string CancelUrl { get; set; } = string.Empty;
    }
}
