-- =====================================================================
-- FUNDTRACK - CONSOLIDATED POSTGRESQL SCRIPT
-- Local fund counts (arqueos) per church, per period.
-- Creation order respects Foreign Key dependencies.
-- =====================================================================

-- =====================================================================
-- LEVEL 1: TABLES WITHOUT DEPENDENCIES
-- =====================================================================

CREATE TABLE role (
    role_id BIGSERIAL PRIMARY KEY,
    name VARCHAR(50) NOT NULL,
    description VARCHAR(255),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_role_name UNIQUE (name)
);

CREATE TABLE region (
    region_id BIGSERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    pending_balance NUMERIC(14,2) NOT NULL DEFAULT 0,
    CONSTRAINT uq_region_name UNIQUE (name)
);

-- ci es opcional: la lista oficial de pastores no lo trae. Es único cuando se llena.
CREATE TABLE pastor (
    pastor_id BIGSERIAL PRIMARY KEY,
    ci VARCHAR(20),
    first_names VARCHAR(100) NOT NULL,
    last_names VARCHAR(100) NOT NULL,
    email VARCHAR(150),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_pastor_ci UNIQUE (ci)
);

CREATE TABLE menu (
    menu_id BIGSERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    translation_key VARCHAR(100),
    route VARCHAR(150),
    icon VARCHAR(50),
    parent_menu_id BIGINT,
    display_order INT NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT fk_menu_parent FOREIGN KEY (parent_menu_id) REFERENCES menu(menu_id),
    CONSTRAINT uq_menu_route UNIQUE (route)
);

CREATE INDEX idx_menu_parent ON menu(parent_menu_id);

-- =====================================================================
-- LEVEL 2: depend on level 1
-- =====================================================================

CREATE TABLE district (
    district_id BIGSERIAL PRIMARY KEY,
    region_id BIGINT NOT NULL,
    name VARCHAR(100) NOT NULL,
    is_missionary BOOLEAN NOT NULL DEFAULT FALSE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_district_region FOREIGN KEY (region_id) REFERENCES region(region_id),
    CONSTRAINT uq_district_region_name UNIQUE (region_id, name)
);

CREATE INDEX idx_district_region ON district(region_id);

CREATE TABLE app_user (
    app_user_id BIGSERIAL PRIMARY KEY,
    ci VARCHAR(20),
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(200) NOT NULL,
    email VARCHAR(150) NOT NULL,
    phone VARCHAR(30),
    pastor_id BIGINT,
    must_change_password BOOLEAN NOT NULL DEFAULT TRUE,
    password_changed_at TIMESTAMP,
    failed_login_attempts INT NOT NULL DEFAULT 0,
    locked_until TIMESTAMP,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    last_login TIMESTAMP,
    created_by BIGINT,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_app_user_email UNIQUE (email),
    CONSTRAINT fk_app_user_pastor FOREIGN KEY (pastor_id) REFERENCES pastor(pastor_id),
    CONSTRAINT fk_app_user_created_by FOREIGN KEY (created_by) REFERENCES app_user(app_user_id)
);

CREATE INDEX idx_app_user_pastor ON app_user(pastor_id);

-- =====================================================================
-- LEVEL 3: depend on app_user, district, menu, role
-- =====================================================================

CREATE TABLE refresh_token (
    refresh_token_id BIGSERIAL PRIMARY KEY,
    app_user_id BIGINT NOT NULL,
    token VARCHAR(500) NOT NULL,
    expires_at TIMESTAMP NOT NULL,
    is_revoked BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    created_ip VARCHAR(50),
    CONSTRAINT fk_refresh_token_app_user FOREIGN KEY (app_user_id) REFERENCES app_user(app_user_id) ON DELETE CASCADE,
    CONSTRAINT uq_refresh_token_token UNIQUE (token)
);

CREATE INDEX idx_refresh_token_app_user ON refresh_token(app_user_id);

CREATE TABLE password_reset (
    password_reset_id BIGSERIAL PRIMARY KEY,
    app_user_id BIGINT NOT NULL,
    token_hash VARCHAR(255) NOT NULL,
    expires_at TIMESTAMP NOT NULL,
    is_used BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_password_reset_app_user FOREIGN KEY (app_user_id) REFERENCES app_user(app_user_id) ON DELETE CASCADE,
    CONSTRAINT uq_password_reset_token_hash UNIQUE (token_hash)
);

CREATE INDEX idx_password_reset_app_user ON password_reset(app_user_id);

CREATE TABLE app_user_role (
    app_user_id BIGINT NOT NULL,
    role_id BIGINT NOT NULL,
    assigned_by BIGINT,
    assigned_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_app_user_role PRIMARY KEY (app_user_id, role_id),
    CONSTRAINT fk_app_user_role_app_user FOREIGN KEY (app_user_id) REFERENCES app_user(app_user_id) ON DELETE CASCADE,
    CONSTRAINT fk_app_user_role_role FOREIGN KEY (role_id) REFERENCES role(role_id) ON DELETE CASCADE,
    CONSTRAINT fk_app_user_role_assigned_by FOREIGN KEY (assigned_by) REFERENCES app_user(app_user_id)
);

CREATE INDEX idx_app_user_role_role ON app_user_role(role_id);

-- A sidebar menu item is visible to a role only if there is a row here for
-- that (menu, role) pair. A menu with no rows is not visible to anyone.
CREATE TABLE menu_role (
    menu_id BIGINT NOT NULL,
    role_id BIGINT NOT NULL,
    CONSTRAINT pk_menu_role PRIMARY KEY (menu_id, role_id),
    CONSTRAINT fk_menu_role_menu FOREIGN KEY (menu_id) REFERENCES menu(menu_id) ON DELETE CASCADE,
    CONSTRAINT fk_menu_role_role FOREIGN KEY (role_id) REFERENCES role(role_id) ON DELETE CASCADE
);

CREATE INDEX idx_menu_role_role ON menu_role(role_id);

CREATE TABLE district_pastor (
    district_pastor_id BIGSERIAL PRIMARY KEY,
    district_id BIGINT NOT NULL,
    pastor_id BIGINT NOT NULL,
    start_date DATE NOT NULL,
    end_date DATE,
    CONSTRAINT fk_district_pastor_district FOREIGN KEY (district_id) REFERENCES district(district_id),
    CONSTRAINT fk_district_pastor_pastor FOREIGN KEY (pastor_id) REFERENCES pastor(pastor_id),
    CONSTRAINT ck_district_pastor_dates CHECK (end_date IS NULL OR end_date >= start_date)
);

CREATE INDEX idx_district_pastor_district ON district_pastor(district_id);
CREATE INDEX idx_district_pastor_pastor ON district_pastor(pastor_id);

-- Only one current (open) pastor assignment per district.
CREATE UNIQUE INDEX uq_district_pastor_current ON district_pastor(district_id) WHERE end_date IS NULL;

-- A pastor cannot lead two districts at the same time.
CREATE UNIQUE INDEX uq_district_pastor_current_pastor ON district_pastor(pastor_id) WHERE end_date IS NULL;

-- Una Filial no existe en el ACMS: forma parte de su Iglesia o Grupo madre
-- (parent_church_id), lleva su mismo código y va dentro de su arqueo. Por eso
-- el código es único solo entre Iglesias y Grupos (uq_church_code, abajo).
CREATE TABLE church (
    church_id BIGSERIAL PRIMARY KEY,
    code VARCHAR(20) NOT NULL,
    name VARCHAR(150) NOT NULL,
    type VARCHAR(30) NOT NULL,
    district_id BIGINT NOT NULL,
    address VARCHAR(255),
    phone VARCHAR(30),
    treasurer_name VARCHAR(150),
    treasurer_phone VARCHAR(30),
    elder_name VARCHAR(150),
    elder_phone VARCHAR(30),
    status VARCHAR(30) NOT NULL,
    merged_into_church_id BIGINT,
    closed_at DATE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    parent_church_id BIGINT,
    CONSTRAINT ck_church_type CHECK (type IN ('Iglesia', 'Grupo', 'Filial')),
    CONSTRAINT ck_church_status CHECK (status IN ('active', 'closed', 'merged')),
    CONSTRAINT ck_church_filial_parent CHECK ((type = 'Filial') = (parent_church_id IS NOT NULL)),
    CONSTRAINT fk_church_district FOREIGN KEY (district_id) REFERENCES district(district_id),
    CONSTRAINT fk_church_merged_into FOREIGN KEY (merged_into_church_id) REFERENCES church(church_id),
    CONSTRAINT fk_church_parent FOREIGN KEY (parent_church_id) REFERENCES church(church_id)
);

CREATE UNIQUE INDEX uq_church_code ON church(code) WHERE type <> 'Filial';
CREATE INDEX idx_church_district ON church(district_id);
CREATE INDEX idx_church_parent ON church(parent_church_id);

CREATE TABLE period (
    period_id BIGSERIAL PRIMARY KEY,
    year INT NOT NULL,
    month INT NOT NULL,
    due_date DATE NOT NULL,
    status VARCHAR(30) NOT NULL,
    closed_at TIMESTAMP,
    closed_by BIGINT,
    CONSTRAINT uq_period_year_month UNIQUE (year, month),
    CONSTRAINT ck_period_month CHECK (month BETWEEN 1 AND 12),
    CONSTRAINT ck_period_status CHECK (status IN ('open', 'closed')),
    CONSTRAINT fk_period_closed_by FOREIGN KEY (closed_by) REFERENCES app_user(app_user_id)
);

-- record_id has no FK on purpose: it points to a row of whichever table is
-- named in table_name.
CREATE TABLE audit_log (
    audit_log_id BIGSERIAL PRIMARY KEY,
    app_user_id BIGINT,
    action VARCHAR(20) NOT NULL,
    module VARCHAR(100),
    table_name VARCHAR(100) NOT NULL,
    record_id BIGINT,
    description VARCHAR(500),
    old_value JSONB,
    new_value JSONB,
    ip VARCHAR(50),
    user_agent VARCHAR(500),
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_audit_log_app_user FOREIGN KEY (app_user_id) REFERENCES app_user(app_user_id)
);

CREATE INDEX idx_audit_log_app_user ON audit_log(app_user_id);
CREATE INDEX idx_audit_log_table_record ON audit_log(table_name, record_id);

-- =====================================================================
-- LEVEL 4: depend on church, period
-- =====================================================================

CREATE TABLE church_history (
    church_history_id BIGSERIAL PRIMARY KEY,
    church_id BIGINT NOT NULL,
    change_type VARCHAR(50) NOT NULL,
    old_value VARCHAR(500),
    new_value VARCHAR(500),
    changed_at DATE NOT NULL,
    reason VARCHAR(500),
    app_user_id BIGINT,
    registered_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_church_history_change_type CHECK (change_type IN ('type', 'district', 'status', 'name')),
    CONSTRAINT fk_church_history_church FOREIGN KEY (church_id) REFERENCES church(church_id),
    CONSTRAINT fk_church_history_app_user FOREIGN KEY (app_user_id) REFERENCES app_user(app_user_id)
);

CREATE INDEX idx_church_history_church ON church_history(church_id);

-- district_id, pastor_id, church_type, treasurer_name and elder_name are a
-- snapshot taken at count time, so the record keeps its meaning even if the
-- church later changes district, pastor or type.
CREATE TABLE local_fund_count (
    local_fund_count_id BIGSERIAL PRIMARY KEY,
    church_id BIGINT NOT NULL,
    period_id BIGINT NOT NULL,
    district_id BIGINT NOT NULL,
    pastor_id BIGINT,
    church_type VARCHAR(30) NOT NULL,
    submission_status VARCHAR(30) NOT NULL,
    acms_balance NUMERIC(14,2) NOT NULL DEFAULT 0,
    -- Arqueo: extracto + depósito + billetes y monedas + dólares en Bs + documentos = total_amount.
    bank_statement_balance NUMERIC(14,2) NOT NULL DEFAULT 0,
    bank_deposit NUMERIC(14,2) NOT NULL DEFAULT 0,
    local_cash NUMERIC(14,2) NOT NULL DEFAULT 0,
    usd_amount NUMERIC(14,2) NOT NULL DEFAULT 0,
    exchange_rate NUMERIC(10,4) NOT NULL DEFAULT 0,
    usd_amount_bs NUMERIC(14,2) NOT NULL DEFAULT 0,
    documents_total NUMERIC(14,2) NOT NULL DEFAULT 0,
    total_amount NUMERIC(14,2) NOT NULL DEFAULT 0,
    difference NUMERIC(14,2) NOT NULL DEFAULT 0,
    difference_status VARCHAR(30),
    count_date DATE,
    treasurer_name VARCHAR(150),
    elder_name VARCHAR(150),
    signed_by_pastor BOOLEAN NOT NULL DEFAULT FALSE,
    signed_by_elder BOOLEAN NOT NULL DEFAULT FALSE,
    observation VARCHAR(1000),
    created_by BIGINT,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_by BIGINT,
    updated_at TIMESTAMP,
    CONSTRAINT uq_local_fund_count_church_period UNIQUE (church_id, period_id),
    CONSTRAINT ck_local_fund_count_church_type CHECK (church_type IN ('Iglesia', 'Grupo', 'Filial')),
    CONSTRAINT ck_local_fund_count_submission_status CHECK (submission_status IN ('submitted', 'not_submitted')),
    CONSTRAINT ck_local_fund_count_difference_status CHECK (difference_status IN ('none', 'pending', 'resolved')),
    CONSTRAINT fk_local_fund_count_church FOREIGN KEY (church_id) REFERENCES church(church_id),
    CONSTRAINT fk_local_fund_count_period FOREIGN KEY (period_id) REFERENCES period(period_id),
    CONSTRAINT fk_local_fund_count_district FOREIGN KEY (district_id) REFERENCES district(district_id),
    CONSTRAINT fk_local_fund_count_pastor FOREIGN KEY (pastor_id) REFERENCES pastor(pastor_id),
    CONSTRAINT fk_local_fund_count_created_by FOREIGN KEY (created_by) REFERENCES app_user(app_user_id),
    CONSTRAINT fk_local_fund_count_updated_by FOREIGN KEY (updated_by) REFERENCES app_user(app_user_id)
);

CREATE INDEX idx_local_fund_count_period ON local_fund_count(period_id);
CREATE INDEX idx_local_fund_count_district ON local_fund_count(district_id);
CREATE INDEX idx_local_fund_count_pastor ON local_fund_count(pastor_id);

-- =====================================================================
-- LEVEL 5: depend on local_fund_count
-- =====================================================================

-- Documentos a rendir del arqueo (facturas, recibos y otros): se suman al total en custodia.
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

CREATE TABLE local_fund_count_attachment (
    local_fund_count_attachment_id BIGSERIAL PRIMARY KEY,
    local_fund_count_id BIGINT NOT NULL,
    file_name VARCHAR(255) NOT NULL,
    file_path VARCHAR(500) NOT NULL,
    mime_type VARCHAR(100) NOT NULL,
    size_bytes BIGINT NOT NULL,
    app_user_id BIGINT,
    registered_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_lfc_attachment_count FOREIGN KEY (local_fund_count_id) REFERENCES local_fund_count(local_fund_count_id) ON DELETE CASCADE,
    CONSTRAINT fk_lfc_attachment_app_user FOREIGN KEY (app_user_id) REFERENCES app_user(app_user_id)
);

CREATE INDEX idx_lfc_attachment_count ON local_fund_count_attachment(local_fund_count_id);

CREATE TABLE local_fund_count_correction (
    local_fund_count_correction_id BIGSERIAL PRIMARY KEY,
    local_fund_count_id BIGINT NOT NULL,
    applied_period_id BIGINT NOT NULL,
    field_name VARCHAR(50) NOT NULL,
    previous_amount NUMERIC(14,2) NOT NULL,
    corrected_amount NUMERIC(14,2) NOT NULL,
    reason VARCHAR(500) NOT NULL,
    app_user_id BIGINT,
    registered_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_lfc_correction_field_name CHECK (field_name IN ('acms_balance', 'total_amount')),
    CONSTRAINT fk_lfc_correction_count FOREIGN KEY (local_fund_count_id) REFERENCES local_fund_count(local_fund_count_id),
    CONSTRAINT fk_lfc_correction_period FOREIGN KEY (applied_period_id) REFERENCES period(period_id),
    CONSTRAINT fk_lfc_correction_app_user FOREIGN KEY (app_user_id) REFERENCES app_user(app_user_id)
);

CREATE INDEX idx_lfc_correction_count ON local_fund_count_correction(local_fund_count_id);
CREATE INDEX idx_lfc_correction_period ON local_fund_count_correction(applied_period_id);

CREATE TABLE difference_follow_up (
    difference_follow_up_id BIGSERIAL PRIMARY KEY,
    local_fund_count_id BIGINT NOT NULL,
    previous_status VARCHAR(30),
    new_status VARCHAR(30) NOT NULL,
    justification VARCHAR(1000) NOT NULL,
    app_user_id BIGINT,
    followed_up_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT ck_difference_follow_up_previous_status CHECK (previous_status IN ('none', 'pending', 'resolved')),
    CONSTRAINT ck_difference_follow_up_new_status CHECK (new_status IN ('none', 'pending', 'resolved')),
    CONSTRAINT fk_difference_follow_up_count FOREIGN KEY (local_fund_count_id) REFERENCES local_fund_count(local_fund_count_id),
    CONSTRAINT fk_difference_follow_up_app_user FOREIGN KEY (app_user_id) REFERENCES app_user(app_user_id)
);

CREATE INDEX idx_difference_follow_up_count ON difference_follow_up(local_fund_count_id);
