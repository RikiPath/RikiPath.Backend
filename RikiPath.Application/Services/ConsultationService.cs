using Domain.Entities;
using Domain.Enums;
using RikiPath.Application.IServices;
using RikiPath.Application.Requests.Consultations;
using RikiPath.Application.Responses;
using RikiPath.Application.Responses.Consultations;

namespace RikiPath.Application.Services
{
    public class ConsultationService(IUnitOfWork unitOfWork) : IConsultationService
    {
        public async Task<ApiResponse<ConsultationRequestResponse>> BookMeetingAsync(
            int learnerId, BookMeetingRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var (purchase, error) = await ValidatePurchaseAsync(learnerId, request.ConsultationPurchaseId, ConsultationType.Meeting);
                if (error is not null)
                    return ApiResponse<ConsultationRequestResponse>.Fail(error);

                var slot = await unitOfWork.ConsultantAvailabilities.GetByIdAsync(request.ConsultantAvailabilityId);
                if (slot is null || slot.IsBooked)
                    return ApiResponse<ConsultationRequestResponse>.Fail("Khung giờ tư vấn không còn trống.");
                if (slot.StartTime <= DateTime.UtcNow)
                    return ApiResponse<ConsultationRequestResponse>.Fail("Khung giờ đã chọn không còn hợp lệ.");

                var consultationRequest = new ConsultationRequest
                {
                    ConsultationPurchaseId = purchase!.Id,
                    ConsultantId = slot.ConsultantId,
                    ConsultantAvailabilityId = slot.Id,
                    Status = ConsultationStatus.Assigned,
                    Question = request.Note,
                    ScheduledAt = slot.StartTime
                };

                slot.IsBooked = true;

                await unitOfWork.ConsultationRequests.AddAsync(consultationRequest);
                unitOfWork.ConsultantAvailabilities.Update(slot);
                await unitOfWork.SaveChangesAsync();

                return ApiResponse<ConsultationRequestResponse>.Success(
                    await MapToResponseAsync(consultationRequest, cancellationToken));
            }
            catch (Exception ex)
            {
                return ApiResponse<ConsultationRequestResponse>.Fail($"Không thể đặt lịch: {ex.Message}");
            }
        }

        public async Task<ApiResponse<ConsultationRequestResponse>> SubmitTicketAsync(
            int learnerId, SubmitTicketRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var (purchase, error) = await ValidatePurchaseAsync(learnerId, request.ConsultationPurchaseId, ConsultationType.WrittenAnswer);
                if (error is not null)
                    return ApiResponse<ConsultationRequestResponse>.Fail(error);

                if (string.IsNullOrWhiteSpace(request.Question))
                    return ApiResponse<ConsultationRequestResponse>.Fail("Câu hỏi không được để trống.");

                var consultationRequest = new ConsultationRequest
                {
                    ConsultationPurchaseId = purchase!.Id,
                    Status = ConsultationStatus.PendingAssignment,
                    Question = request.Question
                };

                await unitOfWork.ConsultationRequests.AddAsync(consultationRequest);
                await unitOfWork.SaveChangesAsync();

                return ApiResponse<ConsultationRequestResponse>.Success(
                    await MapToResponseAsync(consultationRequest, cancellationToken));
            }
            catch (Exception ex)
            {
                return ApiResponse<ConsultationRequestResponse>.Fail($"Không thể gửi ticket: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<ConsultationRequestResponse>>> GetMyRequestsAsync(
            int learnerId, CancellationToken cancellationToken)
        {
            try
            {
                var purchases = await unitOfWork.ConsultationPurchases.FindAsync(p => p.UserId == learnerId);
                var purchaseIds = purchases.Select(p => p.Id).ToHashSet();

                var requests = await unitOfWork.ConsultationRequests
                    .FindAsync(r => purchaseIds.Contains(r.ConsultationPurchaseId));

                var result = new List<ConsultationRequestResponse>();
                foreach (var r in requests.OrderByDescending(r => r.ScheduledAt ?? r.CompletedAt ?? DateTime.MinValue))
                    result.Add(await MapToResponseAsync(r, cancellationToken));

                return ApiResponse<List<ConsultationRequestResponse>>.Success(result);
            }
            catch (Exception ex)
            {
                return ApiResponse<List<ConsultationRequestResponse>>.Fail($"Không thể tải danh sách yêu cầu: {ex.Message}");
            }
        }

        public async Task<ApiResponse<ConsultantQueueResponse>> GetConsultantQueueAsync(
            int consultantId, CancellationToken cancellationToken)
        {
            try
            {
                // Hàng đợi = ticket chưa ai nhận (Learner mới gửi) + các request đã gán cho chính consultant này
                var unclaimedTickets = await unitOfWork.ConsultationRequests
                    .FindAsync(r => r.ConsultantId == null && r.Status == ConsultationStatus.PendingAssignment);

                var myAssigned = await unitOfWork.ConsultationRequests
                    .FindAsync(r => r.ConsultantId == consultantId &&
                                     (r.Status == ConsultationStatus.Assigned || r.Status == ConsultationStatus.Accepted));

                var allRequests = unclaimedTickets.Concat(myAssigned)
                    .OrderBy(r => r.ScheduledAt ?? r.CreatedDate ?? DateTime.MaxValue);

                var items = new List<ConsultantQueueItem>();
                foreach (var r in allRequests)
                {
                    var purchase = await unitOfWork.ConsultationPurchases.GetByIdAsync(r.ConsultationPurchaseId);
                    if (purchase is null) continue;

                    var learner = await unitOfWork.UserAccounts.GetByIdAsync(purchase.UserId);
                    var package = await unitOfWork.ConsultationPackages.GetByIdAsync(purchase.ConsultationPackageId);

                    var attempts = await unitOfWork.PracticeTestAttempts
                        .FindAsync(a => a.UserId == purchase.UserId && a.IsCompleted);

                    var recentResults = new List<MockTestSummary>();
                    foreach (var a in attempts.OrderByDescending(a => a.SubmittedAt).Take(5))
                    {
                        var test = await unitOfWork.PracticeTests.GetByIdAsync(a.PracticeTestId);
                        recentResults.Add(new MockTestSummary
                        {
                            TestTitle = test?.Title ?? "N/A",
                            Score = a.TotalScore,
                            SubmittedAt = a.SubmittedAt
                        });
                    }

                    var learnerName = $"{learner?.FirstName} {learner?.LastName}".Trim();

                    items.Add(new ConsultantQueueItem
                    {
                        RequestId = r.Id,
                        LearnerId = purchase.UserId,
                        LearnerName = string.IsNullOrWhiteSpace(learnerName) ? "N/A" : learnerName,
                        Type = package?.Type ?? ConsultationType.WrittenAnswer,
                        Status = r.Status,
                        ScheduledAt = r.ScheduledAt,
                        Question = r.Question,
                        RecentMockTestResults = recentResults
                    });
                }

                return ApiResponse<ConsultantQueueResponse>.Success(new ConsultantQueueResponse { Items = items });
            }
            catch (Exception ex)
            {
                return ApiResponse<ConsultantQueueResponse>.Fail($"Không thể tải hàng đợi tư vấn: {ex.Message}");
            }
        }

        public async Task<ApiResponse<ConsultationRequestResponse>> ClaimTicketAsync(
            int consultantId, int requestId, CancellationToken cancellationToken)
        {
            try
            {
                var request = await unitOfWork.ConsultationRequests.GetByIdAsync(requestId);
                if (request is null || request.ConsultantId is not null || request.Status != ConsultationStatus.PendingAssignment)
                    return ApiResponse<ConsultationRequestResponse>.Fail("Ticket không còn khả dụng để nhận.");

                request.ConsultantId = consultantId;
                request.Status = ConsultationStatus.Assigned;

                unitOfWork.ConsultationRequests.Update(request);
                await unitOfWork.SaveChangesAsync();

                return ApiResponse<ConsultationRequestResponse>.Success(
                    await MapToResponseAsync(request, cancellationToken));
            }
            catch (Exception ex)
            {
                return ApiResponse<ConsultationRequestResponse>.Fail($"Không thể nhận ticket: {ex.Message}");
            }
        }

        public async Task<ApiResponse<ConsultationRequestResponse>> SubmitAnswerAsync(
            int consultantId, int requestId, SubmitAnswerRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var consultationRequest = await unitOfWork.ConsultationRequests.GetByIdAsync(requestId);
                if (consultationRequest is null || consultationRequest.ConsultantId != consultantId)
                    return ApiResponse<ConsultationRequestResponse>.Fail("Không tìm thấy yêu cầu tư vấn.");

                if (string.IsNullOrWhiteSpace(request.AnswerText))
                    return ApiResponse<ConsultationRequestResponse>.Fail("Câu trả lời không được để trống.");

                await UpsertAnswerAsync(consultationRequest, consultantId, request.AnswerText, meetingNotes: null, cancellationToken);

                consultationRequest.Status = ConsultationStatus.Completed;
                consultationRequest.CompletedAt = DateTime.UtcNow;
                unitOfWork.ConsultationRequests.Update(consultationRequest);
                await unitOfWork.SaveChangesAsync();

                return ApiResponse<ConsultationRequestResponse>.Success(
                    await MapToResponseAsync(consultationRequest, cancellationToken));
            }
            catch (Exception ex)
            {
                return ApiResponse<ConsultationRequestResponse>.Fail($"Không thể trả lời ticket: {ex.Message}");
            }
        }

        public async Task<ApiResponse<ConsultationRequestResponse>> LogMeetingNoteAsync(
            int consultantId, int requestId, LogMeetingNoteRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var consultationRequest = await unitOfWork.ConsultationRequests.GetByIdAsync(requestId);
                if (consultationRequest is null || consultationRequest.ConsultantId != consultantId)
                    return ApiResponse<ConsultationRequestResponse>.Fail("Không tìm thấy buổi tư vấn.");

                await UpsertAnswerAsync(consultationRequest, consultantId, answerText: null, request.MeetingNotes, cancellationToken);

                consultationRequest.Status = ConsultationStatus.Completed;
                consultationRequest.CompletedAt = DateTime.UtcNow;
                unitOfWork.ConsultationRequests.Update(consultationRequest);
                await unitOfWork.SaveChangesAsync();

                return ApiResponse<ConsultationRequestResponse>.Success(
                    await MapToResponseAsync(consultationRequest, cancellationToken));
            }
            catch (Exception ex)
            {
                return ApiResponse<ConsultationRequestResponse>.Fail($"Không thể ghi chú: {ex.Message}");
            }
        }

        private async Task UpsertAnswerAsync(
            ConsultationRequest request, int consultantId, string? answerText, string? meetingNotes, CancellationToken cancellationToken)
        {
            var existing = (await unitOfWork.ConsultationAnswers
                .FindAsync(a => a.ConsultationRequestId == request.Id)).FirstOrDefault();

            if (existing is null)
            {
                await unitOfWork.ConsultationAnswers.AddAsync(new ConsultationAnswer
                {
                    ConsultationRequestId = request.Id,
                    ConsultantId = consultantId,
                    AnswerText = answerText,
                    MeetingNotes = meetingNotes,
                    AnsweredAt = DateTime.UtcNow
                });
            }
            else
            {
                if (answerText is not null) existing.AnswerText = answerText;
                if (meetingNotes is not null) existing.MeetingNotes = meetingNotes;
                existing.AnsweredAt = DateTime.UtcNow;
                unitOfWork.ConsultationAnswers.Update(existing);
            }
        }

        private async Task<(ConsultationPurchase? Purchase, string? Error)> ValidatePurchaseAsync(
            int learnerId, int purchaseId, ConsultationType requiredType)
        {
            var purchase = await unitOfWork.ConsultationPurchases.GetByIdAsync(purchaseId);
            if (purchase is null || purchase.UserId != learnerId)
                return (null, "Không tìm thấy gói tư vấn đã mua.");

            if (purchase.PaymentStatus != PaymentStatus.Paid)
                return (null, "Gói tư vấn chưa được thanh toán thành công.");

            var existingRequest = (await unitOfWork.ConsultationRequests
                .FindAsync(r => r.ConsultationPurchaseId == purchase.Id)).FirstOrDefault();
            if (existingRequest is not null)
                return (null, "Gói tư vấn này đã được sử dụng.");

            var package = await unitOfWork.ConsultationPackages.GetByIdAsync(purchase.ConsultationPackageId);
            if (package is null || package.Type != requiredType)
                return (null, $"Gói tư vấn không hỗ trợ hình thức {requiredType}.");

            return (purchase, null);
        }

        private async Task<ConsultationRequestResponse> MapToResponseAsync(ConsultationRequest r, CancellationToken cancellationToken)
        {
            var purchase = await unitOfWork.ConsultationPurchases.GetByIdAsync(r.ConsultationPurchaseId);
            var package = purchase is null ? null : await unitOfWork.ConsultationPackages.GetByIdAsync(purchase.ConsultationPackageId);
            var answer = (await unitOfWork.ConsultationAnswers
                .FindAsync(a => a.ConsultationRequestId == r.Id)).FirstOrDefault();

            return new ConsultationRequestResponse
            {
                Id = r.Id,
                Type = package?.Type ?? ConsultationType.WrittenAnswer,
                Status = r.Status,
                ConsultantId = r.ConsultantId,
                ScheduledAt = r.ScheduledAt,
                MeetingLink = r.MeetingLink,
                Question = r.Question,
                AnswerText = answer?.AnswerText,
                MeetingNotes = answer?.MeetingNotes,
                CompletedAt = r.CompletedAt
            };
        }
    }
}