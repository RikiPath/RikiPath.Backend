using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RikiPath.WebApi.Pages
{
    public class PaymentSuccessModel : PageModel
    {
        public string Message { get; set; } = string.Empty;
        public string DeepLinkUrl { get; set; } = string.Empty;

        public void OnGet(string status)
        {
            if (status == "success")
            {
                Message = "Thanh toán thành công!";
                DeepLinkUrl = "rikipath://payment-success";
            }
            else
            {
                Message = "Thanh toán đã bị hủy hoặc thất bại!";
                DeepLinkUrl = "rikipath://payment-cancel";
            }
        }
    }
}
