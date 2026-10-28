-- Runs once when the PostgreSQL container is first created (as the superuser).
-- The application connects as a NON-superuser role so Row-Level Security is enforced.
CREATE ROLE restaurant_app LOGIN PASSWORD 'restaurant_app' NOSUPERUSER NOBYPASSRLS;
ALTER DATABASE restaurant_ai OWNER TO restaurant_app;
GRANT ALL ON SCHEMA public TO restaurant_app;

\connect restaurant_ai
-- Extensions need elevated privileges, so they are created here rather than by migrations.
CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS btree_gist;
