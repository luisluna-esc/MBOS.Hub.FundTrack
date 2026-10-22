-- =====================================================================
-- Procedimientos almacenados
-- Mismo patrón que CheckInvoice: cada listado que junta varias tablas
-- se resuelve en una sola consulta (antes eran 3 a 12 consultas por
-- llamada desde C#). Todos los filtros son opcionales (NULL = no filtra),
-- la búsqueda usa ILIKE (no distingue mayúsculas), y total_records sale
-- de COUNT(*) OVER() para paginar sin una segunda consulta.
--
-- p_restricted_district_id: lo manda el backend cuando el usuario solo
-- tiene el rol Pastor (IDataScopeService); NULL = ve todo.
--
-- Nombre del pastor con el formato del Excel: "APELLIDOS, Nombres".
--
-- Las fechas se guardan en UTC sin zona; aquí salen como timestamptz
-- (AT TIME ZONE 'UTC') para que la API las devuelva con "Z", igual que
-- los listados que van por EF.
-- =====================================================================

-- ---------------------------------------------------------------------
-- Distritos: región, pastor vigente y cantidad de iglesias.
-- ---------------------------------------------------------------------
DROP FUNCTION IF EXISTS sp_get_districts;
CREATE OR REPLACE FUNCTION sp_get_districts(
    p_district_id BIGINT,
    p_region_id BIGINT,
    p_is_missionary BOOLEAN,
    p_is_active BOOLEAN,
    p_search VARCHAR,
    p_page_number INT,
    p_page_size INT
)
RETURNS TABLE (
    district_id BIGINT,
    region_id BIGINT,
    region_name VARCHAR,
    name VARCHAR,
    is_missionary BOOLEAN,
    is_active BOOLEAN,
    created_at TIMESTAMPTZ,
    current_pastor_id BIGINT,
    current_pastor_name TEXT,
    church_count INT,
    total_records INT
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        d.district_id,
        d.region_id,
        r.name AS region_name,
        d.name,
        d.is_missionary,
        d.is_active,
        d.created_at AT TIME ZONE 'UTC' AS created_at,
        p.pastor_id AS current_pastor_id,
        (p.last_names || ', ' || p.first_names) AS current_pastor_name,
        CAST((SELECT COUNT(*) FROM church c WHERE c.district_id = d.district_id) AS INT) AS church_count,
        CAST(COUNT(*) OVER() AS INT) AS total_records
    FROM district d
    JOIN region r ON r.region_id = d.region_id
    LEFT JOIN district_pastor dp ON dp.district_id = d.district_id AND dp.end_date IS NULL
    LEFT JOIN pastor p ON p.pastor_id = dp.pastor_id
    WHERE (p_district_id IS NULL OR d.district_id = p_district_id)
      AND (p_region_id IS NULL OR d.region_id = p_region_id)
      AND (p_is_missionary IS NULL OR d.is_missionary = p_is_missionary)
      AND (p_is_active IS NULL OR d.is_active = p_is_active)
      AND (p_search IS NULL OR d.name ILIKE '%' || p_search || '%')
    ORDER BY d.name
    LIMIT p_page_size OFFSET (p_page_number - 1) * p_page_size;
$$;

-- ---------------------------------------------------------------------
-- Pastores: distrito vigente.
-- ---------------------------------------------------------------------
DROP FUNCTION IF EXISTS sp_get_pastors;
CREATE OR REPLACE FUNCTION sp_get_pastors(
    p_pastor_id BIGINT,
    p_ci VARCHAR,
    p_is_active BOOLEAN,
    p_search VARCHAR,
    p_page_number INT,
    p_page_size INT
)
RETURNS TABLE (
    pastor_id BIGINT,
    ci VARCHAR,
    first_names VARCHAR,
    last_names VARCHAR,
    full_name TEXT,
    email VARCHAR,
    is_active BOOLEAN,
    created_at TIMESTAMPTZ,
    current_district_id BIGINT,
    current_district_name VARCHAR,
    total_records INT
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        p.pastor_id,
        p.ci,
        p.first_names,
        p.last_names,
        (p.last_names || ', ' || p.first_names) AS full_name,
        p.email,
        p.is_active,
        p.created_at AT TIME ZONE 'UTC' AS created_at,
        d.district_id AS current_district_id,
        d.name AS current_district_name,
        CAST(COUNT(*) OVER() AS INT) AS total_records
    FROM pastor p
    LEFT JOIN district_pastor dp ON dp.pastor_id = p.pastor_id AND dp.end_date IS NULL
    LEFT JOIN district d ON d.district_id = dp.district_id
    WHERE (p_pastor_id IS NULL OR p.pastor_id = p_pastor_id)
      AND (p_ci IS NULL OR p.ci = p_ci)
      AND (p_is_active IS NULL OR p.is_active = p_is_active)
      AND (p_search IS NULL
           OR p.first_names ILIKE '%' || p_search || '%'
           OR p.last_names ILIKE '%' || p_search || '%'
           OR p.ci ILIKE '%' || p_search || '%')
    ORDER BY p.last_names, p.first_names
    LIMIT p_page_size OFFSET (p_page_number - 1) * p_page_size;
$$;

-- ---------------------------------------------------------------------
-- Iglesias: distrito, región, pastor vigente del distrito, la iglesia
-- con la que se fusionó y, para las Filiales, su Iglesia o Grupo madre.
-- ---------------------------------------------------------------------
DROP FUNCTION IF EXISTS sp_get_churches;
CREATE OR REPLACE FUNCTION sp_get_churches(
    p_church_id BIGINT,
    p_code VARCHAR,
    p_district_id BIGINT,
    p_region_id BIGINT,
    p_type VARCHAR,
    p_status VARCHAR,
    p_parent_church_id BIGINT,
    p_search VARCHAR,
    p_restricted_district_id BIGINT,
    p_page_number INT,
    p_page_size INT
)
RETURNS TABLE (
    church_id BIGINT,
    code VARCHAR,
    name VARCHAR,
    type VARCHAR,
    district_id BIGINT,
    district_name VARCHAR,
    region_id BIGINT,
    region_name VARCHAR,
    current_pastor_name TEXT,
    address VARCHAR,
    phone VARCHAR,
    treasurer_name VARCHAR,
    treasurer_phone VARCHAR,
    elder_name VARCHAR,
    elder_phone VARCHAR,
    status VARCHAR,
    merged_into_church_id BIGINT,
    merged_into_church_name VARCHAR,
    closed_at DATE,
    created_at TIMESTAMPTZ,
    parent_church_id BIGINT,
    parent_church_name VARCHAR,
    filial_count INT,
    total_records INT
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        c.church_id,
        c.code,
        c.name,
        c.type,
        c.district_id,
        d.name AS district_name,
        r.region_id,
        r.name AS region_name,
        (p.last_names || ', ' || p.first_names) AS current_pastor_name,
        c.address,
        c.phone,
        c.treasurer_name,
        c.treasurer_phone,
        c.elder_name,
        c.elder_phone,
        c.status,
        c.merged_into_church_id,
        mc.name AS merged_into_church_name,
        c.closed_at,
        c.created_at AT TIME ZONE 'UTC' AS created_at,
        c.parent_church_id,
        pc.name AS parent_church_name,
        CAST((SELECT COUNT(*) FROM church f WHERE f.parent_church_id = c.church_id AND f.status = 'active') AS INT) AS filial_count,
        CAST(COUNT(*) OVER() AS INT) AS total_records
    FROM church c
    JOIN district d ON d.district_id = c.district_id
    JOIN region r ON r.region_id = d.region_id
    LEFT JOIN district_pastor dp ON dp.district_id = c.district_id AND dp.end_date IS NULL
    LEFT JOIN pastor p ON p.pastor_id = dp.pastor_id
    LEFT JOIN church mc ON mc.church_id = c.merged_into_church_id
    LEFT JOIN church pc ON pc.church_id = c.parent_church_id
    WHERE (p_restricted_district_id IS NULL OR c.district_id = p_restricted_district_id)
      AND (p_church_id IS NULL OR c.church_id = p_church_id)
      AND (p_code IS NULL OR c.code = p_code)
      AND (p_district_id IS NULL OR c.district_id = p_district_id)
      AND (p_region_id IS NULL OR d.region_id = p_region_id)
      AND (p_type IS NULL OR c.type = p_type)
      AND (p_status IS NULL OR c.status = p_status)
      AND (p_parent_church_id IS NULL OR c.parent_church_id = p_parent_church_id)
      AND (p_search IS NULL OR c.name ILIKE '%' || p_search || '%' OR c.code ILIKE '%' || p_search || '%')
    ORDER BY c.name
    LIMIT p_page_size OFFSET (p_page_number - 1) * p_page_size;
$$;

-- ---------------------------------------------------------------------
-- Arqueos: iglesia, período, distrito y región de la foto del arqueo,
-- pastor, usuarios que registraron y cantidad de adjuntos y correcciones.
-- Antes: 9 consultas por llamada.
-- ---------------------------------------------------------------------
DROP FUNCTION IF EXISTS sp_get_local_fund_counts;
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

-- ---------------------------------------------------------------------
-- Vista tipo Excel de un mes: todas las Iglesias y Grupos activos (con o
-- sin arqueo, para ver cuáles faltan) más los inactivos que sí tienen
-- arqueo ese mes. Las Filiales no aparecen: van dentro del arqueo de su
-- madre (filial_count dice cuántas tiene). Las columnas del arqueo vienen NULL cuando no hay arqueo.
-- El distrito que cuenta es el de la foto del arqueo; si no hay arqueo,
-- el actual de la iglesia. Antes: 12 consultas por llamada.
-- ---------------------------------------------------------------------
DROP FUNCTION IF EXISTS sp_get_period_counts;
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

-- ---------------------------------------------------------------------
-- Auditoría: nombre del usuario. Antes: 3 consultas por llamada.
-- ---------------------------------------------------------------------
DROP FUNCTION IF EXISTS sp_get_audit_logs;
CREATE OR REPLACE FUNCTION sp_get_audit_logs(
    p_app_user_id BIGINT,
    p_table_name VARCHAR,
    p_record_id BIGINT,
    p_action VARCHAR,
    p_page_number INT,
    p_page_size INT
)
RETURNS TABLE (
    audit_log_id BIGINT,
    app_user_id BIGINT,
    app_user_full_name VARCHAR,
    action VARCHAR,
    module VARCHAR,
    table_name VARCHAR,
    record_id BIGINT,
    description VARCHAR,
    old_value TEXT,
    new_value TEXT,
    ip VARCHAR,
    user_agent VARCHAR,
    created_at TIMESTAMPTZ,
    total_records INT
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        al.audit_log_id,
        al.app_user_id,
        u.full_name AS app_user_full_name,
        al.action,
        al.module,
        al.table_name,
        al.record_id,
        al.description,
        al.old_value::TEXT,
        al.new_value::TEXT,
        al.ip,
        al.user_agent,
        al.created_at AT TIME ZONE 'UTC' AS created_at,
        CAST(COUNT(*) OVER() AS INT) AS total_records
    FROM audit_log al
    LEFT JOIN app_user u ON u.app_user_id = al.app_user_id
    WHERE (p_app_user_id IS NULL OR al.app_user_id = p_app_user_id)
      AND (p_table_name IS NULL OR al.table_name = p_table_name)
      AND (p_record_id IS NULL OR al.record_id = p_record_id)
      AND (p_action IS NULL OR al.action = p_action)
    ORDER BY al.created_at DESC
    LIMIT p_page_size OFFSET (p_page_number - 1) * p_page_size;
$$;

-- ---------------------------------------------------------------------
-- Limpieza de auditoría: la Misión pidió conservarla de 2 a 3 años.
-- Borra lo más antiguo que p_retention_years y devuelve cuántas filas
-- eliminó. La ejecuta AuditRetentionBackgroundService una vez al día.
-- ---------------------------------------------------------------------
DROP FUNCTION IF EXISTS sp_purge_audit_logs;
CREATE OR REPLACE FUNCTION sp_purge_audit_logs(
    p_retention_years INT
)
RETURNS TABLE (
    deleted_rows INT
)
LANGUAGE sql
VOLATILE
AS $$
    WITH deleted AS (
        DELETE FROM audit_log
        WHERE created_at < (NOW() AT TIME ZONE 'UTC') - make_interval(years => p_retention_years)
        RETURNING 1
    )
    SELECT CAST(COUNT(*) AS INT) AS deleted_rows FROM deleted;
$$;

-- ---------------------------------------------------------------------
-- Resumen de un mes por región, para la pantalla de inicio: cuántas
-- Iglesias y Grupos activos hay (las Filiales no hacen arqueo propio), cuántas presentaron, cuántas no, cuántas faltan
-- registrar, en qué estado están las diferencias y los montos totales.
-- Los arqueos cuentan en la región de la foto del arqueo; las iglesias
-- que faltan, en la región actual de la iglesia.
-- ---------------------------------------------------------------------
DROP FUNCTION IF EXISTS sp_get_dashboard_by_region;
CREATE OR REPLACE FUNCTION sp_get_dashboard_by_region(
    p_period_id BIGINT,
    p_restricted_district_id BIGINT
)
RETURNS TABLE (
    region_id BIGINT,
    region_name VARCHAR,
    active_churches INT,
    submitted INT,
    not_submitted INT,
    missing INT,
    differences_pending INT,
    differences_resolved INT,
    negative_acms INT,
    total_acms_balance NUMERIC,
    total_counted NUMERIC,
    total_difference NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    WITH scoped_churches AS (
        SELECT c.church_id, d.region_id
        FROM church c
        JOIN district d ON d.district_id = c.district_id
        WHERE c.status = 'active'
          AND c.type <> 'Filial'
          AND (p_restricted_district_id IS NULL OR c.district_id = p_restricted_district_id)
    ),
    period_counts AS (
        SELECT lfc.*, d.region_id
        FROM local_fund_count lfc
        JOIN district d ON d.district_id = lfc.district_id
        WHERE lfc.period_id = p_period_id
          AND (p_restricted_district_id IS NULL OR lfc.district_id = p_restricted_district_id)
    )
    SELECT
        r.region_id,
        r.name AS region_name,
        CAST((SELECT COUNT(*) FROM scoped_churches sc WHERE sc.region_id = r.region_id) AS INT) AS active_churches,
        CAST((SELECT COUNT(*) FROM period_counts k WHERE k.region_id = r.region_id AND k.submission_status = 'submitted') AS INT) AS submitted,
        CAST((SELECT COUNT(*) FROM period_counts k WHERE k.region_id = r.region_id AND k.submission_status = 'not_submitted') AS INT) AS not_submitted,
        CAST((SELECT COUNT(*) FROM scoped_churches sc
              WHERE sc.region_id = r.region_id
                AND NOT EXISTS (SELECT 1 FROM local_fund_count x WHERE x.church_id = sc.church_id AND x.period_id = p_period_id)) AS INT) AS missing,
        CAST((SELECT COUNT(*) FROM period_counts k WHERE k.region_id = r.region_id AND k.difference_status = 'pending') AS INT) AS differences_pending,
        CAST((SELECT COUNT(*) FROM period_counts k WHERE k.region_id = r.region_id AND k.difference_status = 'resolved') AS INT) AS differences_resolved,
        CAST((SELECT COUNT(*) FROM period_counts k WHERE k.region_id = r.region_id AND k.acms_balance < 0) AS INT) AS negative_acms,
        COALESCE((SELECT SUM(k.acms_balance) FROM period_counts k WHERE k.region_id = r.region_id), 0) AS total_acms_balance,
        COALESCE((SELECT SUM(k.total_amount) FROM period_counts k WHERE k.region_id = r.region_id), 0) AS total_counted,
        COALESCE((SELECT SUM(k.difference) FROM period_counts k WHERE k.region_id = r.region_id), 0) AS total_difference
    FROM region r
    WHERE EXISTS (SELECT 1 FROM scoped_churches sc WHERE sc.region_id = r.region_id)
       OR EXISTS (SELECT 1 FROM period_counts k WHERE k.region_id = r.region_id)
    ORDER BY r.name;
$$;

-- ---------------------------------------------------------------------
-- Reporte Fondo Local del año (reemplaza el Excel "FONDO LOCAL"): una fila
-- por Iglesia/Grupo y mes con saldo ACMS, arqueo y diferencia. Las
-- iglesias que no tienen arqueo en el año salen igual, con month NULL, para
-- que se vea el hueco. El backend pasa esto a una fila por iglesia con
-- los meses en columnas. Las Filiales no salen: van dentro de su madre.
-- ---------------------------------------------------------------------
DROP FUNCTION IF EXISTS sp_report_local_fund_year;
CREATE OR REPLACE FUNCTION sp_report_local_fund_year(
    p_year INT,
    p_month_from INT,
    p_month_to INT,
    p_region_id BIGINT,
    p_district_id BIGINT,
    p_restricted_district_id BIGINT
)
RETURNS TABLE (
    region_name VARCHAR,
    district_name VARCHAR,
    pastor_name TEXT,
    church_id BIGINT,
    church_code VARCHAR,
    church_name VARCHAR,
    church_type VARCHAR,
    month INT,
    submission_status VARCHAR,
    acms_balance NUMERIC,
    total_amount NUMERIC,
    difference NUMERIC,
    difference_status VARCHAR
)
LANGUAGE sql
STABLE
AS $$
    WITH year_counts AS (
        SELECT lfc.*, pe.month
        FROM local_fund_count lfc
        JOIN period pe ON pe.period_id = lfc.period_id
        WHERE pe.year = p_year AND pe.month BETWEEN p_month_from AND p_month_to
    ),
    churches AS (
        SELECT c.church_id, c.code, c.name, c.type, d.district_id, d.name AS district_name, r.region_id, r.name AS region_name
        FROM church c
        JOIN district d ON d.district_id = c.district_id
        JOIN region r ON r.region_id = d.region_id
        WHERE c.type <> 'Filial'
          AND (c.status = 'active' OR EXISTS (SELECT 1 FROM year_counts y WHERE y.church_id = c.church_id))
          AND (p_restricted_district_id IS NULL OR c.district_id = p_restricted_district_id)
          AND (p_district_id IS NULL OR c.district_id = p_district_id)
          AND (p_region_id IS NULL OR r.region_id = p_region_id)
    )
    SELECT
        ch.region_name,
        ch.district_name,
        (p.last_names || ', ' || p.first_names) AS pastor_name,
        ch.church_id,
        ch.code AS church_code,
        ch.name AS church_name,
        ch.type AS church_type,
        y.month,
        y.submission_status,
        y.acms_balance,
        y.total_amount,
        y.difference,
        y.difference_status
    FROM churches ch
    LEFT JOIN year_counts y ON y.church_id = ch.church_id
    LEFT JOIN district_pastor dp ON dp.district_id = ch.district_id AND dp.end_date IS NULL
    LEFT JOIN pastor p ON p.pastor_id = dp.pastor_id
    ORDER BY ch.region_name, ch.district_name, ch.name, y.month;
$$;

-- ---------------------------------------------------------------------
-- Reporte de pendientes de un mes: lo que se manda al cierre. Una fila
-- por Iglesia/Grupo que todavía no registró arqueo ('missing'), que no
-- lo presentó ('not_submitted') o que tiene una diferencia sin resolver
-- ('difference', todavía pendiente).
-- ---------------------------------------------------------------------
DROP FUNCTION IF EXISTS sp_report_pending;
CREATE OR REPLACE FUNCTION sp_report_pending(
    p_period_id BIGINT,
    p_region_id BIGINT,
    p_district_id BIGINT,
    p_restricted_district_id BIGINT
)
RETURNS TABLE (
    region_name VARCHAR,
    district_name VARCHAR,
    pastor_name TEXT,
    church_id BIGINT,
    church_code VARCHAR,
    church_name VARCHAR,
    church_type VARCHAR,
    pending_kind VARCHAR,
    acms_balance NUMERIC,
    total_amount NUMERIC,
    difference NUMERIC,
    difference_status VARCHAR
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        r.name AS region_name,
        d.name AS district_name,
        COALESCE(pa.last_names || ', ' || pa.first_names, cp.last_names || ', ' || cp.first_names) AS pastor_name,
        c.church_id,
        c.code AS church_code,
        c.name AS church_name,
        c.type AS church_type,
        CAST(CASE
            WHEN lfc.local_fund_count_id IS NULL THEN 'missing'
            WHEN lfc.submission_status = 'not_submitted' THEN 'not_submitted'
            ELSE 'difference'
        END AS VARCHAR) AS pending_kind,
        lfc.acms_balance,
        lfc.total_amount,
        lfc.difference,
        lfc.difference_status
    FROM church c
    LEFT JOIN local_fund_count lfc ON lfc.church_id = c.church_id AND lfc.period_id = p_period_id
    JOIN district d ON d.district_id = COALESCE(lfc.district_id, c.district_id)
    JOIN region r ON r.region_id = d.region_id
    LEFT JOIN pastor pa ON pa.pastor_id = lfc.pastor_id
    LEFT JOIN LATERAL (
        SELECT p.last_names, p.first_names
        FROM district_pastor dp
        JOIN pastor p ON p.pastor_id = dp.pastor_id
        WHERE dp.district_id = d.district_id AND dp.end_date IS NULL
    ) cp ON TRUE
    WHERE c.type <> 'Filial'
      AND (c.status = 'active' OR lfc.local_fund_count_id IS NOT NULL)
      AND (lfc.local_fund_count_id IS NULL
           OR lfc.submission_status = 'not_submitted'
           OR lfc.difference_status = 'pending')
      AND (p_restricted_district_id IS NULL OR d.district_id = p_restricted_district_id)
      AND (p_district_id IS NULL OR d.district_id = p_district_id)
      AND (p_region_id IS NULL OR r.region_id = p_region_id)
    ORDER BY r.name, d.name, c.name;
$$;

-- ---------------------------------------------------------------------
-- Reporte de diferencias: los arqueos presentados que no cuadran con el
-- ACMS en un año, con su estado, la observación, el último seguimiento
-- y cuándo se resolvió (el backend calcula cuántos días lleva abierta).
-- ---------------------------------------------------------------------
DROP FUNCTION IF EXISTS sp_report_differences;
CREATE OR REPLACE FUNCTION sp_report_differences(
    p_year INT,
    p_status VARCHAR,
    p_region_id BIGINT,
    p_district_id BIGINT,
    p_restricted_district_id BIGINT
)
RETURNS TABLE (
    region_name VARCHAR,
    district_name VARCHAR,
    pastor_name TEXT,
    church_code VARCHAR,
    church_name VARCHAR,
    church_type VARCHAR,
    period_month INT,
    acms_balance NUMERIC,
    total_amount NUMERIC,
    difference NUMERIC,
    difference_status VARCHAR,
    observation VARCHAR,
    registered_at TIMESTAMPTZ,
    last_follow_up_at TIMESTAMPTZ,
    last_justification VARCHAR,
    last_follow_up_by VARCHAR,
    resolved_at TIMESTAMPTZ
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        r.name AS region_name,
        d.name AS district_name,
        (pa.last_names || ', ' || pa.first_names) AS pastor_name,
        c.code AS church_code,
        c.name AS church_name,
        lfc.church_type,
        pe.month AS period_month,
        lfc.acms_balance,
        lfc.total_amount,
        lfc.difference,
        lfc.difference_status,
        lfc.observation,
        lfc.created_at AT TIME ZONE 'UTC' AS registered_at,
        lf.followed_up_at AT TIME ZONE 'UTC' AS last_follow_up_at,
        lf.justification AS last_justification,
        fu.full_name AS last_follow_up_by,
        rs.followed_up_at AT TIME ZONE 'UTC' AS resolved_at
    FROM local_fund_count lfc
    JOIN church c ON c.church_id = lfc.church_id
    JOIN period pe ON pe.period_id = lfc.period_id
    JOIN district d ON d.district_id = lfc.district_id
    JOIN region r ON r.region_id = d.region_id
    LEFT JOIN pastor pa ON pa.pastor_id = lfc.pastor_id
    LEFT JOIN LATERAL (
        SELECT f.followed_up_at, f.justification, f.app_user_id
        FROM difference_follow_up f
        WHERE f.local_fund_count_id = lfc.local_fund_count_id
        ORDER BY f.followed_up_at DESC, f.difference_follow_up_id DESC
        LIMIT 1
    ) lf ON TRUE
    LEFT JOIN app_user fu ON fu.app_user_id = lf.app_user_id
    LEFT JOIN LATERAL (
        SELECT f.followed_up_at
        FROM difference_follow_up f
        WHERE f.local_fund_count_id = lfc.local_fund_count_id AND f.new_status = 'resolved'
        ORDER BY f.followed_up_at DESC
        LIMIT 1
    ) rs ON TRUE
    WHERE lfc.submission_status = 'submitted'
      AND lfc.difference <> 0
      AND pe.year = p_year
      AND (p_status IS NULL OR lfc.difference_status = p_status)
      AND (p_restricted_district_id IS NULL OR lfc.district_id = p_restricted_district_id)
      AND (p_district_id IS NULL OR lfc.district_id = p_district_id)
      AND (p_region_id IS NULL OR r.region_id = p_region_id)
    ORDER BY r.name, d.name, c.name, pe.month;
$$;

-- ---------------------------------------------------------------------
-- Reporte de cumplimiento: por distrito y mes, cuántas Iglesias y Grupos
-- debían registrar arqueo, cuántos se registraron, cuántos a tiempo (hasta
-- la fecha límite del período, en hora de Bolivia), cuántos no
-- presentaron y cuántos faltan.
-- ---------------------------------------------------------------------
DROP FUNCTION IF EXISTS sp_report_compliance;
CREATE OR REPLACE FUNCTION sp_report_compliance(
    p_year INT,
    p_region_id BIGINT,
    p_district_id BIGINT,
    p_restricted_district_id BIGINT
)
RETURNS TABLE (
    region_name VARCHAR,
    district_name VARCHAR,
    pastor_name TEXT,
    period_month INT,
    due_date DATE,
    expected INT,
    registered INT,
    on_time INT,
    not_submitted INT
)
LANGUAGE sql
STABLE
AS $$
    WITH periods AS (
        SELECT pe.period_id, pe.month, pe.due_date
        FROM period pe
        WHERE pe.year = p_year
    ),
    districts AS (
        SELECT d.district_id, d.name, r.name AS region_name
        FROM district d
        JOIN region r ON r.region_id = d.region_id
        WHERE (p_restricted_district_id IS NULL OR d.district_id = p_restricted_district_id)
          AND (p_district_id IS NULL OR d.district_id = p_district_id)
          AND (p_region_id IS NULL OR r.region_id = p_region_id)
    ),
    counts AS (
        SELECT lfc.period_id, lfc.district_id, lfc.church_id, lfc.submission_status,
               ((lfc.created_at AT TIME ZONE 'UTC') AT TIME ZONE 'America/La_Paz')::date AS registered_on
        FROM local_fund_count lfc
        WHERE lfc.period_id IN (SELECT period_id FROM periods)
    )
    SELECT
        di.region_name,
        di.name AS district_name,
        (pa.last_names || ', ' || pa.first_names) AS pastor_name,
        p.month AS period_month,
        p.due_date,
        -- Las activas de hoy más las que ya no están activas pero tuvieron arqueo ese mes.
        CAST((SELECT COUNT(*) FROM church c
              WHERE c.district_id = di.district_id AND c.type <> 'Filial'
                AND (c.status = 'active'
                     OR EXISTS (SELECT 1 FROM counts k WHERE k.church_id = c.church_id AND k.period_id = p.period_id))) AS INT) AS expected,
        CAST((SELECT COUNT(*) FROM counts k WHERE k.district_id = di.district_id AND k.period_id = p.period_id) AS INT) AS registered,
        CAST((SELECT COUNT(*) FROM counts k WHERE k.district_id = di.district_id AND k.period_id = p.period_id
                                            AND k.registered_on <= p.due_date) AS INT) AS on_time,
        CAST((SELECT COUNT(*) FROM counts k WHERE k.district_id = di.district_id AND k.period_id = p.period_id
                                            AND k.submission_status = 'not_submitted') AS INT) AS not_submitted
    FROM districts di
    CROSS JOIN periods p
    LEFT JOIN district_pastor dp ON dp.district_id = di.district_id AND dp.end_date IS NULL
    LEFT JOIN pastor pa ON pa.pastor_id = dp.pastor_id
    ORDER BY di.region_name, di.name, p.month;
$$;

-- ---------------------------------------------------------------------
-- Reporte de saldo ACMS negativo: los arqueos de un rango de meses donde la
-- iglesia gastó más de lo que tenía. El backend agrupa por iglesia y
-- cuenta los meses seguidos.
-- ---------------------------------------------------------------------
DROP FUNCTION IF EXISTS sp_report_negative_acms;
CREATE OR REPLACE FUNCTION sp_report_negative_acms(
    p_year INT,
    p_month_from INT,
    p_month_to INT,
    p_region_id BIGINT,
    p_district_id BIGINT,
    p_restricted_district_id BIGINT
)
RETURNS TABLE (
    region_name VARCHAR,
    district_name VARCHAR,
    pastor_name TEXT,
    church_id BIGINT,
    church_code VARCHAR,
    church_name VARCHAR,
    church_type VARCHAR,
    period_month INT,
    acms_balance NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        r.name AS region_name,
        d.name AS district_name,
        (pa.last_names || ', ' || pa.first_names) AS pastor_name,
        c.church_id,
        c.code AS church_code,
        c.name AS church_name,
        lfc.church_type,
        pe.month AS period_month,
        lfc.acms_balance
    FROM local_fund_count lfc
    JOIN church c ON c.church_id = lfc.church_id
    JOIN period pe ON pe.period_id = lfc.period_id
    JOIN district d ON d.district_id = lfc.district_id
    JOIN region r ON r.region_id = d.region_id
    LEFT JOIN pastor pa ON pa.pastor_id = lfc.pastor_id
    WHERE lfc.acms_balance < 0
      AND pe.year = p_year
      AND pe.month BETWEEN p_month_from AND p_month_to
      AND (p_restricted_district_id IS NULL OR lfc.district_id = p_restricted_district_id)
      AND (p_district_id IS NULL OR lfc.district_id = p_district_id)
      AND (p_region_id IS NULL OR r.region_id = p_region_id)
    ORDER BY r.name, d.name, c.name, pe.month;
$$;

-- ---------------------------------------------------------------------
-- Resumen mensual por región: lo mismo que el Inicio (sp_get_dashboard_by_region)
-- pero de un rango de meses en una sola consulta, una fila por mes y región.
-- ---------------------------------------------------------------------
DROP FUNCTION IF EXISTS sp_report_region_summary;
CREATE OR REPLACE FUNCTION sp_report_region_summary(
    p_year INT,
    p_month_from INT,
    p_month_to INT,
    p_restricted_district_id BIGINT
)
RETURNS TABLE (
    period_month INT,
    region_name VARCHAR,
    active_churches INT,
    submitted INT,
    not_submitted INT,
    missing INT,
    differences_pending INT,
    differences_resolved INT,
    negative_acms INT,
    total_acms_balance NUMERIC,
    total_counted NUMERIC,
    total_difference NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    WITH periods AS (
        SELECT pe.period_id, pe.month
        FROM period pe
        WHERE pe.year = p_year AND pe.month BETWEEN p_month_from AND p_month_to
    ),
    scoped_churches AS (
        SELECT c.church_id, d.region_id
        FROM church c
        JOIN district d ON d.district_id = c.district_id
        WHERE c.status = 'active'
          AND c.type <> 'Filial'
          AND (p_restricted_district_id IS NULL OR c.district_id = p_restricted_district_id)
    ),
    counts AS (
        SELECT lfc.*, d.region_id
        FROM local_fund_count lfc
        JOIN district d ON d.district_id = lfc.district_id
        WHERE lfc.period_id IN (SELECT period_id FROM periods)
          AND (p_restricted_district_id IS NULL OR lfc.district_id = p_restricted_district_id)
    )
    SELECT
        p.month AS period_month,
        r.name AS region_name,
        CAST((SELECT COUNT(*) FROM scoped_churches sc WHERE sc.region_id = r.region_id) AS INT) AS active_churches,
        CAST((SELECT COUNT(*) FROM counts k WHERE k.period_id = p.period_id AND k.region_id = r.region_id AND k.submission_status = 'submitted') AS INT) AS submitted,
        CAST((SELECT COUNT(*) FROM counts k WHERE k.period_id = p.period_id AND k.region_id = r.region_id AND k.submission_status = 'not_submitted') AS INT) AS not_submitted,
        CAST((SELECT COUNT(*) FROM scoped_churches sc
              WHERE sc.region_id = r.region_id
                AND NOT EXISTS (SELECT 1 FROM local_fund_count x WHERE x.church_id = sc.church_id AND x.period_id = p.period_id)) AS INT) AS missing,
        CAST((SELECT COUNT(*) FROM counts k WHERE k.period_id = p.period_id AND k.region_id = r.region_id AND k.difference_status = 'pending') AS INT) AS differences_pending,
        CAST((SELECT COUNT(*) FROM counts k WHERE k.period_id = p.period_id AND k.region_id = r.region_id AND k.difference_status = 'resolved') AS INT) AS differences_resolved,
        CAST((SELECT COUNT(*) FROM counts k WHERE k.period_id = p.period_id AND k.region_id = r.region_id AND k.acms_balance < 0) AS INT) AS negative_acms,
        COALESCE((SELECT SUM(k.acms_balance) FROM counts k WHERE k.period_id = p.period_id AND k.region_id = r.region_id), 0) AS total_acms_balance,
        COALESCE((SELECT SUM(k.total_amount) FROM counts k WHERE k.period_id = p.period_id AND k.region_id = r.region_id), 0) AS total_counted,
        COALESCE((SELECT SUM(k.difference) FROM counts k WHERE k.period_id = p.period_id AND k.region_id = r.region_id), 0) AS total_difference
    FROM periods p
    CROSS JOIN region r
    WHERE EXISTS (SELECT 1 FROM scoped_churches sc WHERE sc.region_id = r.region_id)
       OR EXISTS (SELECT 1 FROM counts k WHERE k.period_id = p.period_id AND k.region_id = r.region_id)
    ORDER BY p.month, r.name;
$$;

-- ---------------------------------------------------------------------
-- Reporte de correcciones: lo que se corrigió de arqueos de meses cerrados,
-- en qué período se aplicó, el monto anterior, el nuevo, el motivo y quién.
-- ---------------------------------------------------------------------
DROP FUNCTION IF EXISTS sp_report_corrections;
CREATE OR REPLACE FUNCTION sp_report_corrections(
    p_year INT,
    p_region_id BIGINT,
    p_district_id BIGINT,
    p_restricted_district_id BIGINT
)
RETURNS TABLE (
    registered_at TIMESTAMPTZ,
    region_name VARCHAR,
    district_name VARCHAR,
    church_code VARCHAR,
    church_name VARCHAR,
    count_year INT,
    count_month INT,
    applied_year INT,
    applied_month INT,
    field_name VARCHAR,
    previous_amount NUMERIC,
    corrected_amount NUMERIC,
    reason VARCHAR,
    app_user_full_name VARCHAR
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        cr.registered_at AT TIME ZONE 'UTC' AS registered_at,
        r.name AS region_name,
        d.name AS district_name,
        c.code AS church_code,
        c.name AS church_name,
        cp.year AS count_year,
        cp.month AS count_month,
        ap.year AS applied_year,
        ap.month AS applied_month,
        cr.field_name,
        cr.previous_amount,
        cr.corrected_amount,
        cr.reason,
        u.full_name AS app_user_full_name
    FROM local_fund_count_correction cr
    JOIN local_fund_count lfc ON lfc.local_fund_count_id = cr.local_fund_count_id
    JOIN church c ON c.church_id = lfc.church_id
    JOIN period cp ON cp.period_id = lfc.period_id
    JOIN period ap ON ap.period_id = cr.applied_period_id
    JOIN district d ON d.district_id = lfc.district_id
    JOIN region r ON r.region_id = d.region_id
    LEFT JOIN app_user u ON u.app_user_id = cr.app_user_id
    WHERE ap.year = p_year
      AND (p_restricted_district_id IS NULL OR lfc.district_id = p_restricted_district_id)
      AND (p_district_id IS NULL OR lfc.district_id = p_district_id)
      AND (p_region_id IS NULL OR r.region_id = p_region_id)
    ORDER BY cr.registered_at, c.name;
$$;

-- ---------------------------------------------------------------------
-- Reporte de movimientos de iglesias: del historial, las iglesias que se
-- cerraron, fusionaron, cambiaron de distrito, de tipo o de nombre entre
-- dos fechas. Región y distrito son los actuales de la iglesia.
-- ---------------------------------------------------------------------
DROP FUNCTION IF EXISTS sp_report_church_movements;
CREATE OR REPLACE FUNCTION sp_report_church_movements(
    p_date_from DATE,
    p_date_to DATE,
    p_change_type VARCHAR,
    p_region_id BIGINT,
    p_district_id BIGINT,
    p_restricted_district_id BIGINT
)
RETURNS TABLE (
    changed_at DATE,
    change_type VARCHAR,
    region_name VARCHAR,
    district_name VARCHAR,
    church_code VARCHAR,
    church_name VARCHAR,
    church_type VARCHAR,
    old_value VARCHAR,
    new_value VARCHAR,
    reason VARCHAR,
    app_user_full_name VARCHAR
)
LANGUAGE sql
STABLE
AS $$
    SELECT
        h.changed_at,
        h.change_type,
        r.name AS region_name,
        d.name AS district_name,
        c.code AS church_code,
        c.name AS church_name,
        c.type AS church_type,
        h.old_value,
        h.new_value,
        h.reason,
        u.full_name AS app_user_full_name
    FROM church_history h
    JOIN church c ON c.church_id = h.church_id
    JOIN district d ON d.district_id = c.district_id
    JOIN region r ON r.region_id = d.region_id
    LEFT JOIN app_user u ON u.app_user_id = h.app_user_id
    WHERE h.changed_at BETWEEN p_date_from AND p_date_to
      AND (p_change_type IS NULL OR h.change_type = p_change_type)
      AND (p_restricted_district_id IS NULL OR c.district_id = p_restricted_district_id)
      AND (p_district_id IS NULL OR c.district_id = p_district_id)
      AND (p_region_id IS NULL OR r.region_id = p_region_id)
    ORDER BY h.changed_at, c.name;
$$;

-- ---------------------------------------------------------------------
-- Resumen mensual por distrito: lo mismo que sp_report_region_summary pero
-- una fila por mes y distrito (con su región y pastor actual). Lo usa el
-- reporte "Resumen" cuando se agrupa por distrito.
-- ---------------------------------------------------------------------
DROP FUNCTION IF EXISTS sp_report_district_summary;
CREATE OR REPLACE FUNCTION sp_report_district_summary(
    p_year INT,
    p_month_from INT,
    p_month_to INT,
    p_region_id BIGINT,
    p_restricted_district_id BIGINT
)
RETURNS TABLE (
    period_month INT,
    region_name VARCHAR,
    district_name VARCHAR,
    pastor_name TEXT,
    active_churches INT,
    submitted INT,
    not_submitted INT,
    missing INT,
    differences_pending INT,
    differences_resolved INT,
    negative_acms INT,
    total_acms_balance NUMERIC,
    total_counted NUMERIC,
    total_difference NUMERIC
)
LANGUAGE sql
STABLE
AS $$
    WITH periods AS (
        SELECT pe.period_id, pe.month
        FROM period pe
        WHERE pe.year = p_year AND pe.month BETWEEN p_month_from AND p_month_to
    ),
    districts AS (
        SELECT d.district_id, d.name, r.name AS region_name
        FROM district d
        JOIN region r ON r.region_id = d.region_id
        WHERE (p_restricted_district_id IS NULL OR d.district_id = p_restricted_district_id)
          AND (p_region_id IS NULL OR r.region_id = p_region_id)
    ),
    scoped_churches AS (
        SELECT c.church_id, c.district_id
        FROM church c
        WHERE c.status = 'active'
          AND c.type <> 'Filial'
          AND c.district_id IN (SELECT district_id FROM districts)
    ),
    counts AS (
        SELECT lfc.*
        FROM local_fund_count lfc
        WHERE lfc.period_id IN (SELECT period_id FROM periods)
          AND lfc.district_id IN (SELECT district_id FROM districts)
    )
    SELECT
        p.month AS period_month,
        di.region_name,
        di.name AS district_name,
        (pa.last_names || ', ' || pa.first_names) AS pastor_name,
        CAST((SELECT COUNT(*) FROM scoped_churches sc WHERE sc.district_id = di.district_id) AS INT) AS active_churches,
        CAST((SELECT COUNT(*) FROM counts k WHERE k.period_id = p.period_id AND k.district_id = di.district_id AND k.submission_status = 'submitted') AS INT) AS submitted,
        CAST((SELECT COUNT(*) FROM counts k WHERE k.period_id = p.period_id AND k.district_id = di.district_id AND k.submission_status = 'not_submitted') AS INT) AS not_submitted,
        CAST((SELECT COUNT(*) FROM scoped_churches sc
              WHERE sc.district_id = di.district_id
                AND NOT EXISTS (SELECT 1 FROM local_fund_count x WHERE x.church_id = sc.church_id AND x.period_id = p.period_id)) AS INT) AS missing,
        CAST((SELECT COUNT(*) FROM counts k WHERE k.period_id = p.period_id AND k.district_id = di.district_id AND k.difference_status = 'pending') AS INT) AS differences_pending,
        CAST((SELECT COUNT(*) FROM counts k WHERE k.period_id = p.period_id AND k.district_id = di.district_id AND k.difference_status = 'resolved') AS INT) AS differences_resolved,
        CAST((SELECT COUNT(*) FROM counts k WHERE k.period_id = p.period_id AND k.district_id = di.district_id AND k.acms_balance < 0) AS INT) AS negative_acms,
        COALESCE((SELECT SUM(k.acms_balance) FROM counts k WHERE k.period_id = p.period_id AND k.district_id = di.district_id), 0) AS total_acms_balance,
        COALESCE((SELECT SUM(k.total_amount) FROM counts k WHERE k.period_id = p.period_id AND k.district_id = di.district_id), 0) AS total_counted,
        COALESCE((SELECT SUM(k.difference) FROM counts k WHERE k.period_id = p.period_id AND k.district_id = di.district_id), 0) AS total_difference
    FROM periods p
    CROSS JOIN districts di
    LEFT JOIN district_pastor dp ON dp.district_id = di.district_id AND dp.end_date IS NULL
    LEFT JOIN pastor pa ON pa.pastor_id = dp.pastor_id
    WHERE EXISTS (SELECT 1 FROM scoped_churches sc WHERE sc.district_id = di.district_id)
       OR EXISTS (SELECT 1 FROM counts k WHERE k.period_id = p.period_id AND k.district_id = di.district_id)
    ORDER BY p.month, di.region_name, di.name;
$$;
