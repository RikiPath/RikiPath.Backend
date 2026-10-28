using RikiPath.Domain.Entities;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Notebook;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Notebook;
using System.Net;

namespace RikiPath.Application.Services
{
    // NOTE: cần các hàm repo sau nếu chưa có:
    //   ILearnerNoteRepository.GetByUserAsync(userId), GetWithEntryCountAsync(listId)
    //   ILearnerNoteEntryRepository.GetByListIdAsync(listId)
    //   IReviewCardRepository.GetByNoteEntryIdAsync(noteEntryId) — để xoá kèm khi bỏ bookmark
    // Mọi bookmark (vocab/kanji/manual/grammar) đều tự tạo 1 ReviewCard để item xuất hiện ngay
    // trong hàng chờ ôn tập SM-2 (NextReviewDate = hôm nay, EaseFactor mặc định 2.5).
    public class NotebookService(
         IUnitOfWork unitOfWork,
         IClaimService claimService) : INotebookService
    {
        private const double DefaultEaseFactor = 2.5;

        public async Task<ApiResponse<LearnerNoteResponse>> CreateListAsync(CreateLearnerNoteRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;

                if (string.IsNullOrWhiteSpace(request.Name))
                    return ApiResponse<LearnerNoteResponse>.Fail("Tên danh sách không được để trống.");

                var list = new LearnerNote
                {
                    UserId = userId,
                    Name = request.Name.Trim(),
                    Description = request.Description,
                    CreatedDate = DateTime.UtcNow,
                };
                await unitOfWork.LearnerNotes.AddAsync(list);
                await unitOfWork.SaveChangesAsync();

                return ApiResponse<LearnerNoteResponse>.Created(MapList(list, 0));
            }
            catch (Exception ex)
            {
                return ApiResponse<LearnerNoteResponse>.Fail(
                    "Không thể tạo danh sách từ vựng.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<List<LearnerNoteResponse>>> GetMyListsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var lists = await unitOfWork.LearnerNotes.GetByUserIdAsync(userId);
                var result = new List<LearnerNoteResponse>();
                foreach (var list in lists)
                {
                    var entries = await unitOfWork.LearnerNoteEntries.GetByUserAsync(userId, list.Id);
                    result.Add(MapList(list, entries.Count));
                }

                return ApiResponse<List<LearnerNoteResponse>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<LearnerNoteResponse>>.Fail(
                    "Không thể tải danh sách sổ tay.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<LearnerNoteResponse>> UpdateListAsync(
            int listId, UpdateLearnerNoteRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                if (string.IsNullOrWhiteSpace(request.Name))
                    return ApiResponse<LearnerNoteResponse>.Fail("Tên danh sách không được để trống.");

                var list = await unitOfWork.LearnerNotes.GetByIdAsync(listId);
                if (list is null)
                    return ApiResponse<LearnerNoteResponse>.NotFound($"Không tìm thấy danh sách Id = {listId}.");
                if (list.UserId != userId)
                    return ApiResponse<LearnerNoteResponse>.Fail("Danh sách này không thuộc về bạn.", HttpStatusCode.Forbidden);

                list.Name = request.Name.Trim();
                list.Description = request.Description;
                unitOfWork.LearnerNotes.Update(list);
                await unitOfWork.SaveChangesAsync();

                var entries = await unitOfWork.LearnerNoteEntries.GetByUserAsync(userId, list.Id);
                return ApiResponse<LearnerNoteResponse>.Success(MapList(list, entries.Count));
            }
            catch (Exception ex)
            {
                return ApiResponse<LearnerNoteResponse>.Fail(
                    "Không thể cập nhật danh sách.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse> DeleteListAsync(int listId, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var list = await unitOfWork.LearnerNotes.GetByIdAsync(listId);
                if (list is null)
                    return ApiResponse.NotFound($"Không tìm thấy danh sách Id = {listId}.");
                if (list.UserId != userId)
                    return ApiResponse.Fail("Danh sách này không thuộc về bạn.", HttpStatusCode.Forbidden);

                unitOfWork.LearnerNotes.Remove(list); // giả định DB cascade xoá LearnerNoteEntry + ReviewCard liên quan
                await unitOfWork.SaveChangesAsync();

                return ApiResponse.Success();
            }
            catch (Exception ex)
            {
                return ApiResponse.Fail(
                    "Không thể xoá danh sách.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<List<NotebookEntryResponse>>> GetListEntriesAsync(int listId, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var list = await unitOfWork.LearnerNotes.GetByIdAsync(listId);
                if (list is null)
                    return ApiResponse<List<NotebookEntryResponse>>.NotFound($"Không tìm thấy danh sách Id = {listId}.");
                if (list.UserId != userId)
                    return ApiResponse<List<NotebookEntryResponse>>.Fail("Danh sách này không thuộc về bạn.", HttpStatusCode.Forbidden);

                var entries = await unitOfWork.LearnerNoteEntries.GetByUserAsync(userId, list.Id);
                return ApiResponse<List<NotebookEntryResponse>>.Success(entries.Select(MapEntry).ToList());
            }
            catch (Exception ex)
            {
                return ApiResponse<List<NotebookEntryResponse>>.Fail(
                    "Không thể tải nội dung danh sách.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<NotebookEntryResponse>> BookmarkVocabularyAsync(BookmarkVocabularyRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var list = await unitOfWork.LearnerNotes.GetByIdAsync(request.LearnerNoteId);
                if (list is null)
                    return ApiResponse<NotebookEntryResponse>.NotFound("Không tìm thấy danh sách.");
                if (list.UserId != userId)
                    return ApiResponse<NotebookEntryResponse>.Fail("Danh sách này không thuộc về bạn.", HttpStatusCode.Forbidden);

                // Kiểm tra quyền hiển thị Learner trước khi bookmark Vocabulary.
                // Không cho phép đoán ID để lưu nội dung ẩn hoặc đã xóa.
                var vocab = await unitOfWork.Vocabularies.GetPublicByIdAsync(request.VocabularyId, cancellationToken);
                if (vocab is null)
                    return ApiResponse<NotebookEntryResponse>.NotFound("Không tìm thấy từ vựng trong ngân hàng chung.");

                // Retry tuần tự cùng một bookmark trong cùng danh sách trả entry hiện có.
                var existingEntries = await unitOfWork.LearnerNoteEntries.GetByUserAsync(userId, list.Id);
                var existingEntry = existingEntries.FirstOrDefault(entry =>
                    entry.VocabularyId == vocab.Id && !entry.IsDeleted);
                if (existingEntry is not null)
                    return ApiResponse<NotebookEntryResponse>.Success(MapEntry(existingEntry));

                var note = new LearnerNoteEntry
                {
                    LearnerNoteId = list.Id,
                    VocabularyId = vocab.Id,
                    CreatedDate = DateTime.UtcNow,
                };
                var reviewItem = await AddEntryWithReviewCardAsync(userId, note, cancellationToken);

                note.Vocabulary = vocab;
                var response = MapEntry(note);
                response.ReviewCardId = reviewItem.Id;
                return ApiResponse<NotebookEntryResponse>.Created(response);
            }
            catch (Exception)
            {
                return ApiResponse<NotebookEntryResponse>.Fail(
                    "Không thể bookmark từ vựng.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<NotebookEntryResponse>> BookmarkKanjiAsync(BookmarkKanjiRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var list = await unitOfWork.LearnerNotes.GetByIdAsync(request.LearnerNoteId);
                if (list is null)
                    return ApiResponse<NotebookEntryResponse>.NotFound("Không tìm thấy danh sách.");
                if (list.UserId != userId)
                    return ApiResponse<NotebookEntryResponse>.Fail("Danh sách này không thuộc về bạn.", HttpStatusCode.Forbidden);

                // Kiểm tra quyền hiển thị Learner trước khi bookmark Kanji.
                // Không cho phép đoán ID để lưu nội dung ẩn hoặc đã xóa.
                var kanji = await unitOfWork.Kanjis.GetPublicByIdAsync(request.KanjiId, cancellationToken);
                if (kanji is null)
                    return ApiResponse<NotebookEntryResponse>.NotFound("Không tìm thấy Kanji trong ngân hàng chung.");

                // Retry tuần tự cùng một bookmark trong cùng danh sách trả entry hiện có.
                var existingEntries = await unitOfWork.LearnerNoteEntries.GetByUserAsync(userId, list.Id);
                var existingEntry = existingEntries.FirstOrDefault(entry =>
                    entry.KanjiId == kanji.Id && !entry.IsDeleted);
                if (existingEntry is not null)
                    return ApiResponse<NotebookEntryResponse>.Success(MapEntry(existingEntry));

                var note = new LearnerNoteEntry
                {
                    LearnerNoteId = list.Id,
                    KanjiId = kanji.Id,
                    CreatedDate = DateTime.UtcNow,
                };
                var reviewItem = await AddEntryWithReviewCardAsync(userId, note, cancellationToken);

                note.Kanji = kanji;
                var response = MapEntry(note);
                response.ReviewCardId = reviewItem.Id;
                return ApiResponse<NotebookEntryResponse>.Created(response);
            }
            catch (Exception)
            {
                return ApiResponse<NotebookEntryResponse>.Fail(
                    "Không thể bookmark Kanji.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse<GrammarBookmarkResponse>> BookmarkGrammarAsync(int grammarPointId, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                // Kiểm tra quyền hiển thị Learner trước khi bookmark Grammar.
                // Không cho phép đoán ID để lưu nội dung ẩn hoặc đã xóa.
                var grammar = await unitOfWork.GrammarPatterns.GetPublicByIdAsync(grammarPointId, cancellationToken);
                if (grammar is null)
                    return ApiResponse<GrammarBookmarkResponse>.NotFound("Không tìm thấy điểm ngữ pháp.");

                var reviewItem = new ReviewCard
                {
                    UserId = userId,
                    // Grammar từ public query là detached graph vì dùng AsNoTracking.
                    // Chỉ lưu khóa ngoại để EF không insert lại Grammar, mapping và CertificateLevel đã tồn tại.
                    GrammarPatternId = grammar.Id,
                    Repetitions = 0,
                    EaseFactor = DefaultEaseFactor,
                    IntervalDays = 0,
                    NextReviewDate = DateTime.UtcNow.Date,
                };
                await unitOfWork.ReviewCards.AddAsync(reviewItem);
                await unitOfWork.SaveChangesAsync();

                return ApiResponse<GrammarBookmarkResponse>.Created(new GrammarBookmarkResponse
                {
                    ReviewCardId = reviewItem.Id,
                    GrammarPatternId = grammar.Id,
                    GrammarTitle = grammar.Title,
                });
            }
            catch (Exception ex)
            {
                return ApiResponse<GrammarBookmarkResponse>.Fail(
                    "Không thể bookmark ngữ pháp.", HttpStatusCode.InternalServerError, errors: BuildDebugErrors(ex));
            }
        }

        public async Task<ApiResponse<NotebookEntryResponse>> AddManualEntryAsync(AddManualEntryRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;

                if (string.IsNullOrWhiteSpace(request.Word) || string.IsNullOrWhiteSpace(request.Meaning))
                    return ApiResponse<NotebookEntryResponse>.Fail("Từ và nghĩa không được để trống.");

                var list = await unitOfWork.LearnerNotes.GetByIdAsync(request.LearnerNoteId);
                if (list is null)
                    return ApiResponse<NotebookEntryResponse>.NotFound("Không tìm thấy danh sách.");
                if (list.UserId != userId)
                    return ApiResponse<NotebookEntryResponse>.Fail("Danh sách này không thuộc về bạn.", HttpStatusCode.Forbidden);

                var normalizedWord = request.Word.Trim();
                var normalizedReading = request.Reading?.Trim();
                var normalizedMeaning = request.Meaning.Trim();

                // Retry tuần tự cùng một manual mutation trả lại entry đã có, không tạo thêm dữ liệu trùng.
                var existingEntries = await unitOfWork.LearnerNoteEntries.GetByUserAsync(userId, list.Id);
                var existingEntry = existingEntries.FirstOrDefault(entry =>
                    entry.VocabularyId is null &&
                    entry.KanjiId is null &&
                    string.Equals(entry.ManualWord, normalizedWord, StringComparison.Ordinal) &&
                    string.Equals(entry.ManualReading, normalizedReading, StringComparison.Ordinal) &&
                    string.Equals(entry.ManualMeaning, normalizedMeaning, StringComparison.Ordinal) &&
                    string.Equals(entry.Note, request.Note, StringComparison.Ordinal));
                if (existingEntry is not null)
                {
                    var existingReviewCard = await unitOfWork.ReviewCards.GetByNoteEntryIdAsync(existingEntry.Id);
                    var existingResponse = MapEntry(existingEntry);
                    existingResponse.ReviewCardId = existingReviewCard?.Id;
                    return ApiResponse<NotebookEntryResponse>.Success(existingResponse);
                }

                var note = new LearnerNoteEntry
                {
                    LearnerNoteId = list.Id,
                    ManualWord = normalizedWord,
                    ManualReading = normalizedReading,
                    ManualMeaning = normalizedMeaning,
                    Note = request.Note,
                    CreatedDate = DateTime.UtcNow,
                };
                var reviewItem = await AddEntryWithReviewCardAsync(userId, note, cancellationToken);

                var response = MapEntry(note);
                response.ReviewCardId = reviewItem.Id;
                return ApiResponse<NotebookEntryResponse>.Created(response);
            }
            catch (Exception)
            {
                return ApiResponse<NotebookEntryResponse>.Fail(
                    "Không thể thêm ghi chú thủ công.", HttpStatusCode.InternalServerError);
            }
        }

        public async Task<ApiResponse> RemoveEntryAsync(int noteEntryId, CancellationToken cancellationToken)
        {
            try
            {
                var userId = claimService.GetUserClaim().Id;
                var note = await unitOfWork.LearnerNoteEntries.GetByIdAsync(noteEntryId, cancellationToken);
                if (note is null)
                    return ApiResponse.NotFound($"Không tìm thấy mục sổ tay Id = {noteEntryId}.");

                var list = await unitOfWork.LearnerNotes.GetByIdAsync(note.LearnerNoteId, cancellationToken);
                // Chỉ cho phép Learner xóa entry thuộc Notebook của chính mình.
                // Không tin noteEntryId nếu chưa xác thực ownership qua LearnerNote.
                if (list is null || list.UserId != userId)
                    return ApiResponse.Fail("Bạn không có quyền xoá mục này.", HttpStatusCode.Forbidden);

                var linkedReviewCard = await unitOfWork.ReviewCards.GetByNoteEntryIdAsync(noteEntryId);
                if (linkedReviewCard is not null)
                    unitOfWork.ReviewCards.Remove(linkedReviewCard);

                unitOfWork.LearnerNoteEntries.Remove(note);
                // ReviewCard và entry được xóa trong cùng một SaveChanges để không thể half-delete.
                await unitOfWork.SaveChangesAsync(cancellationToken);

                return ApiResponse.Success();
            }
            catch (Exception)
            {
                return ApiResponse.Fail(
                    "Không thể xoá mục sổ tay.", HttpStatusCode.InternalServerError);
            }
        }

        private async Task<ReviewCard> AddEntryWithReviewCardAsync(
            int userId,
            LearnerNoteEntry noteEntry,
            CancellationToken cancellationToken)
        {
            // Gói entry và ReviewCard trong cùng một SaveChanges; EF sẽ dùng transaction cho toàn bộ change set.
            await unitOfWork.LearnerNoteEntries.AddAsync(noteEntry, cancellationToken);
            var reviewItem = await EnrollIntoReviewQueueAsync(userId, noteEntry, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return reviewItem;
        }

        private async Task<ReviewCard> EnrollIntoReviewQueueAsync(
            int userId,
            LearnerNoteEntry noteEntry,
            CancellationToken cancellationToken)
        {
            var reviewItem = new ReviewCard
            {
                UserId = userId,
                LearnerNoteEntry = noteEntry,
                Repetitions = 0,
                EaseFactor = DefaultEaseFactor,
                IntervalDays = 0,
                NextReviewDate = DateTime.UtcNow.Date, // due ngay lần học đầu tiên
            };
            await unitOfWork.ReviewCards.AddAsync(reviewItem, cancellationToken);
            return reviewItem;
        }

        private static LearnerNoteResponse MapList(LearnerNote list, int entryCount) => new()
        {
            Id = list.Id,
            Name = list.Name,
            Description = list.Description,
            EntryCount = entryCount,
            CreatedAt = list.CreatedDate.Value,
        };

        private static NotebookEntryResponse MapEntry(LearnerNoteEntry note)
        {
            var isFromVocabBank = note.Vocabulary is not null;
            var isFromKanjiBank = note.Kanji is not null;

            return new NotebookEntryResponse
            {
                NoteEntryId = note.Id,
                ReviewCardId = note.ReviewCards?
                    .Where(card => !card.IsDeleted)
                    .OrderBy(card => card.Id)
                    .Select(card => (int?)card.Id)
                    .FirstOrDefault(),
                VocabularyId = note.VocabularyId,
                KanjiId = note.KanjiId,
                ContentType = isFromVocabBank ? "vocabulary" : isFromKanjiBank ? "kanji" : "manual",
                Term = isFromVocabBank ? note.Vocabulary!.Word
                     : isFromKanjiBank ? note.Kanji!.Character
                     : note.ManualWord ?? string.Empty,
                Reading = isFromVocabBank ? note.Vocabulary!.Reading
                        : isFromKanjiBank ? note.Kanji!.OnYomi
                        : note.ManualReading,
                Meaning = isFromVocabBank ? note.Vocabulary!.Meaning
                        : isFromKanjiBank ? note.Kanji!.Meaning
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
