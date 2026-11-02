using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Options;
using RikiPath.Application.Options;
using RikiPath.Application.Services;
using RikiPath.Application.Requests.MentorMeetings;
using RikiPath.Application.Responses.MentorMeetings;
using RikiPath.Domain.Entities;
using RikiPath.Domain.MentorScheduling;
using RikiPath.Infrastructure;

namespace RikiPath.Tests;

public sealed class MentorPersistenceModelTests
{
    private static AppDbContext ModelContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=invalid;Database=metadata_only")
        .Options);

    private static IEntityType Entity<T>(AppDbContext db) =>
        db.Model.FindEntityType(typeof(T)) ?? throw new InvalidOperationException(typeof(T).Name);

    [Fact]
    public void Existing_availability_is_the_only_bookable_slot_table()
    {
        using var db = ModelContext();
        Assert.Equal("MentorBookings", Entity<MentorBooking>(db).GetTableName());
        Assert.Equal("MentorAvailabilities", Entity<MentorAvailability>(db).GetTableName());
        Assert.Equal("MentorBookingTerms", Entity<MentorBookingTerms>(db).GetTableName());
        Assert.DoesNotContain(db.Model.GetEntityTypes(), entity => entity.GetTableName() == "MentorSlots");
        Assert.Equal(7, new[] { typeof(MentorSchedule), typeof(MentorScheduleRule),
            typeof(MentorBookingIntent), typeof(MentorBookingTerms), typeof(MentorBookingReservation),
            typeof(MentorBookingDayGuard), typeof(MentorOutboxEvent) }
            .Count(t => db.Model.FindEntityType(t) is not null));
    }

    [Fact]
    public void Availability_and_booking_timestamps_keep_their_intended_store_types()
    {
        using var db = ModelContext();
        var slot = Entity<MentorAvailability>(db);
        Assert.True(slot.FindProperty(nameof(MentorAvailability.GenerationKey))!.IsNullable);
        Assert.True(slot.FindProperty(nameof(MentorAvailability.MentorScheduleRuleId))!.IsNullable);
        Assert.True(slot.FindProperty(nameof(MentorAvailability.PolicyVersion))!.IsNullable);
        Assert.Equal("date", slot.FindProperty(nameof(MentorAvailability.LocalBusinessDate))!.GetColumnType());
        Assert.Equal("timestamp without time zone", slot.FindProperty(nameof(MentorAvailability.StartTime))!.GetColumnType());
        Assert.Equal("timestamp without time zone", slot.FindProperty(nameof(MentorAvailability.EndTime))!.GetColumnType());
        Assert.Equal("time without time zone", Entity<MentorScheduleRule>(db).FindProperty(nameof(MentorScheduleRule.LocalStart))!.GetColumnType());
        Assert.Equal("jsonb", Entity<MentorOutboxEvent>(db).FindProperty(nameof(MentorOutboxEvent.PayloadJson))!.GetColumnType());
        Assert.Equal("timestamp with time zone", Entity<MentorBookingReservation>(db).FindProperty(nameof(MentorBookingReservation.RestProtectedUntilUtc))!.GetColumnType());
    }

    [Fact]
    public void Generated_slot_metadata_fits_the_existing_availability_identity()
    {
        using var db = ModelContext();
        var availability = Entity<MentorAvailability>(db);
        var generator = new MentorSlotGenerator(Options.Create(new MentorSlotGenerationOptions()));
        var generated = generator.GenerateDailySlots(42, new DateOnly(2026, 6, 1),
            [MentorDaypart.Morning], new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.NotEmpty(generated);
        Assert.All(generated, slot =>
        {
            Assert.Equal(TimeSpan.FromMinutes(MentorSlotPolicy.AppointmentDurationMinutes), slot.EndUtc - slot.StartUtc);
            Assert.True(slot.GenerationKey.Length <= availability.FindProperty(nameof(MentorAvailability.GenerationKey))!.GetMaxLength());
            Assert.Equal(new DateOnly(2026, 6, 1), slot.LocalDate);
            Assert.True(slot.PolicyVersion > 0);
        });
        Assert.Equal(1, MentorAvailability.DefaultMaxLearners);
    }

    [Fact]
    public void Historical_group_availability_seed_keeps_its_capacity()
    {
        using var db = ModelContext();
        var designModel = db.GetService<IDesignTimeModel>().Model;
        var seed = Assert.Single(designModel.FindEntityType(typeof(MentorAvailability))!.GetSeedData(), row =>
            row.TryGetValue("Id", out var id) && id is int { } value && value == 990001);
        Assert.Equal(4, seed["MaxLearners"]);
        Assert.Equal(new Guid("a1000000-0000-0000-0000-000000990001"), seed["RoomId"]);
    }

    [Fact]
    public void Public_booking_contract_keeps_the_existing_availability_identity()
    {
        Assert.Equal(typeof(int), typeof(BookIncludedMeetingRequest)
            .GetProperty(nameof(BookIncludedMeetingRequest.MentorAvailabilityId))!.PropertyType);
        Assert.Equal(typeof(int?), typeof(MentorBookingResponse)
            .GetProperty(nameof(MentorBookingResponse.MentorAvailabilityId))!.PropertyType);
        Assert.Equal(typeof(int), typeof(MentorBookingTerms)
            .GetProperty(nameof(MentorBookingTerms.MentorAvailabilityId))!.PropertyType);
        Assert.Equal(typeof(int), typeof(MentorBookingReservation)
            .GetProperty(nameof(MentorBookingReservation.MentorAvailabilityId))!.PropertyType);
    }

    [Fact]
    public void Scheduling_identity_and_generation_key_are_unique()
    {
        using var db = ModelContext();
        AssertUnique<MentorSchedule>(db, nameof(MentorSchedule.MentorId), nameof(MentorSchedule.PolicyVersion));
        AssertUnique<MentorScheduleRule>(db, nameof(MentorScheduleRule.ScheduleId), nameof(MentorScheduleRule.Weekday), nameof(MentorScheduleRule.DaypartCode), nameof(MentorScheduleRule.EffectiveFrom));
        AssertUnique<MentorAvailability>(db, nameof(MentorAvailability.GenerationKey));
        var generationIndex = Assert.Single(Entity<MentorAvailability>(db).GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(MentorAvailability.GenerationKey)]));
        Assert.Equal("\"GenerationKey\" IS NOT NULL", generationIndex.GetFilter());
        Assert.Equal(2, Entity<MentorBookingDayGuard>(db).FindPrimaryKey()!.Properties.Count);
    }

    [Fact]
    public void Booking_intent_and_reservation_have_explicit_uniqueness()
    {
        using var db = ModelContext();
        AssertUnique<MentorBookingIntent>(db, nameof(MentorBookingIntent.LearnerId), nameof(MentorBookingIntent.IntentKey));
        AssertUnique<MentorBookingReservation>(db, nameof(MentorBookingReservation.MentorBookingId));
    }

    [Fact]
    public void Booking_history_does_not_permanently_claim_a_cancelled_slot()
    {
        using var db = ModelContext();
        var historicalBookingIndex = Assert.Single(Entity<MentorBookingTerms>(db).GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(MentorBookingTerms.MentorAvailabilityId)]));
        Assert.False(historicalBookingIndex.IsUnique);
    }

    [Fact]
    public void Only_one_active_reservation_can_claim_a_slot()
    {
        using var db = ModelContext();
        var index = Assert.Single(Entity<MentorBookingReservation>(db).GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(MentorBookingReservation.MentorAvailabilityId)]));
        Assert.True(index.IsUnique);
        Assert.Equal("\"ReleasedAtUtc\" IS NULL", index.GetFilter());
        Assert.True(Entity<MentorBookingReservation>(db).FindProperty(nameof(MentorBookingReservation.ReleasedAtUtc))!.IsNullable);
    }

    [Fact]
    public void Booking_and_reservation_foreign_keys_restrict_deletion()
    {
        using var db = ModelContext();
        var extension = Entity<MentorBookingTerms>(db);
        Assert.Contains(extension.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(MentorBooking) && f.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(extension.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(MentorAvailability) && f.DeleteBehavior == DeleteBehavior.Restrict);
        var reservation = Entity<MentorBookingReservation>(db);
        Assert.Contains(reservation.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(MentorBooking) && f.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(reservation.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(UserAccount) && f.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(reservation.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(MentorAvailability) && f.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(Entity<MentorBookingIntent>(db).GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(MentorBooking) && f.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(Entity<MentorScheduleRule>(db).GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(MentorSchedule) && f.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(Entity<MentorAvailability>(db).GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(MentorScheduleRule) && f.DeleteBehavior == DeleteBehavior.Restrict);
    }

    [Fact]
    public void Reservation_mentor_must_match_the_availability_owner()
    {
        using var db = ModelContext();
        var reservation = Entity<MentorBookingReservation>(db);
        var ownerKey = Assert.Single(Entity<MentorAvailability>(db).GetKeys(), key =>
            key.Properties.Select(property => property.Name).SequenceEqual([
                nameof(MentorAvailability.Id), nameof(MentorAvailability.MentorId)]));
        Assert.Contains(reservation.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalKey == ownerKey &&
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([
                nameof(MentorBookingReservation.MentorAvailabilityId), nameof(MentorBookingReservation.MentorId)]));
    }

    [Fact]
    public void Interval_and_count_checks_are_present_in_metadata()
    {
        using var db = ModelContext();
        var designModel = db.GetService<IDesignTimeModel>().Model;
        Assert.Contains(designModel.FindEntityType(typeof(MentorAvailability))!.GetCheckConstraints(), c =>
            c.Name == "CK_MentorAvailability_GeneratedSlot" && c.Sql.Contains("\"PolicyVersion\" IS NOT NULL")
            && c.Sql.Contains("GenerationKey") && c.Sql.Contains("MentorScheduleRuleId")
            && c.Sql.Contains("MaxLearners") && c.Sql.Contains("90 minutes"));
        Assert.Contains(designModel.FindEntityType(typeof(MentorBookingReservation))!.GetCheckConstraints(), c => c.Sql.Contains("EndUtc") && c.Sql.Contains("StartUtc"));
        Assert.Contains(designModel.FindEntityType(typeof(MentorBookingReservation))!.GetCheckConstraints(), c =>
            c.Name == "CK_MentorBookingReservation_Rest" && c.Sql.Contains("RestProtectedUntilUtc") && c.Sql.Contains("15 minutes"));
        Assert.Contains(designModel.FindEntityType(typeof(MentorBookingTerms))!.GetCheckConstraints(), c => c.Sql.Contains("ScheduledEndUtc"));
        Assert.Contains(designModel.FindEntityType(typeof(MentorBookingDayGuard))!.GetCheckConstraints(), c => c.Sql.Contains("ActiveBookingCount"));
        Assert.Contains(designModel.FindEntityType(typeof(MentorScheduleRule))!.GetCheckConstraints(), c =>
            c.Sql.Contains("DaypartCode") && c.Sql.Contains("Noon"));
        Assert.Contains(designModel.FindEntityType(typeof(MentorBookingTerms))!.GetCheckConstraints(), c =>
            c.Sql.Contains("PolicyVersion") && c.Sql.Contains("> 0"));
        Assert.Contains(designModel.FindEntityType(typeof(MentorBookingReservation))!.GetCheckConstraints(), c =>
            c.Sql.Contains("PolicyVersion") && c.Sql.Contains("> 0"));
        Assert.Contains(designModel.FindEntityType(typeof(MentorOutboxEvent))!.GetCheckConstraints(), c =>
            c.Sql.Contains("EventVersion") && c.Sql.Contains("> 0"));
    }

    [Fact]
    public void Outbox_has_unique_event_key_and_delivery_index()
    {
        using var db = ModelContext();
        AssertUnique<MentorOutboxEvent>(db, nameof(MentorOutboxEvent.EventKey));
        Assert.Contains(Entity<MentorOutboxEvent>(db).GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual(new[] {
            nameof(MentorOutboxEvent.DeliveredAtUtc), nameof(MentorOutboxEvent.AvailableAtUtc), nameof(MentorOutboxEvent.CreatedAtUtc) }));
    }

    [Fact]
    public void Legacy_booking_and_availability_contracts_remain_mapped()
    {
        using var db = ModelContext();
        var booking = Entity<MentorBooking>(db);
        Assert.NotNull(booking.FindProperty(nameof(MentorBooking.Status)));
        Assert.NotNull(booking.FindProperty(nameof(MentorBooking.UserSubscriptionId)));
        Assert.NotNull(booking.FindProperty(nameof(MentorBooking.MentorAvailabilityId)));
        var availability = Entity<MentorAvailability>(db);
        Assert.NotNull(availability.FindProperty(nameof(MentorAvailability.MaxLearners)));
        Assert.NotNull(availability.FindProperty(nameof(MentorAvailability.RoomId)));
    }

    [Fact]
    public void AddMentorSchedulingPersistence_migration_contains_zero_UpdateData_statements()
    {
        using var db = ModelContext();
        var assembly = db.GetService<IMigrationsAssembly>();
        const string migrationId = "20261030125610_AddMentorSchedulingPersistence";
        Assert.True(assembly.Migrations.ContainsKey(migrationId), $"Migration {migrationId} not found in assembly");

        var migration = assembly.CreateMigration(assembly.Migrations[migrationId], "Npgsql.EntityFrameworkCore.PostgreSQL");
        Assert.NotNull(migration);

        var upUpdates = migration.UpOperations.OfType<UpdateDataOperation>().ToList();
        var downUpdates = migration.DownOperations.OfType<UpdateDataOperation>().ToList();

        Assert.Empty(upUpdates);
        Assert.Empty(downUpdates);

        // Verify that Up only contains the 7 tables and agreed DDL
        var createdTables = migration.UpOperations.OfType<CreateTableOperation>().Select(t => t.Name).ToHashSet();
        var expectedTables = new HashSet<string>
        {
            "MentorBookingDayGuards", "MentorBookingIntents", "MentorBookingReservations",
            "MentorBookingTerms", "MentorOutboxEvents", "MentorSchedules", "MentorScheduleRules"
        };
        Assert.Equal(expectedTables, createdTables);
    }

    [Fact]
    public void Mentor_scheduling_persistence_tables_match_model_snapshot()
    {
        using var db = ModelContext();
        var assembly = db.GetService<IMigrationsAssembly>();
        var snapshot = assembly.ModelSnapshot;
        Assert.NotNull(snapshot?.Model);

        var mentorEntityTypes = new[]
        {
            typeof(MentorAvailability), typeof(MentorBooking), typeof(MentorBookingDayGuard),
            typeof(MentorBookingIntent), typeof(MentorBookingReservation), typeof(MentorBookingTerms),
            typeof(MentorChatMessage), typeof(MentorOutboxEvent), typeof(MentorSchedule),
            typeof(MentorScheduleRule), typeof(MentorWeeklyAvailability), typeof(MentorCreditGrant),
            typeof(MentorCreditEntry), typeof(MentorAttendanceRecord),
            typeof(MentorReview), typeof(MentorDispute),
            typeof(MentorDisputeHistory), typeof(MentorAbuseReport)
        };

        foreach (var type in mentorEntityTypes)
        {
            var runtimeEntity = db.Model.FindEntityType(type);
            Assert.NotNull(runtimeEntity);

            var snapshotEntity = snapshot!.Model!.FindEntityType(type.FullName!);
            Assert.NotNull(snapshotEntity);

            var runtimeProps = runtimeEntity.GetProperties().Select(p => p.Name).ToHashSet();
            var snapshotProps = snapshotEntity.GetProperties().Select(p => p.Name).ToHashSet();
            Assert.Equal(runtimeProps, snapshotProps);
        }
    }


    [Fact]
    public void Mentor_credit_ledger_migration_has_no_update_data_and_creates_agreed_schema()
    {
        using var db = ModelContext();
        var assembly = db.GetService<IMigrationsAssembly>();
        const string migrationId = "20261031082840_AddMentorCreditLedgerPersistence";
        Assert.True(assembly.Migrations.ContainsKey(migrationId), $"Migration {migrationId} not found in assembly");

        var migration = assembly.CreateMigration(assembly.Migrations[migrationId], "Npgsql.EntityFrameworkCore.PostgreSQL");
        Assert.NotNull(migration);

        var upUpdates = migration.UpOperations.OfType<UpdateDataOperation>().ToList();
        var downUpdates = migration.DownOperations.OfType<UpdateDataOperation>().ToList();

        Assert.Empty(upUpdates);
        Assert.Empty(downUpdates);

        var createdTables = migration.UpOperations.OfType<CreateTableOperation>().Select(t => t.Name).ToHashSet();
        var expectedTables = new HashSet<string> { "MentorCreditGrants", "MentorCreditEntries" };
        Assert.Equal(expectedTables, createdTables);

        // Kiểm tra ràng buộc kiểm tra và chỉ mục duy nhất cho sổ cái tín dụng
        AssertUnique<MentorCreditEntry>(db, nameof(MentorCreditEntry.OperationKey));
        var designModel = db.GetService<IDesignTimeModel>().Model;
        var grantEntity = designModel.FindEntityType(typeof(MentorCreditGrant));
        Assert.NotNull(grantEntity);
        Assert.Contains(grantEntity.GetCheckConstraints(), c => c.Name == "CK_MentorCreditGrant_Quantity");
        var entryEntity = designModel.FindEntityType(typeof(MentorCreditEntry));
        Assert.NotNull(entryEntity);
        Assert.Contains(entryEntity.GetCheckConstraints(), c => c.Name == "CK_MentorCreditEntry_Amount");
    }


    [Fact]
    public void Mentor_attendance_migration_has_no_update_data_and_creates_agreed_schema()
    {
        using var db = ModelContext();
        var assembly = db.GetService<IMigrationsAssembly>();
        const string migrationId = "20261031093906_AddMentorAttendancePersistence";
        Assert.True(assembly.Migrations.ContainsKey(migrationId), $"Migration {migrationId} not found in assembly");

        var migration = assembly.CreateMigration(assembly.Migrations[migrationId], "Npgsql.EntityFrameworkCore.PostgreSQL");
        Assert.NotNull(migration);

        var upUpdates = migration.UpOperations.OfType<UpdateDataOperation>().ToList();
        var downUpdates = migration.DownOperations.OfType<UpdateDataOperation>().ToList();

        Assert.Empty(upUpdates);
        Assert.Empty(downUpdates);

        var createdTables = migration.UpOperations.OfType<CreateTableOperation>().Select(t => t.Name).ToHashSet();
        var expectedTables = new HashSet<string> { "MentorAttendanceRecords" };
        Assert.Equal(expectedTables, createdTables);

        // Kiểm tra ràng buộc kiểm tra và chỉ mục duy nhất cho hồ sơ điểm danh
        AssertUnique<MentorAttendanceRecord>(db, nameof(MentorAttendanceRecord.MentorBookingId), nameof(MentorAttendanceRecord.UserId));
        var designModel = db.GetService<IDesignTimeModel>().Model;
        var attendanceEntity = designModel.FindEntityType(typeof(MentorAttendanceRecord));
        Assert.NotNull(attendanceEntity);
        Assert.Contains(attendanceEntity.GetCheckConstraints(), c => c.Name == "CK_MentorAttendanceRecord_Duration");
        Assert.Contains(attendanceEntity.GetCheckConstraints(), c => c.Name == "CK_MentorAttendanceRecord_Reconnection");
    }


    [Fact]
    public void Mentor_cancellation_migration_has_no_update_data_and_creates_agreed_schema()
    {
        using var db = ModelContext();
        var assembly = db.GetService<IMigrationsAssembly>();
        const string migrationId = "20261031104216_AddMentorBookingCancellationMetadata";
        Assert.True(assembly.Migrations.ContainsKey(migrationId), $"Migration {migrationId} not found in assembly");

        var migration = assembly.CreateMigration(assembly.Migrations[migrationId], "Npgsql.EntityFrameworkCore.PostgreSQL");
        Assert.NotNull(migration);

        var upUpdates = migration.UpOperations.OfType<UpdateDataOperation>().ToList();
        var downUpdates = migration.DownOperations.OfType<UpdateDataOperation>().ToList();

        Assert.Empty(upUpdates);
        Assert.Empty(downUpdates);

        var addedColumns = migration.UpOperations.OfType<AddColumnOperation>()
            .Where(c => c.Table == "MentorBookings")
            .Select(c => c.Name)
            .ToHashSet();

        var expectedColumns = new HashSet<string>
        {
            "CancellationReason", "CancelledAtUtc", "CancelledByRole",
            "CancelledByUserId", "ExceptionApproved", "ExceptionReason", "ExceptionRequested"
        };
        Assert.Equal(expectedColumns, addedColumns);
    }


    [Fact]
    public void Mentor_reviews_and_disputes_migration_has_no_update_data_and_creates_agreed_schema()
    {
        using var db = ModelContext();
        var assembly = db.GetService<IMigrationsAssembly>();
        const string migrationId = "20261031111326_AddMentorReviewsAndDisputesPersistence";
        Assert.True(assembly.Migrations.ContainsKey(migrationId), $"Migration {migrationId} not found in assembly");

        var migration = assembly.CreateMigration(assembly.Migrations[migrationId], "Npgsql.EntityFrameworkCore.PostgreSQL");
        Assert.NotNull(migration);

        var upUpdates = migration.UpOperations.OfType<UpdateDataOperation>().ToList();
        var downUpdates = migration.DownOperations.OfType<UpdateDataOperation>().ToList();

        Assert.Empty(upUpdates);
        Assert.Empty(downUpdates);

        var createdTables = migration.UpOperations.OfType<CreateTableOperation>().Select(t => t.Name).ToHashSet();
        var expectedTables = new HashSet<string>
        {
            "MentorAbuseReports", "MentorDisputeHistories", "MentorDisputes", "MentorReviews"
        };
        Assert.Equal(expectedTables, createdTables);
    }

    private static void AssertUnique<T>(AppDbContext db, params string[] names) =>
        Assert.Contains(Entity<T>(db).GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(names));
}
