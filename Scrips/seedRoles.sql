-- =====================================================================
-- Seed for roles. FundTrack has no permission catalog: what each role can
-- reach is controlled by menu visibility (menu_role) and, on the backend,
-- by [Authorize(Roles = ...)] with the groups in RoleNames.cs:
--   M-BOS and Analista write; everyone else only reads.
--   Pastor only sees his own district.
-- =====================================================================

INSERT INTO role (name, description, is_active) VALUES
('M-BOS', 'Superusuario: además administra usuarios, roles y menús', TRUE),
('Analista', 'Registra y modifica catálogos, arqueos y cierres; ve la auditoría', TRUE),
('Administrador', 'Visualiza y saca reportes para dar informe; ve la auditoría', TRUE),
('Tesorero', 'Visualiza e informa lo que generan los analistas', TRUE),
('Auditor', 'Solo visualiza', TRUE),
('Pastor', 'Solo visualiza los datos de su distrito', TRUE)
ON CONFLICT (name) DO NOTHING;
