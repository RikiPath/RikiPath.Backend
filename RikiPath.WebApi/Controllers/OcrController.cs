//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using RikiPath.Application.IServices;

//namespace RikiPath.WebApi.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class OcrController : ControllerBase
//    {
//        private readonly IGeminiOcrService _ocrService;

//        public OcrController(IGeminiOcrService ocrService)
//        {
//            _ocrService = ocrService;
//        }

//        [HttpPost("scan-japanese")]
//        public async Task<IActionResult> ScanJapaneseHandwriting(IFormFile file)
//        {
//            if (file == null || file.Length == 0)
//                return BadRequest(new { message = "Vui lòng chọn một file ảnh." });

//            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
//            if (!allowedTypes.Contains(file.ContentType))
//                return BadRequest(new { message = "Chỉ chấp nhận file ảnh JPG, PNG, WEBP." });

//            try
//            {
//                var jsonResult = await _ocrService.ScanJapaneseHandwritingAsync(file);
//                return Content(jsonResult, "application/json");
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, new { message = "Lỗi khi xử lý scan ảnh", details = ex.Message });
//            }
//        }
//    }
//}