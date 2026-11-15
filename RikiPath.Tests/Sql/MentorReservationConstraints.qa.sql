\set ON_ERROR_STOP on
-- Chỉ chạy sau khi xác minh chính xác PostgreSQL QA tại 127.0.0.1:55439.
-- Schema này là fixture cô lập, không phải application migration.
CREATE SCHEMA qa_mentor_03a_constraints;

CREATE TABLE qa_mentor_03a_constraints.availability (
    id integer PRIMARY KEY,
    mentor_id integer NOT NULL,
    start_utc timestamptz NOT NULL,
    end_utc timestamptz NOT NULL,
    max_learners integer NOT NULL DEFAULT 1,
    generation_key text NULL,
    policy_version integer NULL,
    schedule_rule_id bigint NULL,
    local_business_date date NULL,
    UNIQUE (id, mentor_id),
    CHECK (
        (generation_key IS NULL AND policy_version IS NULL AND schedule_rule_id IS NULL
            AND local_business_date IS NULL)
        OR
        (generation_key IS NOT NULL AND policy_version IS NOT NULL AND policy_version > 0
            AND schedule_rule_id IS NOT NULL AND local_business_date IS NOT NULL
            AND max_learners = 1 AND end_utc = start_utc + interval '90 minutes')
    )
);

CREATE TABLE qa_mentor_03a_constraints.booking_history (
    id integer PRIMARY KEY,
    availability_id integer NOT NULL REFERENCES qa_mentor_03a_constraints.availability(id),
    status text NOT NULL CHECK (status IN ('Confirmed', 'Cancelled'))
);

CREATE TABLE qa_mentor_03a_constraints.reservation (
    id integer PRIMARY KEY,
    availability_id integer NOT NULL,
    mentor_id integer NOT NULL,
    booking_id integer NOT NULL UNIQUE REFERENCES qa_mentor_03a_constraints.booking_history(id),
    start_utc timestamptz NOT NULL,
    end_utc timestamptz NOT NULL,
    rest_protected_until_utc timestamptz NOT NULL,
    released_at_utc timestamptz NULL,
    FOREIGN KEY (availability_id, mentor_id)
        REFERENCES qa_mentor_03a_constraints.availability(id, mentor_id),
    CHECK (end_utc > start_utc),
    CHECK (rest_protected_until_utc = end_utc + interval '15 minutes')
);

CREATE UNIQUE INDEX reservation_active_availability
    ON qa_mentor_03a_constraints.reservation (availability_id)
    WHERE released_at_utc IS NULL;

ALTER TABLE qa_mentor_03a_constraints.reservation
    ADD CONSTRAINT reservation_active_mentor_rest_exclusion
    EXCLUDE USING gist (
        mentor_id WITH =,
        tstzrange(start_utc, rest_protected_until_utc, '[)') WITH &&
    ) WHERE (released_at_utc IS NULL);

CREATE FUNCTION qa_mentor_03a_constraints.expect_rejection(test_name text, statement text, expected_state text)
RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    BEGIN
        EXECUTE statement;
    EXCEPTION WHEN OTHERS THEN
        IF SQLSTATE = expected_state THEN
            RAISE NOTICE '% PASS: expected SQLSTATE %', test_name, SQLSTATE;
            RETURN;
        END IF;
        RAISE;
    END;
    RAISE EXCEPTION '% FAIL: statement was accepted', test_name;
END;
$$;

INSERT INTO qa_mentor_03a_constraints.availability (id,mentor_id,start_utc,end_utc) VALUES
    (1,1,'2026-11-03 09:00+00','2026-11-03 10:30+00'),
    (2,1,'2026-11-03 09:00+00','2026-11-03 10:30+00'),
    (3,1,'2026-11-03 10:00+00','2026-11-03 11:30+00'),
    (4,1,'2026-11-03 10:30+00','2026-11-03 12:00+00'),
    (5,1,'2026-11-03 10:44+00','2026-11-03 12:14+00'),
    (6,1,'2026-11-03 10:45+00','2026-11-03 12:15+00'),
    (7,2,'2026-11-03 09:00+00','2026-11-03 10:30+00'),
    (8,1,'2026-11-03 13:00+00','2026-11-03 14:30+00'),
    (9,1,'2026-11-03 15:00+00','2026-11-03 16:30+00'),
    (10,1,'2026-11-03 15:30+00','2026-11-03 17:00+00');

UPDATE qa_mentor_03a_constraints.availability
SET generation_key='qa-slot-' || id, policy_version=1, schedule_rule_id=1,
    local_business_date='2026-11-03'
WHERE id BETWEEN 1 AND 10;

-- Bản ghi lịch sử 50 phút/nhóm không có metadata sinh lịch mới.
INSERT INTO qa_mentor_03a_constraints.availability
    (id,mentor_id,start_utc,end_utc,max_learners)
VALUES (11,1,'2026-11-03 18:00+00','2026-11-03 18:50+00',4);

INSERT INTO qa_mentor_03a_constraints.booking_history VALUES
    (101,1,'Confirmed'),(102,2,'Confirmed'),(103,3,'Confirmed'),
    (104,4,'Confirmed'),(105,5,'Confirmed'),(106,6,'Confirmed'),
    (107,7,'Confirmed'),(108,8,'Confirmed'),(109,9,'Confirmed'),
    (110,10,'Confirmed'),(111,6,'Confirmed'),(112,11,'Cancelled');

INSERT INTO qa_mentor_03a_constraints.reservation
    (id,availability_id,mentor_id,booking_id,start_utc,end_utc,rest_protected_until_utc)
VALUES (201,1,1,101,'2026-11-03 09:00+00','2026-11-03 10:30+00','2026-11-03 10:45+00');

-- T01: Kiểm tra metadata của partial unique độc lập với lỗi GiST có thể xảy ra trước.
DO $$ BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes WHERE schemaname='qa_mentor_03a_constraints'
          AND indexname='reservation_active_availability'
          AND indexdef LIKE '%UNIQUE INDEX%'
          AND indexdef LIKE '%released_at_utc IS NULL%'
    ) THEN RAISE EXCEPTION 'T01 FAIL: missing active-slot unique index'; END IF;
    RAISE NOTICE 'T01 PASS: active Availability ID uniqueness is indexed';
END $$;

-- T15/T16: Lịch sử 50 phút được giữ, nhưng slot sinh mới không được có thời lượng này.
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM qa_mentor_03a_constraints.availability
                   WHERE id=11 AND max_learners=4 AND generation_key IS NULL
                     AND end_utc-start_utc=interval '50 minutes')
       OR NOT EXISTS (SELECT 1 FROM qa_mentor_03a_constraints.booking_history
                      WHERE id=112 AND status='Cancelled')
    THEN RAISE EXCEPTION 'T15 FAIL'; END IF;
    RAISE NOTICE 'T15 PASS: synthetic historical 50-minute/group row and booking preserved';
END $$;
SELECT qa_mentor_03a_constraints.expect_rejection('T16',
    'INSERT INTO qa_mentor_03a_constraints.availability (id,mentor_id,start_utc,end_utc,generation_key,policy_version,schedule_rule_id,local_business_date) VALUES (12,1,''2026-11-03 19:00+00'',''2026-11-03 19:50+00'',''qa-bad'',1,1,''2026-11-03'')', '23514');

-- T13: MentorId của reservation phải khớp Mentor sở hữu Availability.
SELECT qa_mentor_03a_constraints.expect_rejection('T13',
    'INSERT INTO qa_mentor_03a_constraints.reservation VALUES (213,8,2,108,''2026-11-03 13:00+00'',''2026-11-03 14:30+00'',''2026-11-03 14:45+00'',NULL)', '23503');
-- T14: Không được khai báo sai độ dài nghỉ so với chính sách 15 phút.
SELECT qa_mentor_03a_constraints.expect_rejection('T14',
    'INSERT INTO qa_mentor_03a_constraints.reservation VALUES (214,8,1,108,''2026-11-03 13:00+00'',''2026-11-03 14:30+00'',''2026-11-03 14:44+00'',NULL)', '23514');

-- T02: ID khác nhưng giờ trùng hoàn toàn.
SELECT qa_mentor_03a_constraints.expect_rejection('T02',
    'INSERT INTO qa_mentor_03a_constraints.reservation VALUES (202,2,1,102,''2026-11-03 09:00+00'',''2026-11-03 10:30+00'',''2026-11-03 10:45+00'',NULL)', '23P01');
-- T03: Trùng một phần.
SELECT qa_mentor_03a_constraints.expect_rejection('T03',
    'INSERT INTO qa_mentor_03a_constraints.reservation VALUES (203,3,1,103,''2026-11-03 10:00+00'',''2026-11-03 11:30+00'',''2026-11-03 11:45+00'',NULL)', '23P01');
-- T04: Giờ học liền kề nhưng chưa có khoảng nghỉ.
SELECT qa_mentor_03a_constraints.expect_rejection('T04',
    'INSERT INTO qa_mentor_03a_constraints.reservation VALUES (204,4,1,104,''2026-11-03 10:30+00'',''2026-11-03 12:00+00'',''2026-11-03 12:15+00'',NULL)', '23P01');
-- T05: Nghỉ 14 phút vẫn trùng khoảng bảo vệ.
SELECT qa_mentor_03a_constraints.expect_rejection('T05',
    'INSERT INTO qa_mentor_03a_constraints.reservation VALUES (205,5,1,105,''2026-11-03 10:44+00'',''2026-11-03 12:14+00'',''2026-11-03 12:29+00'',NULL)', '23P01');

-- T06: Đúng 15 phút, hai khoảng [) chỉ chạm biên.
INSERT INTO qa_mentor_03a_constraints.reservation VALUES
    (206,6,1,106,'2026-11-03 10:45+00','2026-11-03 12:15+00','2026-11-03 12:30+00',NULL);
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM qa_mentor_03a_constraints.reservation WHERE id=206)
    THEN RAISE EXCEPTION 'T06 FAIL'; END IF;
    RAISE NOTICE 'T06 PASS: exact 15-minute rest and half-open boundary';
END $$;

-- T07: Mentor khác được giữ cùng khoảng giờ.
INSERT INTO qa_mentor_03a_constraints.reservation VALUES
    (207,7,2,107,'2026-11-03 09:00+00','2026-11-03 10:30+00','2026-11-03 10:45+00',NULL);
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM qa_mentor_03a_constraints.reservation WHERE id=207)
    THEN RAISE EXCEPTION 'T07 FAIL'; END IF;
    RAISE NOTICE 'T07 PASS: independent Mentor';
END $$;

-- T08: Reservation chưa nhả tiếp tục chặn ID cũ.
SELECT qa_mentor_03a_constraints.expect_rejection('T08',
    'INSERT INTO qa_mentor_03a_constraints.reservation VALUES (208,6,1,111,''2026-11-03 10:45+00'',''2026-11-03 12:15+00'',''2026-11-03 12:30+00'',NULL)', '23505');

-- T09/T10: Hủy giữ nguyên lịch sử; nhả reservation rồi đặt lại cùng slot.
UPDATE qa_mentor_03a_constraints.booking_history SET status='Cancelled' WHERE id=106;
UPDATE qa_mentor_03a_constraints.reservation SET released_at_utc='2026-11-02 00:00+00' WHERE id=206;
INSERT INTO qa_mentor_03a_constraints.reservation VALUES
    (208,6,1,111,'2026-11-03 10:45+00','2026-11-03 12:15+00','2026-11-03 12:30+00',NULL);
DO $$ BEGIN
    IF (SELECT count(*) FROM qa_mentor_03a_constraints.reservation WHERE availability_id=6) <> 2
       OR (SELECT count(*) FROM qa_mentor_03a_constraints.reservation WHERE availability_id=6 AND released_at_utc IS NULL) <> 1
    THEN RAISE EXCEPTION 'T09 FAIL'; END IF;
    RAISE NOTICE 'T09 PASS: released slot can be rebooked';
    IF NOT EXISTS (SELECT 1 FROM qa_mentor_03a_constraints.booking_history WHERE id=106 AND status='Cancelled')
    THEN RAISE EXCEPTION 'T10 FAIL'; END IF;
    RAISE NOTICE 'T10 PASS: cancelled booking history preserved';
END $$;

-- T11: Ghi trong transaction rồi rollback không để lại reservation.
BEGIN;
INSERT INTO qa_mentor_03a_constraints.reservation VALUES
    (211,8,1,108,'2026-11-03 13:00+00','2026-11-03 14:30+00','2026-11-03 14:45+00',NULL);
ROLLBACK;
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM qa_mentor_03a_constraints.reservation WHERE id=211)
    THEN RAISE EXCEPTION 'T11 FAIL'; END IF;
    RAISE NOTICE 'T11 PASS: transaction rollback';
END $$;
