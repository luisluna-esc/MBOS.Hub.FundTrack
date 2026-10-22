-- =====================================================================
-- Migración 04: detalle del arqueo (formulario nuevo del tesorero).
--
-- El arqueo deja de ser "efectivo + banco" y pasa a ser:
--   saldo según extracto bancario + depósito bancario
--   + billetes y monedas en moneda nacional (fondo local)
--   + dólares convertidos a Bs (monto en USD x tipo de cambio)
--   + documentos a rendir (facturas, recibos y otros, varios por arqueo)
--   = total fondos en custodia del tesorero (total_amount)
-- y la diferencia sigue siendo total_amount menos el saldo ACMS.
--
-- 1. Columnas nuevas en local_fund_count; lo que había en cash_amount pasa a
--    local_cash y lo de bank_amount a bank_statement_balance, y se borran
--    las dos columnas viejas.
-- 2. Tabla local_fund_count_document para los documentos a rendir.
-- 3. Las correcciones (meses cerrados) ahora corrigen el saldo ACMS o el
--    total en custodia.
-- 4. sp_get_local_fund_counts y sp_get_period_counts devuelven las columnas
--    nuevas.
-- Todo en una transacción: si algo falla no queda nada a medias.
-- =====================================================================

BEGIN;

-- 1. Montos del arqueo -------------------------------------------------
ALTER TABLE local_fund_count
    ADD COLUMN bank_statement_balance NUMERIC(14,2) NOT NULL DEFAULT 0,
    ADD COLUMN bank_deposit NUMERIC(14,2) NOT NULL DEFAULT 0,
    ADD COLUMN local_cash NUMERIC(14,2) NOT NULL DEFAULT 0,
    ADD COLUMN usd_amount NUMERIC(14,2) NOT NULL DEFAULT 0,
    ADD COLUMN exchange_rate NUMERIC(10,4) NOT NULL DEFAULT 0,
    ADD COLUMN usd_amount_bs NUMERIC(14,2) NOT NULL DEFAULT 0,
    ADD COLUMN documents_total NUMERIC(14,2) NOT NULL DEFAULT 0;

UPDATE local_fund_count
SET local_cash = cash_amount,
    bank_statement_balance = bank_amount;

-- Las funciones de listado leen las columnas viejas: se quitan antes y se vuelven a crear abajo.
DROP FUNCTION IF EXISTS sp_get_local_fund_counts;
DROP FUNCTION IF EXISTS sp_get_period_counts;

ALTER TABLE local_fund_count
    DROP COLUMN cash_amount,
    DROP COLUMN bank_amount;

-- 2. Documentos a rendir ----------------------------------------------
CREATE TABLE local_fund_count_document (
    local_fund_count_document_id BIGSERIAL PRIMARY KEY,
    local_fund_count_id BIGINT NOT NULL,
    document_date DATE NOT NULL,
    document_number VARCHAR(50),
    detail VARCHAR(500) NOT NULL,
    amount NUMERIC(14,2) NOT NULL,
    CONSTRAINT ck_lfc_document_amount CHECK (amount > 0),
    CONSTRAINT fk_lfc_document_count FOREIGN KEY (local_fund_count_id) REFERENCES local_fund_count(local_fund_count_id) ON DELETE CASCADE
);

CREATE INDEX ix_lfc_document_count ON local_fund_count_document (local_fund_count_id);

-- 3. Correcciones -----------------------------------------------------
UPDATE local_fund_count_correction SET field_name = 'total_amount' WHERE field_name IN ('cash_amount', 'bank_amount');

ALTER TABLE local_fund_count_correction DROP CONSTRAINT ck_lfc_correction_field_name;
ALTER TABLE local_fund_count_correction
    ADD CONSTRAINT ck_lfc_correction_field_name CHECK (field_name IN ('acms_balance', 'total_amount'));

-- 4. Funciones de listado ---------------------------------------------
CREATE OR REPLACE FUNCTION sp_get_local_fund_counts(
    p_local_fund_count_id BIGINT,
    p_period_id BIGINT,
    p_year INT,
    p_church_id BIGINT,
    p_district_id BIGINT,
    p_region_id BIGINT,
    p_pastor_id BIGINT,
    p_submission_status VARCHAR,
    p_difference_status VARCHAR,
    p_search VARCHAR,
    p_restricted_district_id BIGINT,
    p_page_number INT,
    p_page_size INT
)
RETURNS TABLE (
    local_fund_count_id BIGINT,
    church_id BIGINT,
    church_code VARCHAR,
    church_name VARCHAR,
    period_id BIGINT,
    period_year INT,
    period_month INT,
    period_status VARCHAR,
    district_id BIGINT,
    district_name VARCHAR,
    region_id BIGINT,
    region_name VARCHAR,
    pastor_id BIGINT,
    pastor_name TEXT,
    church_type VARCHAR,
    submission_status VARCHAR,
    acms_balance NUMERIC,
    bank_statement_balance NUMERIC,
    bank_deposit NUMERIC,
    local_cash NUMERIC,
    usd_amount NUMERIC,
    exchange_rate NUMERIC,
    usd_amount_bs NUMERIC,
    documents_total NUMERIC,
    total_amount NUMERIC,
    difference NUMERIC,
    difference_status VARCHAR,
    count_date DATE,
    treasurer_name VARCHAR,
    elder_name VARCHAR,
    signed_by_pastor BOOLEAN,
    signed_by_elder BOOLEAN,
    observation VARCHAR,
    attachment_count INT,
    correction_count INT,
    created_by_id BIGINT,
    created_by_full_name VARCHAR,
    created_at TIMESTAMPTZ,
    updated_by_id BIGINT,
    updated_by_full_name VARCHAR,
    updated_at TIMESTAMPTZ,
    total_records INT
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        lfc.local_fund_count_id,
        lfc.church_id,
        c.code AS church_code,
        c.name AS church_name,
        lfc.period_id,
        pe.year AS period_year,
        pe.month AS period_month,
        pe.status AS period_status,
        lfc.district_id,
        d.name AS district_name,
        r.region_id,
        r.name AS region_name,
        lfc.pastor_id,
        (pa.last_names || ', ' || pa.first_names) AS pastor_name,
        lfc.church_type,
        lfc.submission_status,
        lfc.acms_balance,
        lfc.bank_statement_balance,
        lfc.bank_deposit,
        lfc.local_cash,
        lfc.usd_amount,
        lfc.exchange_rate,
        lfc.usd_amount_bs,
        lfc.documents_total,
        lfc.total_amount,
        lfc.difference,
        lfc.difference_status,
        lfc.count_date,
        lfc.treasurer_name,
        lfc.elder_name,
        lfc.signed_by_pastor,
        lfc.signed_by_elder,
        lfc.observation,
        CAST((SELECT COUNT(*) FROM local_fund_count_attachment a WHERE a.local_fund_count_id = lfc.local_fund_count_id) AS INT) AS attachment_count,
        CAST((SELECT COUNT(*) FROM local_fund_count_correction cr WHERE cr.local_fund_count_id = lfc.local_fund_count_id) AS INT) AS correction_count,
        lfc.created_by AS created_by_id,
        cu.full_name AS created_by_full_name,
        lfc.created_at AT TIME ZONE 'UTC' AS created_at,
        lfc.updated_by AS updated_by_id,
        uu.full_name AS updated_by_full_name,
        lfc.updated_at AT TIME ZONE 'UTC' AS updated_at,
        CAST(COUNT(*) OVER() AS INT) AS total_records
    FROM local_fund_count lfc
    JOIN church c ON c.church_id = lfc.church_id
    JOIN period pe ON pe.period_id = lfc.period_id
    JOIN district d ON d.district_id = lfc.district_id
    JOIN region r ON r.region_id = d.region_id
    LEFT JOIN pastor pa ON pa.pastor_id = lfc.pastor_id
    LEFT JOIN app_user cu ON cu.app_user_id = lfc.created_by
    LEFT JOIN app_user uu ON uu.app_user_id = lfc.updated_by
    WHERE (p_restricted_district_id IS NULL OR lfc.district_id = p_restricted_district_id)
      AND (p_local_fund_count_id IS NULL OR lfc.local_fund_count_id = p_local_fund_count_id)
      AND (p_period_id IS NULL OR lfc.period_id = p_period_id)
      AND (p_year IS NULL OR pe.year = p_year)
      AND (p_church_id IS NULL OR lfc.church_id = p_church_id)
      AND (p_district_id IS NULL OR lfc.district_id = p_district_id)
      AND (p_region_id IS NULL OR d.region_id = p_region_id)
      AND (p_pastor_id IS NULL OR lfc.pastor_id = p_pastor_id)
      AND (p_submission_status IS NULL OR lfc.submission_status = p_submission_status)
      AND (p_difference_status IS NULL OR lfc.difference_status = p_difference_status)
      AND (p_search IS NULL OR c.name ILIKE '%' || p_search || '%' OR c.code ILIKE '%' || p_search || '%')
    ORDER BY pe.year DESC, pe.month DESC, c.name
    LIMIT p_page_size OFFSET (p_page_number - 1) * p_page_size;
$$;

CREATE OR REPLACE FUNCTION sp_get_period_counts(
    p_period_id BIGINT,
    p_region_id BIGINT,
    p_district_id BIGINT,
    p_restricted_district_id BIGINT
)
RETURNS TABLE (
    church_id BIGINT,
    church_code VARCHAR,
    church_name VARCHAR,
    church_type VARCHAR,
    district_id BIGINT,
    district_name VARCHAR,
    region_id BIGINT,
    region_name VARCHAR,
    pastor_id BIGINT,
    pastor_name TEXT,
    period_year INT,
    period_month INT,
    period_status VARCHAR,
    local_fund_count_id BIGINT,
    count_church_type VARCHAR,
    submission_status VARCHAR,
    acms_balance NUMERIC,
    bank_statement_balance NUMERIC,
    bank_deposit NUMERIC,
    local_cash NUMERIC,
    usd_amount NUMERIC,
    exchange_rate NUMERIC,
    usd_amount_bs NUMERIC,
    documents_total NUMERIC,
    total_amount NUMERIC,
    difference NUMERIC,
    difference_status VARCHAR,
    count_date DATE,
    treasurer_name VARCHAR,
    elder_name VARCHAR,
    signed_by_pastor BOOLEAN,
    signed_by_elder BOOLEAN,
    observation VARCHAR,
    attachment_count INT,
    correction_count INT,
    created_by_id BIGINT,
    created_by_full_name VARCHAR,
    created_at TIMESTAMPTZ,
    updated_by_id BIGINT,
    updated_by_full_name VARCHAR,
    updated_at TIMESTAMPTZ,
    filial_count INT
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        c.church_id,
        c.code AS church_code,
        c.name AS church_name,
        c.type AS church_type,
        d.district_id,
        d.name AS district_name,
        r.region_id,
        r.name AS region_name,
        COALESCE(lfc.pastor_id, cp.pastor_id) AS pastor_id,
        COALESCE(pa.last_names || ', ' || pa.first_names, cp.last_names || ', ' || cp.first_names) AS pastor_name,
        pe.year AS period_year,
        pe.month AS period_month,
        pe.status AS period_status,
        lfc.local_fund_count_id,
        lfc.church_type AS count_church_type,
        lfc.submission_status,
        lfc.acms_balance,
        lfc.bank_statement_balance,
        lfc.bank_deposit,
        lfc.local_cash,
        lfc.usd_amount,
        lfc.exchange_rate,
        lfc.usd_amount_bs,
        lfc.documents_total,
        lfc.total_amount,
        lfc.difference,
        lfc.difference_status,
        lfc.count_date,
        lfc.treasurer_name,
        lfc.elder_name,
        lfc.signed_by_pastor,
        lfc.signed_by_elder,
        lfc.observation,
        CAST((SELECT COUNT(*) FROM local_fund_count_attachment a WHERE a.local_fund_count_id = lfc.local_fund_count_id) AS INT) AS attachment_count,
        CAST((SELECT COUNT(*) FROM local_fund_count_correction cr WHERE cr.local_fund_count_id = lfc.local_fund_count_id) AS INT) AS correction_count,
        lfc.created_by AS created_by_id,
        cu.full_name AS created_by_full_name,
        lfc.created_at AT TIME ZONE 'UTC' AS created_at,
        lfc.updated_by AS updated_by_id,
        uu.full_name AS updated_by_full_name,
        lfc.updated_at AT TIME ZONE 'UTC' AS updated_at,
        CAST((SELECT COUNT(*) FROM church f WHERE f.parent_church_id = c.church_id AND f.status = 'active') AS INT) AS filial_count
    FROM church c
    CROSS JOIN period pe
    LEFT JOIN local_fund_count lfc ON lfc.church_id = c.church_id AND lfc.period_id = pe.period_id
    JOIN district d ON d.district_id = COALESCE(lfc.district_id, c.district_id)
    JOIN region r ON r.region_id = d.region_id
    LEFT JOIN pastor pa ON pa.pastor_id = lfc.pastor_id
    LEFT JOIN LATERAL (
        SELECT p.pastor_id, p.last_names, p.first_names
        FROM district_pastor dp
        JOIN pastor p ON p.pastor_id = dp.pastor_id
        WHERE dp.district_id = d.district_id AND dp.end_date IS NULL
    ) cp ON TRUE
    LEFT JOIN app_user cu ON cu.app_user_id = lfc.created_by
    LEFT JOIN app_user uu ON uu.app_user_id = lfc.updated_by
    WHERE pe.period_id = p_period_id
      AND ((c.status = 'active' AND c.type <> 'Filial') OR lfc.local_fund_count_id IS NOT NULL)
      AND (p_restricted_district_id IS NULL OR d.district_id = p_restricted_district_id)
      AND (p_district_id IS NULL OR d.district_id = p_district_id)
      AND (p_region_id IS NULL OR r.region_id = p_region_id)
    ORDER BY r.name, d.name, c.name;
$$;

COMMIT;
