using Microsoft.EntityFrameworkCore;
using RikiPath.Application.Helpers;
using RikiPath.Application.Responses.Content;
using RikiPath.Domain.Enums;
using ApprovalRequest = RikiPath.Domain.Entities.ContentApprovalRequest;

namespace RikiPath.Application.Handlers
{
    /// <summary>
    /// Admin xem các yêu cầu Content Author đã gửi (thêm mới / cập nhật) kèm so sánh dữ liệu cũ (DB hiện tại) và dữ liệu đề xuất.
    /// Chỉ ĐỌC, không ghi gì. Giá trị cũ luôn đọc trực tiếp từ DB tại thời điểm xem, nên nếu dữ liệu gốc đã bị sửa từ lúc
    /// Author gửi thì Admin thấy ngay cảnh báo.
    /// </summary>
    public sealed class ContentApprovalReviewHandler(IUnitOfWork unitOfWork)
    {
        // Muốn so sánh chính xác cho loại nội dung khác (Kanji, Grammar, Lesson...): viết thêm 1 IApprovalDiffBuilder rồi thêm vào đây.
        private readonly IApprovalDiffBuilder[] _builders = { new VocabularyApprovalDiffBuilder(unitOfWork) };

        /// <summary>Danh sách yêu cầu (phân trang, mới nhất trước). Mọi bộ lọc đều tùy chọn.</summary>
        public async Task<ContentApprovalListResponse> ListAsync(
            ContentApprovalStatus? status, ImportEntityType? entityType, Guid? batchId, ImportRowAction? action,
            int page, int pageSize, CancellationToken cancellationToken)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = unitOfWork.ContentApprovalRequests.Query().Where(x => !x.IsDeleted);
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            if (entityType.HasValue) query = query.Where(x => x.EntityType == entityType.Value);
            if (batchId.HasValue) query = query.Where(x => x.BatchId == batchId.Value);
            if (action.HasValue) query = query.Where(x => x.Action == action.Value);

            var total = await query.CountAsync(cancellationToken);
            var rows = await query
                .OrderByDescending(x => x.SubmittedAt).ThenBy(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync(cancellationToken);

            return new ContentApprovalListResponse
            {
                Items = rows.Select(r => MapBase<ContentApprovalListItemResponse>(r, ApprovalSummary.Parse(r.DiffSummaryJson))).ToList(),
                TotalCount = total,
                Page = page,
                PageSize = pageSize,
            };
        }

        /// <summary>Chi tiết 1 yêu cầu: từng trường cũ → mới. Trả null nếu không tồn tại.</summary>
        public async Task<ContentApprovalDetailResponse?> GetDetailAsync(int id, CancellationToken cancellationToken)
        {
            var request = await unitOfWork.ContentApprovalRequests.Query()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
            if (request is null) return null;

            var details = await BuildDetailsAsync(new List<ApprovalRequest> { request }, cancellationToken);
            return details[0];
        }

        /// <summary>Toàn bộ yêu cầu của 1 lần import (1 lần bấm Xác nhận), mỗi dòng đã kèm so sánh. Trả null nếu batch trống.</summary>
        public async Task<ContentApprovalBatchResponse?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken)
        {
            var requests = await unitOfWork.ContentApprovalRequests.Query()
                .Where(x => x.BatchId == batchId && !x.IsDeleted)
                .OrderBy(x => x.EntityType).ThenBy(x => x.SourceRowNumber).ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);
            if (requests.Count == 0) return null;

            var details = await BuildDetailsAsync(requests, cancellationToken);
            return new ContentApprovalBatchResponse
            {
                BatchId = batchId,
                TotalCount = details.Count,
                NewCount = details.Count(d => d.Action == ImportRowAction.New),
                ModifiedCount = details.Count(d => d.Action == ImportRowAction.Modified),
                Items = details,
            };
        }

        private async Task<List<ContentApprovalDetailResponse>> BuildDetailsAsync(
            List<ApprovalRequest> requests, CancellationToken cancellationToken)
        {
            var summaries = requests.ToDictionary(r => r.Id, r => ApprovalSummary.Parse(r.DiffSummaryJson));
            var diffs = new Dictionary<int, ApprovalDiffResult>();

            foreach (var group in requests.GroupBy(r => r.EntityType))
            {
                var builder = _builders.FirstOrDefault(b => b.EntityType == group.Key);
                if (builder is null)
                {
                    foreach (var r in group) diffs[r.Id] = GenericApprovalDiff.Build(r, summaries[r.Id]);
                    continue;
                }

                foreach (var kv in await builder.BuildAsync(group.ToList(), cancellationToken))
                    diffs[kv.Key] = kv.Value;
            }

            var list = new List<ContentApprovalDetailResponse>(requests.Count);
            foreach (var r in requests)
            {
                var summary = summaries[r.Id];
                var diff = diffs.TryGetValue(r.Id, out var d) ? d : new ApprovalDiffResult();

                // Dữ liệu gốc đã đổi từ lúc Author gửi? So giá trị "cũ" lúc import (DiffSummaryJson) với giá trị DB hiện tại.
                if (r.Action == ImportRowAction.Modified && !diff.TargetMissing)
                {
                    var drifted = diff.Fields
                        .Where(f => summary.TryGetValue(f.Field, out var s)
                                    && !string.Equals(ApprovalSummary.OldPart(s), f.OldValue ?? string.Empty, StringComparison.Ordinal))
                        .Select(f => f.Label)
                        .ToList();
                    if (drifted.Count > 0)
                        diff.Warnings.Add($"Dữ liệu gốc đã thay đổi kể từ lúc gửi yêu cầu ở các trường: {string.Join(", ", drifted)}. Hãy xem lại cột 'cũ' trước khi duyệt.");
                }

                var detail = MapBase<ContentApprovalDetailResponse>(r, summary);
                detail.TargetMissing = diff.TargetMissing;
                detail.Fields = diff.Fields;
                detail.Warnings = diff.Warnings;
                list.Add(detail);
            }

            return list;
        }

        private static T MapBase<T>(ApprovalRequest r, Dictionary<string, string> summary) where T : ContentApprovalListItemResponse, new() => new()
        {
            Id = r.Id,
            BatchId = r.BatchId,
            EntityType = r.EntityType,
            Action = r.Action,
            TargetEntityId = r.TargetEntityId,
            RowKey = r.RowKey,
            Status = r.Status,
            SourceFileName = r.SourceFileName,
            SourceRowNumber = r.SourceRowNumber,
            SubmittedByUserId = r.SubmittedByUserId,
            SubmittedAt = r.SubmittedAt,
            ReviewedByName = r.ReviewedByName,
            ReviewedAt = r.ReviewedAt,
            ReviewNote = r.ReviewNote,
            ChangedFieldCount = summary.Count > 0 ? summary.Count : null,
        };
    }
}