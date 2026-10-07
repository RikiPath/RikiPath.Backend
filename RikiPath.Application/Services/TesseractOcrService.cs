//using Tesseract;
//using RikiPath.Application.IServices;

//namespace RikiPath.Application.Services;

//public class TesseractOcrService : IOcrService
//{
//    private readonly string _dataPath;

//    public TesseractOcrService()
//    {
//        _dataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
//        if (!File.Exists(Path.Combine(_dataPath, "jpn.traineddata")))
//            throw new InvalidOperationException(
//                "Thiếu bộ dữ liệu OCR tiếng Nhật jpn.traineddata trong thư mục tessdata.");
//    }

//    public async Task<string> RecognizeJapaneseAsync(
//        Stream imageStream,
//        string fileName,
//        CancellationToken cancellationToken = default)
//    {
//        var tempPath = Path.Combine(Path.GetTempPath(), $"rikipath-ocr-{Guid.NewGuid():N}{Path.GetExtension(fileName)}");
//        try
//        {
//            await using (var output = File.Create(tempPath))
//                await imageStream.CopyToAsync(output, cancellationToken);

//            using var engine = new TesseractEngine(_dataPath, "jpn", EngineMode.Default);
//            using var pix = Pix.LoadFromFile(tempPath);
//            using var page = engine.Process(pix);
//            return page.GetText().Trim();
//        }
//        finally
//        {
//            if (File.Exists(tempPath))
//                File.Delete(tempPath);
//        }
//    }
//}
