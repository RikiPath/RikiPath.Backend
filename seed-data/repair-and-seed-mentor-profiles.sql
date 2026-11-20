BEGIN;

SET LOCAL lock_timeout = '10s';
SET LOCAL statement_timeout = '30s';
SELECT pg_advisory_xact_lock(hashtextextended('rikipath:seed:mentor-profiles', 0));

-- Match MentorProfileConfig and the existing AddMentorProfileEntity migration.
CREATE TABLE IF NOT EXISTS public."MentorProfiles" (
    "MentorId" integer NOT NULL,
    "Bio" character varying(2000),
    "Expertise" character varying(500),
    "JlptLevel" character varying(20),
    "YearsOfExperience" integer,
    "Workplace" character varying(200),
    "VerificationDocumentUrl" character varying(1000),
    "IntroductionVideoUrl" character varying(1000),
    "VerificationStatus" character varying(30) NOT NULL,
    "SubmittedAt" timestamp without time zone,
    "ReviewedAt" timestamp without time zone,
    "ReviewedByAdminName" character varying(100),
    "RejectionReason" character varying(1000),
    "ReviewNotes" character varying(1000),
    "CreatedDate" timestamp without time zone,
    "ModifiedDate" timestamp without time zone,
    "CreatedBy" uuid,
    "ModifiedBy" uuid,
    "IsDeleted" boolean NOT NULL,
    CONSTRAINT "PK_MentorProfiles" PRIMARY KEY ("MentorId"),
    CONSTRAINT "FK_MentorProfiles_Users_MentorId"
        FOREIGN KEY ("MentorId") REFERENCES public."Users" ("Id") ON DELETE CASCADE
);

-- Verification documents are served through authenticated backend endpoints.
ALTER TABLE public."MentorProfiles" ENABLE ROW LEVEL SECURITY;

WITH seed (
    mentor_id, email, bio, expertise, jlpt_level, experience_years, workplace
) AS (
    VALUES
        (
            900002,
            'mentor1@rikipath.local',
            'Chuyên gia tiếng Nhật với 8 năm kinh nghiệm giảng dạy JLPT N1, N2 và tiếng Nhật doanh nghiệp.',
            'Luyện thi JLPT N1-N2, Tiếng Nhật thương mại, Phỏng vấn xin việc',
            'N1',
            8,
            'Riki Nihongo Academy'
        ),
        (
            900003,
            'mentor2@rikipath.local',
            'Giảng viên tiếng Nhật bản xứ, chuyên sâu về phát âm và giao tiếp Kaiwa thực tế.',
            'Giao tiếp Kaiwa tự nhiên, Luyện phát âm chuẩn Tokyo, JLPT N3-N2',
            'N1',
            5,
            'Riki Nihongo Academy'
        )
)
INSERT INTO public."MentorProfiles" (
    "MentorId", "Bio", "Expertise", "JlptLevel", "YearsOfExperience",
    "Workplace", "VerificationStatus", "SubmittedAt", "ReviewedAt",
    "ReviewedByAdminName", "CreatedDate", "IsDeleted"
)
SELECT
    s.mentor_id, s.bio, s.expertise, s.jlpt_level, s.experience_years,
    s.workplace, 'Approved', TIMESTAMP '2026-09-28 00:00:00',
    TIMESTAMP '2026-09-28 00:00:00', 'Admin System',
    TIMESTAMP '2026-09-28 00:00:00', FALSE
FROM seed s
JOIN public."Users" u ON u."Id" = s.mentor_id
    AND lower(u."Email") = s.email
    AND u."Role" = 'Mentor'
    AND NOT u."IsDeleted"
ON CONFLICT ("MentorId") DO NOTHING;

SELECT "MentorId", "VerificationStatus", "IsDeleted"
FROM public."MentorProfiles"
WHERE "MentorId" IN (900002, 900003)
ORDER BY "MentorId";

COMMIT;
