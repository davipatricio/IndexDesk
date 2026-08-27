-- Módulo Auth — preferências do usuário (privacy toggle "Esconder dados" do dashboard).
-- Coluna users.preferences (jsonb, default '{}') — espelha o mapeamento EF de
-- IndexDeskDbContext (UserEntity.Preferences) e MODELS.md §users.
--   docker exec -i indexdesk-timescaledb psql -U indexdesk -d indexdesk < docker/init-db/07-user-preferences.sql
-- Idempotente: seguro re-executar.

alter table users add column if not exists "Preferences" jsonb not null default '{}';
