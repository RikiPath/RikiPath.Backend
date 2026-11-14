namespace RikiPath.Application.IServices;

public interface IMentorCalendarService
{
    bool IsConfigured { get; }
    Task<object> StatusAsync(int mentorId, CancellationToken ct);
    Task<string> ConnectAsync(int mentorId, CancellationToken ct);
    Task CompleteAsync(int mentorId, string state, string code, CancellationToken ct);
    Task DisconnectAsync(int mentorId, CancellationToken ct);
    Task SyncAsync(int mentorId, CancellationToken ct);
}
