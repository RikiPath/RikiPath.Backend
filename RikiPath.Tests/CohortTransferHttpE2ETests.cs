using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RikiPath.Application.IClients;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;
using RikiPath.Infrastructure;
using static RikiPath.Tests.CanonicalGroupLearningHttpE2ETests;

namespace RikiPath.Tests;

// Real Kestrel host + JWT + SignalR over an isolated PostgreSQL schema (RIKIPATH_COURSE_TEST_DB).
public sealed class CohortTransferHttpE2ETests
{
    static CohortTransferHttpE2ETests() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    private static DateTime Utc(DateTime v) => new(v.Year, v.Month, v.Day, v.Hour, v.Minute, 0, DateTimeKind.Utc);

    [CoursePostgresFact]
    public async Task Transfer_and_mentor_replacement_enforce_access_control_and_meeting_authorization_over_http_and_signalr()
    {
        var (schema, cs) = await CreateIsolatedSchemaAsync(GetBaseConnectionString()!);
        var payments = new Mock<IPaymentGatewayClient>(MockBehavior.Strict);
        Microsoft.AspNetCore.Builder.WebApplication? app = null;
        try
        {
            var host = await StartE2EHostAsync(cs, payments);
            app = host.App;
            int admin, mentorA, mentorB; int[] learners;
            int sourceCohort, targetCohort, replaceCohort, l1Course, l2Course, l4Course;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                UserAccount U(string email, Role role, bool mentor = false) => new()
                {
                    Email = email, FirstName = email.Split('@')[0], LastName = "E2E", Role = role, PasswordHash = [], PasswordSalt = [],
                    MentorProfile = mentor ? new MentorProfile { VerificationStatus = MentorVerificationStatus.Approved, JlptLevel = "N3" } : null
                };
                var a = U("admin@t.e2e", Role.Admin); var ma = U("mentora@t.e2e", Role.Mentor, true); var mb = U("mentorb@t.e2e", Role.Mentor, true);
                var ls = Enumerable.Range(1, 5).Select(i => U($"learner{i}@t.e2e", Role.Learner)).ToArray();
                var level = new CertificateLevel { Code = "N3", CertificateType = new CertificateType { Name = "JLPT", IsActive = true } };
                var plan = new StudyCoursePlan { Name = "N3 Group", CertificateLevel = level, SessionType = "Group", Capacity = 4, SessionMinutes = 90, TotalSessions = 3, DurationMonths = 2, SessionsPerWeek = 3, Price = 500_000, IsPublished = true };
                var policy = new MentorGroupPolicy
                {
                    Version = 1, EffectiveFromUtc = DateTime.UtcNow.AddDays(-30), MaxLearners = 4, EnrollmentCutoffMinutesBeforeFirstSession = 60, SessionMinutes = 90,
                    RestMinutes = 15, DailySessionCap = 4, PublicationHorizonDays = 30, CancellationLeadHours = 24, ReschedulingLeadHours = 24, HomeworkDeadlineDays = 7,
                    PlacementAttemptLimit = 2, RemediationCompletionThresholdPercent = 90, RemediationThresholdComparison = ">", KpiReportingPeriodDays = 30,
                    RecurringWeekdayPatternsJson = "[[1,3,5]]", CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
                };
                db.AddRange(a, ma, mb, level, plan, policy); db.AddRange(ls);
                await db.SaveChangesAsync();
                foreach (var m in new[] { ma, mb })
                    db.MentorTeachingQualifications.Add(new MentorTeachingQualification { MentorUserId = m.Id, CertificateLevelId = level.Id, IsActive = true, VerifiedByAdminId = a.Id, VerifiedAtUtc = DateTime.UtcNow.AddDays(-5) });
                StudyCohort Cohort(UserAccount mentor, double firstDay, int capacity, string name)
                {
                    var c = new StudyCohort { Plan = plan, Name = name, Mentor = mentor, Capacity = capacity, CapacitySnapshot = capacity, Status = "Published" };
                    for (var i = 0; i < 3; i++)
                    {
                        var start = Utc(DateTime.UtcNow.AddDays(firstDay + i));
                        c.Sessions.Add(new StudyCohortSession { Number = i + 1, Availability = new MentorAvailability { Mentor = mentor, StartTime = start, EndTime = start.AddMinutes(90), MaxLearners = capacity, IsApproved = true, RoomId = Guid.NewGuid() } });
                    }
                    c.StartsAt = c.Sessions.Min(s => s.Availability.StartTime); c.EndsAt = c.Sessions.Max(s => s.Availability.EndTime);
                    return c;
                }
                StudyCourse Enroll(UserAccount learner, StudyCohort c) => new()
                {
                    Cohort = c, Mentor = c.Mentor, StartsAt = c.StartsAt, EndsAt = c.EndsAt, SessionType = "Group", Learner = learner, PurchaseKey = Guid.NewGuid(),
                    Plan = plan, Name = plan.Name, CertificateLevel = level, PlanRevision = 1, DurationMonths = 2, SessionsPerWeek = 3, TotalSessions = 3, SessionMinutes = 90,
                    Amount = 500_000, PaymentStatus = "Paid", Status = "Active",
                    Sessions = c.Sessions.Select(s => new StudyCourseSession { Number = s.Number, ScheduledAt = s.Availability.StartTime, Availability = s.Availability, MeetingUrl = $"/meeting-room/{s.Availability.RoomId}" }).ToList()
                };
                var src = Cohort(ma, 3, 4, "Source"); var tgt = Cohort(mb, 10, 2, "Target"); var rep = Cohort(ma, 20, 4, "Replace");
                var e1 = Enroll(ls[0], src); var e2 = Enroll(ls[1], src); var e3 = Enroll(ls[2], tgt); var e4 = Enroll(ls[3], rep);
                db.AddRange(src, tgt, rep, e1, e2, e3, e4);
                await db.SaveChangesAsync();
                admin = a.Id; mentorA = ma.Id; mentorB = mb.Id; learners = ls.Select(x => x.Id).ToArray();
                sourceCohort = src.Id; targetCohort = tgt.Id; replaceCohort = rep.Id; l1Course = e1.Id; l2Course = e2.Id; l4Course = e4.Id;
            }
            var adminJwt = IssueJwt(admin, Role.Admin, "admin@t.e2e");
            var mentorAJwt = IssueJwt(mentorA, Role.Mentor, "mentora@t.e2e");
            var mentorBJwt = IssueJwt(mentorB, Role.Mentor, "mentorb@t.e2e");
            var lj = learners.Select((id, i) => IssueJwt(id, Role.Learner, $"learner{i + 1}@t.e2e")).ToArray();
            using var http = new HttpClient { BaseAddress = new Uri(host.BaseUrl) };

            HttpRequestMessage TransferForm(string? jwt, int courseId, Guid key, string reason, int? target)
            {
                var req = new HttpRequestMessage(HttpMethod.Post, $"/api/study-courses/{courseId}/transfer-requests");
                if (jwt is not null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
                var form = new MultipartFormDataContent { { new StringContent(key.ToString()), "idempotencyKey" }, { new StringContent(reason), "reason" } };
                if (target.HasValue) form.Add(new StringContent(target.Value.ToString()), "targetCohortId");
                form.Add(new ByteArrayContent("%PDF-1.4 e2e"u8.ToArray()) { Headers = { ContentType = new MediaTypeHeaderValue("application/pdf") } }, "evidence", "proof.pdf");
                req.Content = form; return req;
            }
            const string Reason = "Đổi ca làm việc nên cần lớp khác";

            // Access control: 401 anonymous, 403 other learner / mentor role, 422 missing reason.
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.SendAsync(TransferForm(null, l1Course, Guid.NewGuid(), Reason, targetCohort))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(TransferForm(lj[1], l1Course, Guid.NewGuid(), Reason, targetCohort))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(TransferForm(mentorAJwt, l1Course, Guid.NewGuid(), Reason, targetCohort))).StatusCode);
            var noReason = await http.SendAsync(TransferForm(lj[0], l1Course, Guid.NewGuid(), "ngắn", targetCohort));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, noReason.StatusCode);
            Assert.Contains("TRANSFER_REASON_REQUIRED", await noReason.Content.ReadAsStringAsync());

            var key = Guid.NewGuid();
            var created = await ReadResultAsync(await http.SendAsync(TransferForm(lj[0], l1Course, key, Reason, targetCohort)));
            var requestId = created.GetProperty("id").GetInt32();
            Assert.Equal("Pending", created.GetProperty("status").GetString());
            Assert.True(created.GetProperty("hasEvidence").GetBoolean());
            Assert.Equal(requestId, (await ReadResultAsync(await http.SendAsync(TransferForm(lj[0], l1Course, key, Reason, targetCohort)))).GetProperty("id").GetInt32());
            Assert.Equal(HttpStatusCode.NotFound, (await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/study-courses/transfer-requests/{requestId}", lj[3]))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/study-courses/transfer-requests/{requestId}/evidence", lj[3]))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/study-courses/transfer-requests/{requestId}/evidence", adminJwt))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(AuthReq(HttpMethod.Get, "/api/study-courses/transfer-requests", mentorAJwt))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/transfer-requests/{requestId}/approve", lj[0], new { }))).StatusCode);

            var detail = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/study-courses/transfer-requests/{requestId}", adminJwt)));
            Assert.True(detail.GetProperty("eligibility").GetProperty("eligible").GetBoolean(), detail.ToString());
            Assert.Contains(detail.GetProperty("candidateCohorts").EnumerateArray(), c => c.GetProperty("id").GetInt32() == targetCohort);
            var approved = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/transfer-requests/{requestId}/approve", adminJwt, new { note = "OK" })));
            var destinationCourse = approved.GetProperty("destinationCourseId").GetInt32();
            var retry = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/transfer-requests/{requestId}/approve", adminJwt, new { })));
            Assert.True(retry.GetProperty("idempotent").GetBoolean());

            // Target now full (L3 + L1): a second learner's approval is refused with 409 and stays Pending; rejection needs a reason.
            var r2 = (await ReadResultAsync(await http.SendAsync(TransferForm(lj[1], l2Course, Guid.NewGuid(), Reason, targetCohort)))).GetProperty("id").GetInt32();
            var full = await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/transfer-requests/{r2}/approve", adminJwt, new { }));
            Assert.Equal(HttpStatusCode.Conflict, full.StatusCode);
            Assert.Contains("TRANSFER_TARGET_FULL", await full.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/transfer-requests/{r2}/reject", adminJwt, new { reason = "" }))).StatusCode);
            await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/transfer-requests/{r2}/reject", adminJwt, new { reason = "Lớp đích đã đủ chỗ" })));
            var mine = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, "/api/study-courses/transfer-requests", lj[1])));
            Assert.Equal("Lớp đích đã đủ chỗ", mine.EnumerateArray().Single().GetProperty("decisionNote").GetString());

            // Meeting authorization follows the destination: open session 1 rooms by moving them to "now".
            Guid sourceRoom, targetRoom;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = DateTime.UtcNow.AddMinutes(-5);
                var slots = await db.Set<StudyCohortSession>().Where(s => (s.CohortId == sourceCohort || s.CohortId == targetCohort) && s.Number == 1).Select(s => s.Availability).ToListAsync();
                foreach (var slot in slots) { slot.StartTime = now; slot.EndTime = now.AddMinutes(90); }
                await db.SaveChangesAsync();
                sourceRoom = slots.Single(s => s.MentorId == mentorA).RoomId; targetRoom = slots.Single(s => s.MentorId == mentorB).RoomId;
            }
            using (var l1Ws = await ConnectSignalRRoomAsync(host.BaseUrl, lj[0]))
            using (var l2Ws = await ConnectSignalRRoomAsync(host.BaseUrl, lj[1]))
            {
                var inTarget = await SendSignalRHubInvokeAsync(l1Ws, "t1", "JoinRoom", targetRoom, "HV1", "Learner");
                Assert.False(inTarget.TryGetProperty("error", out _), inTarget.ToString());
                var inSource = await SendSignalRHubInvokeAsync(l1Ws, "t2", "JoinRoom", sourceRoom, "HV1", "Learner");
                Assert.True(inSource.TryGetProperty("error", out _), "Transferred learner must not join the source cohort room.");
                var stayer = await SendSignalRHubInvokeAsync(l2Ws, "t3", "JoinRoom", sourceRoom, "HV2", "Learner");
                Assert.False(stayer.TryGetProperty("error", out _), stayer.ToString());
            }
            var destDetail = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/study-courses/{destinationCourse}", lj[0])));
            Assert.Equal(targetCohort, destDetail.GetProperty("cohortId").GetInt32());
            var srcDetail = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/study-courses/{l1Course}", lj[0])));
            Assert.Equal("Transferred", srcDetail.GetProperty("status").GetString());

            // Mentor replacement: preview, confirm, notifications queued, meeting access switches to the new mentor.
            Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/study-courses/cohorts/{replaceCohort}/mentor-replacement/preview?mentorId={mentorB}", mentorAJwt))).StatusCode);
            var preview = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/study-courses/cohorts/{replaceCohort}/mentor-replacement/preview?mentorId={mentorB}", adminJwt)));
            Assert.True(preview.GetProperty("eligible").GetBoolean(), preview.ToString());
            Assert.Equal(3, preview.GetProperty("impact").GetProperty("affectedSessionCount").GetInt32());
            var replaced = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Post, $"/api/study-courses/cohorts/{replaceCohort}/mentor-replacement", adminJwt,
                new { mentorId = mentorB, reason = "Cố vấn A nghỉ phép dài hạn", idempotencyKey = Guid.NewGuid(), expectedCohortRevision = preview.GetProperty("revision").GetInt32() })));
            Assert.Equal(mentorA, replaced.GetProperty("previousMentorId").GetInt32());
            Assert.Equal(HttpStatusCode.OK, (await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/study-courses/cohorts/{replaceCohort}/mentor-assignments", mentorAJwt))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/study-courses/cohorts/{replaceCohort}/mentor-assignments", lj[3]))).StatusCode);
            Guid replacedRoom;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var slot = await db.Set<StudyCohortSession>().Where(s => s.CohortId == replaceCohort && s.Number == 1).Select(s => s.Availability).SingleAsync();
                Assert.Equal(mentorB, slot.MentorId);
                var now = DateTime.UtcNow.AddMinutes(-5); slot.StartTime = now; slot.EndTime = now.AddMinutes(90);
                await db.SaveChangesAsync();
                replacedRoom = slot.RoomId;
                Assert.Equal(0, await db.MentorBookings.CountAsync());
                Assert.True(await db.MentorOutboxEvents.AnyAsync(e => e.EventKey.StartsWith("cohort-mentor-replacement:")));
            }
            var l4Detail = await ReadResultAsync(await http.SendAsync(AuthReq(HttpMethod.Get, $"/api/study-courses/{l4Course}", lj[3])));
            Assert.Equal($"/meeting-room/{replacedRoom}", l4Detail.GetProperty("sessions").EnumerateArray().First().GetProperty("meetingUrl").GetString());
            using (var aWs = await ConnectSignalRRoomAsync(host.BaseUrl, mentorAJwt))
            using (var bWs = await ConnectSignalRRoomAsync(host.BaseUrl, mentorBJwt))
            using (var l4Ws = await ConnectSignalRRoomAsync(host.BaseUrl, lj[3]))
            {
                Assert.True((await SendSignalRHubInvokeAsync(aWs, "r1", "JoinRoom", replacedRoom, "A", "Mentor")).TryGetProperty("error", out _));
                var b = await SendSignalRHubInvokeAsync(bWs, "r2", "JoinRoom", replacedRoom, "B", "Mentor");
                Assert.False(b.TryGetProperty("error", out _), b.ToString());
                var l4 = await SendSignalRHubInvokeAsync(l4Ws, "r3", "JoinRoom", replacedRoom, "HV4", "Learner");
                Assert.False(l4.TryGetProperty("error", out _), l4.ToString());
            }
            payments.VerifyNoOtherCalls();
        }
        finally
        {
            if (app is not null) await app.DisposeAsync();
            await DropSchemaAsync(schema, cs);
        }
    }
}
