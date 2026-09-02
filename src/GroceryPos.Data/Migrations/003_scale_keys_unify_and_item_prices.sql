-- Unify scale settings under dot-notation keys (matches ScaleSetupForm) and
-- seed the ES 510 frame regex that we captured on-site.
-- Also add default price columns to items so Item Master can carry a
-- selling price and MRP without requiring a purchase entry first.

-- 1) Scale keys — copy underscore values into dot-keys if not present already.
INSERT OR IGNORE INTO settings(key, value)
  SELECT 'scale.mode', 'Manual';
INSERT OR IGNORE INTO settings(key, value)
  SELECT 'scale.port', COALESCE((SELECT value FROM settings WHERE key='scale_port'), 'COM1');
INSERT OR IGNORE INTO settings(key, value)
  SELECT 'scale.baud', COALESCE((SELECT value FROM settings WHERE key='scale_baud'), '9600');
INSERT OR IGNORE INTO settings(key, value)
  SELECT 'scale.data_bits', COALESCE((SELECT value FROM settings WHERE key='scale_databits'), '8');
INSERT OR IGNORE INTO settings(key, value)
  SELECT 'scale.parity', COALESCE((SELECT value FROM settings WHERE key='scale_parity'), 'None');
INSERT OR IGNORE INTO settings(key, value)
  SELECT 'scale.stop_bits', COALESCE((SELECT value FROM settings WHERE key='scale_stopbits'), '1');
INSERT OR IGNORE INTO settings(key, value)
  SELECT 'scale.regex', '(?<value>\d+\.\d+)';
INSERT OR IGNORE INTO settings(key, value)
  SELECT 'scale.poll_cmd', COALESCE((SELECT value FROM settings WHERE key='scale_poll'), '');

-- Always overwrite the regex with the ES 510 frame format (NNN.NNN\r\n).
UPDATE settings SET value = '(?<value>\d+\.\d+)' WHERE key = 'scale.regex';

-- 2) Item default prices: cost, selling, MRP.
-- SQLite: ALTER TABLE ADD COLUMN is safe on existing tables.
ALTER TABLE items ADD COLUMN default_cost_paise    INTEGER NOT NULL DEFAULT 0;
ALTER TABLE items ADD COLUMN default_selling_paise INTEGER NOT NULL DEFAULT 0;
ALTER TABLE items ADD COLUMN default_mrp_paise     INTEGER NOT NULL DEFAULT 0;
