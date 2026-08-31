-- Make customers.name optional (loyalty-only customers can be phone-only).
-- Credit enablement still requires a name; that rule lives in the repository.
-- SQLite cannot ALTER a column's NOT NULL flag, so recreate the table.

CREATE TABLE customers_new (
  id                      INTEGER PRIMARY KEY AUTOINCREMENT,
  phone                   TEXT UNIQUE,
  name                    TEXT,
  address                 TEXT,
  credit_limit_paise      INTEGER NOT NULL DEFAULT 0,
  credit_allowed          INTEGER NOT NULL DEFAULT 0,
  opening_balance_paise   INTEGER NOT NULL DEFAULT 0,
  opening_balance_at      TEXT,
  current_balance_paise   INTEGER NOT NULL DEFAULT 0,
  loyalty_points          INTEGER NOT NULL DEFAULT 0,
  since                   TEXT NOT NULL DEFAULT (datetime('now')),
  last_txn_at             TEXT,
  notes                   TEXT,
  is_active               INTEGER NOT NULL DEFAULT 1,
  created_by              INTEGER REFERENCES users(id),
  created_at              TEXT NOT NULL DEFAULT (datetime('now')),
  updated_at              TEXT NOT NULL DEFAULT (datetime('now'))
);

INSERT INTO customers_new
SELECT id, phone, name, address, credit_limit_paise, credit_allowed,
       opening_balance_paise, opening_balance_at, current_balance_paise,
       loyalty_points, since, last_txn_at, notes, is_active,
       created_by, created_at, updated_at
FROM customers;

DROP TABLE customers;
ALTER TABLE customers_new RENAME TO customers;

-- Shop defaults for AKIL STORE. Overwrite the placeholder from 001.
UPDATE settings SET value = 'AKIL STORE' WHERE key = 'store_name';

-- New settings for phone, footer, and non-GST receipt title.
INSERT OR IGNORE INTO settings(key, value) VALUES ('store_phone', '9698776767');
INSERT OR IGNORE INTO settings(key, value) VALUES ('store_footer', 'Thank you, Visit Again!!!');
INSERT OR IGNORE INTO settings(key, value) VALUES ('receipt_title_no_gst', 'CASH BILL');

-- ES 510 confirmed frame format on-site: continuous stream, 9600-8-N-1, "NNN.NNN\r\n" in kg.
-- Overwrite the permissive default regex with one that matches the actual frame.
UPDATE settings SET value = '(?<value>\d+\.\d+)' WHERE key = 'scale_regex';
