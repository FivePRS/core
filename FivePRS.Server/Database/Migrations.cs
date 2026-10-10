using System.Collections.Generic;

namespace FivePRS.Server.Database
{
    public static class Migrations
    {
        public static IReadOnlyList<Migration> All { get; } = new[]
        {
            new Migration(1, "Initial schema",
                Sql(@"CREATE TABLE IF NOT EXISTS ers_players (
                        license     TEXT    NOT NULL PRIMARY KEY,
                        name        TEXT    NOT NULL,
                        department  INTEGER DEFAULT 0,
                        is_on_duty  INTEGER DEFAULT 0,
                        xp          INTEGER DEFAULT 0,
                        rank_level  INTEGER DEFAULT 1,
                        agency      TEXT    NOT NULL DEFAULT '',
                        callsign    TEXT    NOT NULL DEFAULT '',
                        last_seen   TEXT    DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now')))",
                    @"CREATE TABLE IF NOT EXISTS `ers_players` (
                        `license`     VARCHAR(60)  NOT NULL,
                        `name`        VARCHAR(100) NOT NULL,
                        `department`  TINYINT UNSIGNED DEFAULT 0,
                        `is_on_duty`  TINYINT(1)   DEFAULT 0,
                        `xp`          INT UNSIGNED  DEFAULT 0,
                        `rank_level`  TINYINT UNSIGNED DEFAULT 1,
                        `agency`      VARCHAR(40)  NOT NULL DEFAULT '',
                        `callsign`    VARCHAR(16)  NOT NULL DEFAULT '',
                        `last_seen`   DATETIME     DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                        PRIMARY KEY (`license`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci"),
                new AddColumnStep("ers_players", "agency", "TEXT NOT NULL DEFAULT ''", "VARCHAR(40) NOT NULL DEFAULT ''"),
                new AddColumnStep("ers_players", "callsign", "TEXT NOT NULL DEFAULT ''", "VARCHAR(16) NOT NULL DEFAULT ''"),
                Sql(@"CREATE TABLE IF NOT EXISTS fiveprs_audit (
                        id             INTEGER PRIMARY KEY AUTOINCREMENT,
                        created_at     TEXT    NOT NULL,
                        action         TEXT    NOT NULL,
                        actor_license  TEXT,
                        actor_name     TEXT    NOT NULL,
                        target_license TEXT,
                        details        TEXT    NOT NULL);
                    CREATE INDEX IF NOT EXISTS idx_fiveprs_audit_target ON fiveprs_audit(target_license)",
                    @"CREATE TABLE IF NOT EXISTS `fiveprs_audit` (
                        `id`             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
                        `created_at`     DATETIME     NOT NULL,
                        `action`         VARCHAR(40)  NOT NULL,
                        `actor_license`  VARCHAR(60)  NULL,
                        `actor_name`     VARCHAR(100) NOT NULL,
                        `target_license` VARCHAR(60)  NULL,
                        `details`        VARCHAR(255) NOT NULL,
                        PRIMARY KEY (`id`),
                        KEY `idx_fiveprs_audit_target` (`target_license`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci"),
                Sql(@"CREATE TABLE IF NOT EXISTS fiveprs_characters (
                        id            INTEGER PRIMARY KEY AUTOINCREMENT,
                        owner_license TEXT    NOT NULL,
                        first_name    TEXT    NOT NULL,
                        last_name     TEXT    NOT NULL,
                        date_of_birth TEXT    NOT NULL,
                        gender        TEXT    NOT NULL,
                        created_at    TEXT    NOT NULL,
                        last_used_at  TEXT    NOT NULL);
                    CREATE INDEX IF NOT EXISTS idx_fiveprs_characters_owner ON fiveprs_characters(owner_license);
                    CREATE INDEX IF NOT EXISTS idx_fiveprs_characters_name ON fiveprs_characters(last_name, first_name)",
                    @"CREATE TABLE IF NOT EXISTS `fiveprs_characters` (
                        `id`            INT UNSIGNED NOT NULL AUTO_INCREMENT,
                        `owner_license` VARCHAR(60)  NOT NULL,
                        `first_name`    VARCHAR(32)  NOT NULL,
                        `last_name`     VARCHAR(32)  NOT NULL,
                        `date_of_birth` CHAR(10)     NOT NULL,
                        `gender`        VARCHAR(16)  NOT NULL,
                        `created_at`    DATETIME(6)  NOT NULL,
                        `last_used_at`  DATETIME(6)  NOT NULL,
                        PRIMARY KEY (`id`),
                        KEY `idx_fiveprs_characters_owner` (`owner_license`),
                        KEY `idx_fiveprs_characters_name` (`last_name`, `first_name`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci"),
                Sql(@"CREATE TABLE IF NOT EXISTS fiveprs_licenses (
                        character_id INTEGER NOT NULL,
                        type         TEXT    NOT NULL,
                        status       INTEGER NOT NULL DEFAULT 0,
                        issued_at    TEXT    NOT NULL,
                        PRIMARY KEY (character_id, type))",
                    @"CREATE TABLE IF NOT EXISTS `fiveprs_licenses` (
                        `character_id` INT UNSIGNED NOT NULL,
                        `type`         VARCHAR(32)  NOT NULL,
                        `status`       TINYINT UNSIGNED NOT NULL DEFAULT 0,
                        `issued_at`    DATETIME(6)  NOT NULL,
                        PRIMARY KEY (`character_id`, `type`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci"),
                Sql(@"CREATE TABLE IF NOT EXISTS fiveprs_vehicles (
                        id            INTEGER PRIMARY KEY AUTOINCREMENT,
                        character_id  INTEGER NOT NULL,
                        plate         TEXT    NOT NULL UNIQUE,
                        model         TEXT    NOT NULL,
                        status        INTEGER NOT NULL DEFAULT 0,
                        registered_at TEXT    NOT NULL);
                    CREATE INDEX IF NOT EXISTS idx_fiveprs_vehicles_character ON fiveprs_vehicles(character_id)",
                    @"CREATE TABLE IF NOT EXISTS `fiveprs_vehicles` (
                        `id`            INT UNSIGNED NOT NULL AUTO_INCREMENT,
                        `character_id`  INT UNSIGNED NOT NULL,
                        `plate`         VARCHAR(8)   NOT NULL,
                        `model`         VARCHAR(40)  NOT NULL,
                        `status`        TINYINT UNSIGNED NOT NULL DEFAULT 0,
                        `registered_at` DATETIME(6)  NOT NULL,
                        PRIMARY KEY (`id`),
                        UNIQUE KEY `uq_fiveprs_vehicles_plate` (`plate`),
                        KEY `idx_fiveprs_vehicles_character` (`character_id`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci"),
                Sql(@"CREATE TABLE IF NOT EXISTS fiveprs_records (
                        id               INTEGER PRIMARY KEY AUTOINCREMENT,
                        character_id     INTEGER NOT NULL,
                        type             INTEGER NOT NULL,
                        description      TEXT    NOT NULL,
                        fine             INTEGER NOT NULL DEFAULT 0,
                        officer_license  TEXT    NOT NULL,
                        officer_name     TEXT    NOT NULL,
                        officer_callsign TEXT    NOT NULL,
                        created_at       TEXT    NOT NULL,
                        active           INTEGER NOT NULL DEFAULT 0,
                        resolution       TEXT    NULL);
                    CREATE INDEX IF NOT EXISTS idx_fiveprs_records_character ON fiveprs_records(character_id)",
                    @"CREATE TABLE IF NOT EXISTS `fiveprs_records` (
                        `id`               INT UNSIGNED NOT NULL AUTO_INCREMENT,
                        `character_id`     INT UNSIGNED NOT NULL,
                        `type`             TINYINT UNSIGNED NOT NULL,
                        `description`      VARCHAR(500) NOT NULL,
                        `fine`             INT UNSIGNED NOT NULL DEFAULT 0,
                        `officer_license`  VARCHAR(60)  NOT NULL,
                        `officer_name`     VARCHAR(100) NOT NULL,
                        `officer_callsign` VARCHAR(16)  NOT NULL,
                        `created_at`       DATETIME(6)  NOT NULL,
                        `active`           TINYINT(1)   NOT NULL DEFAULT 0,
                        `resolution`       VARCHAR(120) NULL,
                        PRIMARY KEY (`id`),
                        KEY `idx_fiveprs_records_character` (`character_id`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci"),
                Sql(@"CREATE TABLE IF NOT EXISTS fiveprs_roster (
                        license    TEXT    NOT NULL,
                        department INTEGER NOT NULL,
                        granted_by TEXT    NOT NULL,
                        granted_at TEXT    NOT NULL,
                        PRIMARY KEY (license, department))",
                    @"CREATE TABLE IF NOT EXISTS `fiveprs_roster` (
                        `license`    VARCHAR(60)  NOT NULL,
                        `department` TINYINT UNSIGNED NOT NULL,
                        `granted_by` VARCHAR(100) NOT NULL,
                        `granted_at` DATETIME(6)  NOT NULL,
                        PRIMARY KEY (`license`, `department`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci"),
                Sql(@"CREATE TABLE IF NOT EXISTS fiveprs_appearances (
                        character_id INTEGER PRIMARY KEY,
                        data         TEXT    NOT NULL,
                        updated_at   TEXT    NOT NULL)",
                    @"CREATE TABLE IF NOT EXISTS `fiveprs_appearances` (
                        `character_id` INT UNSIGNED NOT NULL,
                        `data`         MEDIUMTEXT   NOT NULL,
                        `updated_at`   DATETIME(6)  NOT NULL,
                        PRIMARY KEY (`character_id`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci"),
                Sql(@"CREATE TABLE IF NOT EXISTS fiveprs_preferences (
                        license    TEXT PRIMARY KEY,
                        wallpaper  TEXT NOT NULL,
                        updated_at TEXT NOT NULL)",
                    @"CREATE TABLE IF NOT EXISTS `fiveprs_preferences` (
                        `license`    VARCHAR(60)  NOT NULL,
                        `wallpaper`  VARCHAR(600) NOT NULL,
                        `updated_at` DATETIME(6)  NOT NULL,
                        PRIMARY KEY (`license`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci"),
                Sql(@"CREATE TABLE IF NOT EXISTS fiveprs_positions (
                        license    TEXT PRIMARY KEY,
                        x          REAL NOT NULL,
                        y          REAL NOT NULL,
                        z          REAL NOT NULL,
                        heading    REAL NOT NULL,
                        updated_at TEXT NOT NULL)",
                    @"CREATE TABLE IF NOT EXISTS `fiveprs_positions` (
                        `license`    VARCHAR(60) NOT NULL,
                        `x`          DOUBLE      NOT NULL,
                        `y`          DOUBLE      NOT NULL,
                        `z`          DOUBLE      NOT NULL,
                        `heading`    DOUBLE      NOT NULL,
                        `updated_at` DATETIME(6) NOT NULL,
                        PRIMARY KEY (`license`)
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci")),
        };

        private static SqlStep Sql(string sqlite, string mySql) => new SqlStep(sqlite, mySql);
    }
}
