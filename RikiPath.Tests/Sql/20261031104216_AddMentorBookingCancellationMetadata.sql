START TRANSACTION;

ALTER TABLE "MentorBookings" ADD "CancellationReason" character varying(500);

ALTER TABLE "MentorBookings" ADD "CancelledAtUtc" timestamp without time zone;

ALTER TABLE "MentorBookings" ADD "CancelledByRole" character varying(50);

ALTER TABLE "MentorBookings" ADD "CancelledByUserId" integer;

ALTER TABLE "MentorBookings" ADD "ExceptionApproved" boolean;

ALTER TABLE "MentorBookings" ADD "ExceptionReason" character varying(500);

ALTER TABLE "MentorBookings" ADD "ExceptionRequested" boolean NOT NULL DEFAULT FALSE;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261031104216_AddMentorBookingCancellationMetadata', '8.0.8');

COMMIT;

