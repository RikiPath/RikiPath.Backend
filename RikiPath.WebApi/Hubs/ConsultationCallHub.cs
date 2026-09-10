using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Spreadsheet;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RikiPath.Application;
using System.Security.Claims;

namespace RikiPath.WebApi.Hubs
{
    [Authorize]
    public class ConsultationCallHub(IUnitOfWork unitOfWork, ICallConnectionTracker tracker) : Hub
    {
        private const int MaxParticipantsPerCall = 2;

        private int CurrentUserId
        {
            get
            {
                var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)
                            ?? Context.User?.FindFirst("sub")
                            ?? Context.User?.FindFirst("userId")
                            ?? Context.User?.FindFirst("id");

                if (claim is null || !int.TryParse(claim.Value, out var userId))
                    throw new HubException("Không xác định được userId từ JWT token.");

                return userId;
            }
        }

        private static string GroupName(int consultationRequestId) => $"consult-call-{consultationRequestId}";

        public async Task JoinCall(int consultationRequestId)
        {
            var userId = CurrentUserId;
            var (allowed, error) = await CanJoinAsync(consultationRequestId, userId);

            if (!allowed)
            {
                await Clients.Caller.SendAsync("CallError", error);
                return;
            }

            if (tracker.GetParticipantCount(consultationRequestId) >= MaxParticipantsPerCall)
            {
                await Clients.Caller.SendAsync("CallError", "Phòng tư vấn đã có đủ 2 người tham gia.");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(consultationRequestId));
            tracker.Track(Context.ConnectionId, consultationRequestId, userId);

            await Clients.OthersInGroup(GroupName(consultationRequestId)).SendAsync("PeerJoined", userId);
        }

        public async Task LeaveCall(int consultationRequestId)
        {
            var userId = CurrentUserId;

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(consultationRequestId));
            tracker.Untrack(Context.ConnectionId);

            await Clients.OthersInGroup(GroupName(consultationRequestId)).SendAsync("PeerLeft", userId);
        }

        public async Task SendOffer(int consultationRequestId, string sdpOfferJson)
            => await Clients.OthersInGroup(GroupName(consultationRequestId))
                .SendAsync("ReceiveOffer", CurrentUserId, sdpOfferJson);

        public async Task SendAnswer(int consultationRequestId, string sdpAnswerJson)
            => await Clients.OthersInGroup(GroupName(consultationRequestId))
                .SendAsync("ReceiveAnswer", CurrentUserId, sdpAnswerJson);

        public async Task SendIceCandidate(int consultationRequestId, string candidateJson)
            => await Clients.OthersInGroup(GroupName(consultationRequestId))
                .SendAsync("ReceiveIceCandidate", CurrentUserId, candidateJson);

        /// <summary>Trả về cấu hình STUN/TURN cho FE khởi tạo RTCPeerConnection.</summary>
        public IceServerConfig[] GetIceServers()
        {
            return
            [
                new IceServerConfig { Urls = ["stun:stun.l.google.com:19302"] }
                // TODO production: thêm TURN server (STUN không đủ khi 1 trong 2 phía ở sau NAT
                // đối xứng / firewall công ty chặn UDP trực tiếp - lúc đó bắt buộc phải relay qua
                // TURN mới kết nối được). Có thể lấy free-tier từ Twilio/Xirsys để test, hoặc tự
                // host coturn. Khi có, thêm: new IceServerConfig { Urls = ["turn:..."], Username = "...", Credential = "..." }
            ];
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var info = tracker.Untrack(Context.ConnectionId);
            if (info is not null)
            {
                await Clients.OthersInGroup(GroupName(info.Value.ConsultationRequestId))
                    .SendAsync("PeerLeft", info.Value.UserId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        private async Task<(bool Allowed, string? Error)> CanJoinAsync(int consultationRequestId, int userId)
        {
            var request = await unitOfWork.ConsultationRequests.GetByIdAsync(consultationRequestId);
            if (request is null)
                return (false, "Không tìm thấy buổi tư vấn.");

            if (request.Status is not (ConsultationStatus.Assigned or ConsultationStatus.Accepted))
                return (false, "Buổi tư vấn chưa sẵn sàng hoặc đã kết thúc.");

            if (request.ConsultantId == userId)
                return (true, null);

            var purchase = await unitOfWork.ConsultationPurchases.GetByIdAsync(request.ConsultationPurchaseId);
            if (purchase is not null && purchase.UserId == userId)
                return (true, null);

            return (false, "Bạn không có quyền tham gia buổi tư vấn này.");
        }
    }
}