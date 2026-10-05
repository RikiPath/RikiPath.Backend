using System.Net;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.JapaneseDictionary;
using RikiPath.Domain.Entities;

namespace RikiPath.Application.Services;

public class JapaneseDictionaryService(
    IUnitOfWork unitOfWork,
    IClaimService claimService) : IJapaneseDictionaryService
{
    public async Task<ApiResponse<List<JapaneseDictionaryEntryResponse>>> SearchAsync(
        string readingKana,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(readingKana))
            return ApiResponse<List<JapaneseDictionaryEntryResponse>>.Success([]);

        var userId = claimService.GetUserClaim().Id;
        var entries = await unitOfWork.JapaneseDictionaryEntries.SearchAsync(
            userId, readingKana.Trim(), cancellationToken);

        return ApiResponse<List<JapaneseDictionaryEntryResponse>>.Success(
            entries.Select(Map).ToList());
    }

    public async Task<ApiResponse<JapaneseDictionaryEntryResponse>> AddPersonalAsync(
        CreatePersonalDictionaryEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Surface)
            || string.IsNullOrWhiteSpace(request.ReadingKana)
            || string.IsNullOrWhiteSpace(request.Meaning))
        {
            return ApiResponse<JapaneseDictionaryEntryResponse>.Fail(
                "Chữ tiếng Nhật, cách đọc kana và nghĩa không được để trống.");
        }

        var entry = new JapaneseDictionaryEntry
        {
            UserId = claimService.GetUserClaim().Id,
            Surface = request.Surface.Trim(),
            ReadingKana = request.ReadingKana.Trim(),
            ReadingRomaji = request.ReadingRomaji.Trim(),
            Meaning = request.Meaning.Trim(),
            PartOfSpeech = request.PartOfSpeech?.Trim(),
            IsKatakana = request.IsKatakana,
            Source = "Personal",
            CreatedDate = DateTime.UtcNow,
        };

        await unitOfWork.JapaneseDictionaryEntries.AddAsync(entry, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ApiResponse<JapaneseDictionaryEntryResponse>.Success(Map(entry), HttpStatusCode.Created);
    }

    private static JapaneseDictionaryEntryResponse Map(JapaneseDictionaryEntry entry) => new()
    {
        Id = entry.Id,
        Surface = entry.Surface,
        ReadingKana = entry.ReadingKana,
        ReadingRomaji = entry.ReadingRomaji,
        Meaning = entry.Meaning,
        PartOfSpeech = entry.PartOfSpeech,
        IsKatakana = entry.IsKatakana,
        IsPersonal = entry.Source == "Personal",
    };
}
