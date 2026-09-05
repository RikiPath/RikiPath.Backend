using Domain.Entities;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Notebook;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Notebook;
using System.Net;

namespace RikiPath.Application.Services
{
    // NOTE: cần các hàm repo sau nếu chưa có:
    //   IVocabularyListRepository.GetByUserAsync(userId), GetWithEntryCountAsync(listId)
    //   IVocabularyNoteEntryRepository.GetByListIdAsync(listId)
    //   IReviewItemRepository.GetByNoteEntryIdAsync(noteEntryId) — để xoá kèm khi bỏ bookmark
    // Mọi bookmark (vocab/kanji/manual/grammar) đều tự tạo 1 ReviewItem để item xuất hiện ngay
    // trong hàng chờ ôn tập SM-2 (NextReviewDate = hôm nay, EaseFactor mặc định 2.5).
    public class NotebookService(IUnitOfWork unitOfWork) : INotebookService
    {
        private const double DefaultEaseFactor = 2.5;

        public async Task<ApiResponse<VocabularyListResponse>> CreateListAsync(
            int userId, CreateVocabularyListRequest request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return ApiResponse<VocabularyListResponse>.Fail("Tên danh sách không được để trống.");

                var list = new VocabularyList
                {
                    UserId = userId,
                    Name = request.Name.Trim(),
                    Description = request.Description,
                    CreatedDate = DateTime.UtcNow,
                };
                await unitOfWork.VocabularyLists.AddAsync(list);
                await unitOfWork.SaveChangesAsync();

                return ApiResponse<VocabularyListResponse>.Created(MapList(list, 0));
            }
            catch (Exception ex)
            {
                return ApiResponse<VocabularyListResponse>.Fail(
                    "Không thể tạo danh sách từ vựng.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<List<VocabularyListResponse>>> GetMyListsAsync(int userId, CancellationToken cancellationToken)
        {
            try
            {
                var lists = await unitOfWork.VocabularyLists.GetByUserIdAsync(userId);
                var result = new List<VocabularyListResponse>();
                foreach (var list in lists)
                {
                    var entries = await unitOfWork.VocabularyNoteEntries.GetByUserAsync(userId, list.Id);
                    result.Add(MapList(list, entries.Count));
                }

                return ApiResponse<List<VocabularyListResponse>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<VocabularyListResponse>>.Fail(
                    "Không thể tải danh sách sổ tay.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<VocabularyListResponse>> UpdateListAsync(
            int userId, int listId, UpdateVocabularyListRequest request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                    return ApiResponse<VocabularyListResponse>.Fail("Tên danh sách không được để trống.");

                var list = await unitOfWork.VocabularyLists.GetByIdAsync(listId);
                if (list is null)
                    return ApiResponse<VocabularyListResponse>.NotFound($"Không tìm thấy danh sách Id = {listId}.");
                if (list.UserId != userId)
                    return ApiResponse<VocabularyListResponse>.Fail("Danh sách này không thuộc về bạn.", HttpStatusCode.Forbidden);

                list.Name = request.Name.Trim();
                list.Description = request.Description;
                unitOfWork.VocabularyLists.Update(list);
                await unitOfWork.SaveChangesAsync();

                var entries = await unitOfWork.VocabularyNoteEntries.GetByUserAsync(userId, list.Id);
                return ApiResponse<VocabularyListResponse>.Success(MapList(list, entries.Count));
            }
            catch (Exception ex)
            {
                return ApiResponse<VocabularyListResponse>.Fail(
                    "Không thể cập nhật danh sách.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse> DeleteListAsync(int userId, int listId, CancellationToken cancellationToken)
        {
            try
            {
                var list = await unitOfWork.VocabularyLists.GetByIdAsync(listId);
                if (list is null)
                    return ApiResponse.NotFound($"Không tìm thấy danh sách Id = {listId}.");
                if (list.UserId != userId)
                    return ApiResponse.Fail("Danh sách này không thuộc về bạn.", HttpStatusCode.Forbidden);

                unitOfWork.VocabularyLists.Remove(list); // giả định DB cascade xoá VocabularyNoteEntry + ReviewItem liên quan
                await unitOfWork.SaveChangesAsync();

                return ApiResponse.Success();
            }
            catch (Exception ex)
            {
                return ApiResponse.Fail(
                    "Không thể xoá danh sách.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<List<NotebookEntryResponse>>> GetListEntriesAsync(
            int userId, int listId, CancellationToken cancellationToken)
        {
            try
            {
                var list = await unitOfWork.VocabularyLists.GetByIdAsync(listId);
                if (list is null)
                    return ApiResponse<List<NotebookEntryResponse>>.NotFound($"Không tìm thấy danh sách Id = {listId}.");
                if (list.UserId != userId)
                    return ApiResponse<List<NotebookEntryResponse>>.Fail("Danh sách này không thuộc về bạn.", HttpStatusCode.Forbidden);

                var entries = await unitOfWork.VocabularyNoteEntries.GetByUserAsync(userId, list.Id);
                return ApiResponse<List<NotebookEntryResponse>>.Success(entries.Select(MapEntry).ToList());
            }
            catch (Exception ex)
            {
                return ApiResponse<List<NotebookEntryResponse>>.Fail(
                    "Không thể tải nội dung danh sách.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<NotebookEntryResponse>> BookmarkVocabularyAsync(
            int userId, BookmarkVocabularyRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var list = await unitOfWork.VocabularyLists.GetByIdAsync(request.VocabularyListId);
                if (list is null)
                    return ApiResponse<NotebookEntryResponse>.NotFound("Không tìm thấy danh sách.");
                if (list.UserId != userId)
                    return ApiResponse<NotebookEntryResponse>.Fail("Danh sách này không thuộc về bạn.", HttpStatusCode.Forbidden);

                var vocab = await unitOfWork.VocabularyEntries.GetByIdAsync(request.VocabularyEntryId);
                if (vocab is null)
                    return ApiResponse<NotebookEntryResponse>.NotFound("Không tìm thấy từ vựng trong ngân hàng chung.");

                var note = new VocabularyNoteEntry
                {
                    VocabularyListId = list.Id,
                    VocabularyEntryId = vocab.Id,
                    CreatedDate = DateTime.UtcNow,
                };
                await unitOfWork.VocabularyNoteEntries.AddAsync(note);
                await unitOfWork.SaveChangesAsync();

                var reviewItem = await EnrollIntoReviewQueueAsync(userId, noteEntry: note);
                await unitOfWork.SaveChangesAsync();

                note.VocabularyEntry = vocab;
                var response = MapEntry(note);
                response.ReviewItemId = reviewItem.Id;
                return ApiResponse<NotebookEntryResponse>.Created(response);
            }
            catch (Exception ex)
            {
                return ApiResponse<NotebookEntryResponse>.Fail(
                    "Không thể bookmark từ vựng.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<NotebookEntryResponse>> BookmarkKanjiAsync(
            int userId, BookmarkKanjiRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var list = await unitOfWork.VocabularyLists.GetByIdAsync(request.VocabularyListId);
                if (list is null)
                    return ApiResponse<NotebookEntryResponse>.NotFound("Không tìm thấy danh sách.");
                if (list.UserId != userId)
                    return ApiResponse<NotebookEntryResponse>.Fail("Danh sách này không thuộc về bạn.", HttpStatusCode.Forbidden);

                var kanji = await unitOfWork.KanjiEntries.GetByIdAsync(request.KanjiEntryId);
                if (kanji is null)
                    return ApiResponse<NotebookEntryResponse>.NotFound("Không tìm thấy Kanji trong ngân hàng chung.");

                var note = new VocabularyNoteEntry
                {
                    VocabularyListId = list.Id,
                    KanjiEntryId = kanji.Id,
                    CreatedDate = DateTime.UtcNow,
                };
                await unitOfWork.VocabularyNoteEntries.AddAsync(note);
                await unitOfWork.SaveChangesAsync();

                var reviewItem = await EnrollIntoReviewQueueAsync(userId, noteEntry: note);
                await unitOfWork.SaveChangesAsync();

                note.KanjiEntry = kanji;
                var response = MapEntry(note);
                response.ReviewItemId = reviewItem.Id;
                return ApiResponse<NotebookEntryResponse>.Created(response);
            }
            catch (Exception ex)
            {
                return ApiResponse<NotebookEntryResponse>.Fail(
                    "Không thể bookmark Kanji.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<GrammarBookmarkResponse>> BookmarkGrammarAsync(
            int userId, int grammarPointId, CancellationToken cancellationToken)
        {
            try
            {
                var grammar = await unitOfWork.GrammarPoints.GetByIdAsync(grammarPointId);
                if (grammar is null)
                    return ApiResponse<GrammarBookmarkResponse>.NotFound("Không tìm thấy điểm ngữ pháp.");

                var reviewItem = new ReviewItem
                {
                    UserId = userId,
                    GrammarPoint = grammar,
                    Repetitions = 0,
                    EaseFactor = DefaultEaseFactor,
                    IntervalDays = 0,
                    NextReviewDate = DateTime.UtcNow.Date,
                };
                await unitOfWork.ReviewItems.AddAsync(reviewItem);
                await unitOfWork.SaveChangesAsync();

                return ApiResponse<GrammarBookmarkResponse>.Created(new GrammarBookmarkResponse
                {
                    ReviewItemId = reviewItem.Id,
                    GrammarPointId = grammar.Id,
                    GrammarTitle = grammar.Title,
                });
            }
            catch (Exception ex)
            {
                return ApiResponse<GrammarBookmarkResponse>.Fail(
                    "Không thể bookmark ngữ pháp.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<NotebookEntryResponse>> AddManualEntryAsync(
            int userId, AddManualEntryRequest request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Word) || string.IsNullOrWhiteSpace(request.Meaning))
                    return ApiResponse<NotebookEntryResponse>.Fail("Từ và nghĩa không được để trống.");

                var list = await unitOfWork.VocabularyLists.GetByIdAsync(request.VocabularyListId);
                if (list is null)
                    return ApiResponse<NotebookEntryResponse>.NotFound("Không tìm thấy danh sách.");
                if (list.UserId != userId)
                    return ApiResponse<NotebookEntryResponse>.Fail("Danh sách này không thuộc về bạn.", HttpStatusCode.Forbidden);

                var note = new VocabularyNoteEntry
                {
                    VocabularyListId = list.Id,
                    ManualWord = request.Word.Trim(),
                    ManualReading = request.Reading?.Trim(),
                    ManualMeaning = request.Meaning.Trim(),
                    Note = request.Note,
                    CreatedDate = DateTime.UtcNow,
                };
                await unitOfWork.VocabularyNoteEntries.AddAsync(note);
                await unitOfWork.SaveChangesAsync();

                var reviewItem = await EnrollIntoReviewQueueAsync(userId, noteEntry: note);
                await unitOfWork.SaveChangesAsync();

                var response = MapEntry(note);
                response.ReviewItemId = reviewItem.Id;
                return ApiResponse<NotebookEntryResponse>.Created(response);
            }
            catch (Exception ex)
            {
                return ApiResponse<NotebookEntryResponse>.Fail(
                    "Không thể thêm ghi chú thủ công.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse> RemoveEntryAsync(int userId, int noteEntryId, CancellationToken cancellationToken)
        {
            try
            {
                var note = await unitOfWork.VocabularyNoteEntries.GetByIdAsync(noteEntryId);
                if (note is null)
                    return ApiResponse.NotFound($"Không tìm thấy mục sổ tay Id = {noteEntryId}.");

                var list = await unitOfWork.VocabularyLists.GetByIdAsync(note.VocabularyListId);
                if (list is null || list.UserId != userId)
                    return ApiResponse.Fail("Bạn không có quyền xoá mục này.", HttpStatusCode.Forbidden);

                var linkedReviewItem = await unitOfWork.ReviewItems.GetByNoteEntryIdAsync(noteEntryId);
                if (linkedReviewItem is not null)
                    unitOfWork.ReviewItems.Remove(linkedReviewItem);

                unitOfWork.VocabularyNoteEntries.Remove(note);
                await unitOfWork.SaveChangesAsync();

                return ApiResponse.Success();
            }
            catch (Exception ex)
            {
                return ApiResponse.Fail(
                    "Không thể xoá mục sổ tay.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        private async Task<ReviewItem> EnrollIntoReviewQueueAsync(int userId, VocabularyNoteEntry noteEntry)
        {
            var reviewItem = new ReviewItem
            {
                UserId = userId,
                VocabularyNoteEntry = noteEntry,
                Repetitions = 0,
                EaseFactor = DefaultEaseFactor,
                IntervalDays = 0,
                NextReviewDate = DateTime.UtcNow.Date, // due ngay lần học đầu tiên
            };
            await unitOfWork.ReviewItems.AddAsync(reviewItem);
            return reviewItem;
        }

        private static VocabularyListResponse MapList(VocabularyList list, int entryCount) => new()
        {
            Id = list.Id,
            Name = list.Name,
            Description = list.Description,
            EntryCount = entryCount,
            CreatedAt = list.CreatedDate.Value,
        };

        private static NotebookEntryResponse MapEntry(VocabularyNoteEntry note)
        {
            var isFromVocabBank = note.VocabularyEntry is not null;
            var isFromKanjiBank = note.KanjiEntry is not null;

            return new NotebookEntryResponse
            {
                NoteEntryId = note.Id,
                ContentType = isFromVocabBank ? "vocabulary" : isFromKanjiBank ? "kanji" : "manual",
                Term = isFromVocabBank ? note.VocabularyEntry!.Word
                     : isFromKanjiBank ? note.KanjiEntry!.Character
                     : note.ManualWord ?? string.Empty,
                Reading = isFromVocabBank ? note.VocabularyEntry!.Reading
                        : isFromKanjiBank ? note.KanjiEntry!.OnYomi
                        : note.ManualReading,
                Meaning = isFromVocabBank ? note.VocabularyEntry!.Meaning
                        : isFromKanjiBank ? note.KanjiEntry!.Meaning
                        : note.ManualMeaning ?? string.Empty,
                Note = note.Note,
                AddedAt = note.CreatedDate.Value,
            };
        }

        private static List<string> BuildDebugErrors(Exception ex)
        {
            var errors = new List<string> { $"{ex.GetType().Name}: {ex.Message}" };
            if (ex.InnerException is not null)
                errors.Add($"Inner: {ex.InnerException.Message}");
#if DEBUG
            if (!string.IsNullOrEmpty(ex.StackTrace))
                errors.Add(ex.StackTrace);
#endif
            return errors;
        }
    }
}
