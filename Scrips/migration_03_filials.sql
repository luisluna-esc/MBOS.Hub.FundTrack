-- =====================================================================
-- MIGRATION 03: filiales y pastores sin CI (2026-10-05).
-- Already folded into dataBase.sql for fresh installs; run this only on a
-- database created before this date.
--
-- En el ACMS solo existen las Iglesias y los Grupos (325 códigos). Una
-- Filial no tiene saldo ACMS propio: forma parte de su Iglesia o Grupo
-- madre, lleva su mismo código y va dentro del arqueo de la madre.
-- El código sigue siendo único entre Iglesias y Grupos.
--
-- Los pastores se cargan sin CI (no venía en la lista oficial); el CI se
-- completa después y sigue siendo único cuando se llena.
-- =====================================================================

ALTER TABLE church ADD COLUMN parent_church_id BIGINT;

ALTER TABLE church
    ADD CONSTRAINT fk_church_parent FOREIGN KEY (parent_church_id) REFERENCES church(church_id),
    ADD CONSTRAINT ck_church_filial_parent CHECK ((type = 'Filial') = (parent_church_id IS NOT NULL));

ALTER TABLE church DROP CONSTRAINT uq_church_code;
CREATE UNIQUE INDEX uq_church_code ON church(code) WHERE type <> 'Filial';
CREATE INDEX idx_church_parent ON church(parent_church_id);

ALTER TABLE pastor ALTER COLUMN ci DROP NOT NULL;
