namespace RikiPath.Application.Requests.Payments
{
    public class CreateConsultationPaymentRequest
    {
        public int ConsultationPackageId { get; set; }

        public string ReturnUrl { get; set; } = string.Empty;

        public string CancelUrl { get; set; } = string.Empty;
    }
}
