-- Helper script to add missing ApplicationSettings columns to SQLite DB.
-- Run this using the sqlite3 client or through your DB management tooling.
-- It will attempt to add the columns required by the newer code paths.

BEGIN TRANSACTION;

ALTER TABLE "ApplicationSettings" ADD COLUMN "DownloadCompletionStabilitySeconds" INTEGER NOT NULL DEFAULT 10;
ALTER TABLE "ApplicationSettings" ADD COLUMN "MissingSourceRetryInitialDelaySeconds" INTEGER NOT NULL DEFAULT 30;
ALTER TABLE "ApplicationSettings" ADD COLUMN "MissingSourceMaxRetries" INTEGER NOT NULL DEFAULT 3;

COMMIT;

-- Note: SQLite does not support conditional column creation. If your database already
-- has these columns (added previously by a different method), running this script will
-- fail. In that case, don't run it or remove the already-present columns first.
