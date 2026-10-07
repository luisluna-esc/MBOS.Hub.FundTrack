-- =====================================================================
-- MIGRATION 02: business rules from "Preguntas.docx" (2026-10-05).
-- Already folded into dataBase.sql for fresh installs; run this only on a
-- database created with the original dataBase.sql.
-- =====================================================================

-- A pastor cannot lead two districts at the same time.
CREATE UNIQUE INDEX IF NOT EXISTS uq_district_pastor_current_pastor ON district_pastor(pastor_id) WHERE end_date IS NULL;

ALTER TABLE church
    ADD CONSTRAINT ck_church_type CHECK (type IN ('Iglesia', 'Grupo', 'Filial')),
    ADD CONSTRAINT ck_church_status CHECK (status IN ('active', 'closed', 'merged'));

ALTER TABLE church_history
    ADD CONSTRAINT ck_church_history_change_type CHECK (change_type IN ('type', 'district', 'status', 'name'));

ALTER TABLE period
    ADD CONSTRAINT ck_period_status CHECK (status IN ('open', 'closed'));

ALTER TABLE local_fund_count
    ADD CONSTRAINT ck_local_fund_count_church_type CHECK (church_type IN ('Iglesia', 'Grupo', 'Filial')),
    ADD CONSTRAINT ck_local_fund_count_submission_status CHECK (submission_status IN ('submitted', 'not_submitted')),
    ADD CONSTRAINT ck_local_fund_count_difference_status CHECK (difference_status IN ('none', 'pending', 'justified', 'resolved'));

ALTER TABLE difference_follow_up
    ADD CONSTRAINT ck_difference_follow_up_previous_status CHECK (previous_status IN ('none', 'pending', 'justified', 'resolved')),
    ADD CONSTRAINT ck_difference_follow_up_new_status CHECK (new_status IN ('none', 'pending', 'justified', 'resolved'));

ALTER TABLE local_fund_count_correction
    ADD CONSTRAINT ck_lfc_correction_field_name CHECK (field_name IN ('acms_balance', 'cash_amount', 'bank_amount'));
