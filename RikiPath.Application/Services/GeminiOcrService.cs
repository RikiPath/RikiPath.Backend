//using Microsoft.AspNetCore.Http;
//using RikiPath.Application.IClients;
//using RikiPath.Application.IServices;

//namespace RikiPath.Application.Services
//{
//    public class GeminiOcrService : IGeminiOcrService
//    {
//        private readonly IGeminiOcrClient _geminiClient;

//        public GeminiOcrService(IGeminiOcrClient geminiClient)
//        {
//            _geminiClient = geminiClient;
//        }

//        public async Task<string> ScanJapaneseHandwritingAsync(IFormFile file)
//        {
//            using var memoryStream = new MemoryStream();
//            await file.CopyToAsync(memoryStream);
//            var base64Image = Convert.ToBase64String(memoryStream.ToArray());

//            string prompt = @"
//                Bạn là hệ thống OCR chuyên đọc bài tập tiếng Nhật. Hãy đọc hình ảnh và thực hiện:
//                1. Trích xuất chính xác văn bản trong hình (bao gồm chữ in và chữ viết tay).
//                2. Phân tách riêng phần chữ người học viết tay.
//                3. Trả về KẾT QUẢ DUY NHẤT dưới dạng JSON hợp lệ (Không chứa markdown ```json):
//                {
//                  ""fullText"": ""Toàn bộ câu trong bài"",
//                  ""handwritingText"": ""Phần chữ viết tay"",
//                  ""translation"": ""Dịch nghĩa tiếng Việt""
//                }";

//            return await _geminiClient.ScanHandwritingAsync(prompt, base64Image, file.ContentType);
//        }
//    }
//}
