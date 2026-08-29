-- Initial schema. Money is integer paise. Weight is integer grams.
-- Every table gets created_at/updated_at where relevant.

CREATE TABLE users (
  id           INTEGER PRIMARY KEY AUTOINCREMENT,
  name         TEXT NOT NULL,
  pin_hash     TEXT NOT NULL,
  role         TEXT NOT NULL CHECK(role IN ('owner','manager','cashier')),
  is_active    INTEGER NOT NULL DEFAULT 1,
  created_at   TEXT NOT NULL DEFAULT (datetime('now')),
  updated_at   TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE settings (
  key    TEXT PRIMARY KEY,
  value  TEXT NOT NULL
);

CREATE TABLE categories (
  id    INTEGER PRIMARY KEY AUTOINCREMENT,
  name  TEXT NOT NULL UNIQUE
);

CREATE TABLE suppliers (
  id                   INTEGER PRIMARY KEY AUTOINCREMENT,
  name                 TEXT NOT NULL,
  phone                TEXT,
  gstin                TEXT,
  address              TEXT,
  payment_terms_days   INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE items (
  id                  INTEGER PRIMARY KEY AUTOINCREMENT,
  sku                 TEXT UNIQUE,
  name                TEXT NOT NULL,
  print_name          TEXT NOT NULL,
  category_id         INTEGER REFERENCES categories(id),
  brand               TEXT,
  rack                TEXT,
  sold_by             TEXT NOT NULL CHECK(sold_by IN ('piece','weight','volume')),
  unit                TEXT NOT NULL,
  tax_rate_bp         INTEGER NOT NULL DEFAULT 0,
  hsn_code            TEXT,
  reorder_level       INTEGER NOT NULL DEFAULT 0,
  max_level           INTEGER NOT NULL DEFAULT 0,
  default_supplier_id INTEGER REFERENCES suppliers(id),
  track_batch         INTEGER NOT NULL DEFAULT 0,
  track_expiry        INTEGER NOT NULL DEFAULT 0,
  allow_discount      INTEGER NOT NULL DEFAULT 1,
  weigh_at_counter    INTEGER NOT NULL DEFAULT 0,
  tare_grams          INTEGER NOT NULL DEFAULT 0,
  round_to_grams      INTEGER NOT NULL DEFAULT 5,
  min_sale_grams      INTEGER NOT NULL DEFAULT 100,
  is_active           INTEGER NOT NULL DEFAULT 1,
  created_at          TEXT NOT NULL DEFAULT (datetime('now')),
  updated_at          TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE INDEX ix_items_name ON items(name);
CREATE INDEX ix_items_print_name ON items(print_name);

CREATE TABLE item_barcodes (
  id         INTEGER PRIMARY KEY AUTOINCREMENT,
  item_id    INTEGER NOT NULL REFERENCES items(id),
  barcode    TEXT NOT NULL UNIQUE,
  is_primary INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX ix_barcodes_item ON item_barcodes(item_id);

CREATE TABLE batches (
  id                INTEGER PRIMARY KEY AUTOINCREMENT,
  item_id           INTEGER NOT NULL REFERENCES items(id),
  batch_code        TEXT,
  expiry_date       TEXT,
  cost_paise        INTEGER NOT NULL DEFAULT 0,
  mrp_paise         INTEGER NOT NULL DEFAULT 0,
  selling_paise     INTEGER NOT NULL DEFAULT 0,
  qty_grams         INTEGER NOT NULL DEFAULT 0,
  qty_units         INTEGER NOT NULL DEFAULT 0,
  supplier_id       INTEGER REFERENCES suppliers(id),
  purchase_line_id  INTEGER,
  received_at       TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE INDEX ix_batches_item ON batches(item_id);
CREATE INDEX ix_batches_expiry ON batches(expiry_date);

CREATE TABLE stock_ledger (
  id             INTEGER PRIMARY KEY AUTOINCREMENT,
  item_id        INTEGER NOT NULL REFERENCES items(id),
  batch_id       INTEGER REFERENCES batches(id),
  change_units   INTEGER NOT NULL DEFAULT 0,
  change_grams   INTEGER NOT NULL DEFAULT 0,
  reason         TEXT NOT NULL CHECK(reason IN
                    ('sale','purchase','return_to_supplier','damage','wastage','stock_take','conversion')),
  ref_table      TEXT,
  ref_id         INTEGER,
  user_id        INTEGER NOT NULL REFERENCES users(id),
  at             TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE INDEX ix_stock_ledger_item ON stock_ledger(item_id, at);

-- stock_ledger is append-only
CREATE TRIGGER stock_ledger_no_update
BEFORE UPDATE ON stock_ledger
BEGIN
  SELECT RAISE(ABORT, 'stock_ledger is append-only');
END;
CREATE TRIGGER stock_ledger_no_delete
BEFORE DELETE ON stock_ledger
BEGIN
  SELECT RAISE(ABORT, 'stock_ledger is append-only');
END;

CREATE TABLE customers (
  id                      INTEGER PRIMARY KEY AUTOINCREMENT,
  phone                   TEXT UNIQUE,
  name                    TEXT NOT NULL,
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

CREATE TABLE bills (
  id               INTEGER PRIMARY KEY AUTOINCREMENT,
  bill_no          INTEGER NOT NULL UNIQUE,
  counter_id       INTEGER NOT NULL DEFAULT 1,
  user_id          INTEGER NOT NULL REFERENCES users(id),
  customer_id      INTEGER REFERENCES customers(id),
  billed_at        TEXT NOT NULL DEFAULT (datetime('now')),
  status           TEXT NOT NULL CHECK(status IN ('completed','cancelled')) DEFAULT 'completed',
  subtotal_paise   INTEGER NOT NULL DEFAULT 0,
  discount_paise   INTEGER NOT NULL DEFAULT 0,
  taxable_paise    INTEGER NOT NULL DEFAULT 0,
  cgst_paise       INTEGER NOT NULL DEFAULT 0,
  sgst_paise       INTEGER NOT NULL DEFAULT 0,
  round_off_paise  INTEGER NOT NULL DEFAULT 0,
  net_paise        INTEGER NOT NULL DEFAULT 0,
  is_credit_sale   INTEGER NOT NULL DEFAULT 0,
  cancelled_by     INTEGER REFERENCES users(id),
  cancelled_at     TEXT,
  cancel_reason    TEXT
);

CREATE TRIGGER bills_no_delete
BEFORE DELETE ON bills
BEGIN
  SELECT RAISE(ABORT, 'bills cannot be deleted; cancel instead');
END;

CREATE TABLE bill_lines (
  id              INTEGER PRIMARY KEY AUTOINCREMENT,
  bill_id         INTEGER NOT NULL REFERENCES bills(id),
  line_no         INTEGER NOT NULL,
  item_id         INTEGER NOT NULL REFERENCES items(id),
  batch_id        INTEGER REFERENCES batches(id),
  qty_units       INTEGER NOT NULL DEFAULT 0,
  qty_grams       INTEGER NOT NULL DEFAULT 0,
  weight_source   TEXT NOT NULL CHECK(weight_source IN ('scale','label','manual','na')) DEFAULT 'na',
  raw_grams       INTEGER NOT NULL DEFAULT 0,
  rate_paise      INTEGER NOT NULL DEFAULT 0,
  discount_paise  INTEGER NOT NULL DEFAULT 0,
  tax_rate_bp     INTEGER NOT NULL DEFAULT 0,
  tax_paise       INTEGER NOT NULL DEFAULT 0,
  amount_paise    INTEGER NOT NULL DEFAULT 0,
  hsn_code        TEXT
);
CREATE INDEX ix_bill_lines_bill ON bill_lines(bill_id);

CREATE TABLE payments (
  id            INTEGER PRIMARY KEY AUTOINCREMENT,
  bill_id       INTEGER NOT NULL REFERENCES bills(id),
  mode          TEXT NOT NULL CHECK(mode IN ('cash','upi','card','khata')),
  amount_paise  INTEGER NOT NULL,
  reference     TEXT
);
CREATE INDEX ix_payments_bill ON payments(bill_id);

CREATE TABLE customer_ledger (
  id                  INTEGER PRIMARY KEY AUTOINCREMENT,
  customer_id         INTEGER NOT NULL REFERENCES customers(id),
  at                  TEXT NOT NULL,
  type                TEXT NOT NULL CHECK(type IN
                        ('opening','credit_sale','payment','discount','write_off','adjustment','reversal')),
  ref_table           TEXT,
  ref_id              INTEGER,
  description         TEXT,
  debit_paise         INTEGER NOT NULL DEFAULT 0,
  credit_paise        INTEGER NOT NULL DEFAULT 0,
  balance_paise       INTEGER NOT NULL,
  reverses_ledger_id  INTEGER REFERENCES customer_ledger(id),
  user_id             INTEGER NOT NULL REFERENCES users(id),
  counter_id          INTEGER,
  created_at          TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE INDEX ix_ledger_cust_at ON customer_ledger(customer_id, at, id);

-- customer_ledger is append-only
CREATE TRIGGER customer_ledger_no_update
BEFORE UPDATE ON customer_ledger
BEGIN
  SELECT RAISE(ABORT, 'customer_ledger is append-only');
END;
CREATE TRIGGER customer_ledger_no_delete
BEFORE DELETE ON customer_ledger
BEGIN
  SELECT RAISE(ABORT, 'customer_ledger is append-only');
END;

CREATE TABLE credit_payments (
  id            INTEGER PRIMARY KEY AUTOINCREMENT,
  customer_id   INTEGER NOT NULL REFERENCES customers(id),
  amount_paise  INTEGER NOT NULL,
  mode          TEXT NOT NULL CHECK(mode IN ('cash','upi','card','adjustment')),
  reference     TEXT,
  received_at   TEXT NOT NULL,
  received_by   INTEGER NOT NULL REFERENCES users(id),
  shift_id      INTEGER,
  note          TEXT,
  is_reversed   INTEGER NOT NULL DEFAULT 0,
  created_at    TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE credit_allocations (
  id                 INTEGER PRIMARY KEY AUTOINCREMENT,
  credit_payment_id  INTEGER NOT NULL REFERENCES credit_payments(id),
  bill_id            INTEGER NOT NULL REFERENCES bills(id),
  allocated_paise    INTEGER NOT NULL,
  created_at         TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE INDEX ix_alloc_bill ON credit_allocations(bill_id);

CREATE TABLE credit_limit_events (
  id                       INTEGER PRIMARY KEY AUTOINCREMENT,
  customer_id              INTEGER NOT NULL REFERENCES customers(id),
  event_type               TEXT NOT NULL CHECK(event_type IN
                             ('limit_set','limit_changed','override_allowed',
                              'override_refused','credit_enabled','credit_disabled')),
  old_limit_paise          INTEGER,
  new_limit_paise          INTEGER,
  bill_id                  INTEGER REFERENCES bills(id),
  attempted_paise          INTEGER,
  balance_at_time_paise    INTEGER,
  reason                   TEXT,
  authorised_by            INTEGER REFERENCES users(id),
  requested_by             INTEGER REFERENCES users(id),
  at                       TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE purchases (
  id             INTEGER PRIMARY KEY AUTOINCREMENT,
  supplier_id    INTEGER NOT NULL REFERENCES suppliers(id),
  invoice_no     TEXT NOT NULL,
  invoice_date   TEXT NOT NULL,
  goods_paise    INTEGER NOT NULL DEFAULT 0,
  tax_paise      INTEGER NOT NULL DEFAULT 0,
  freight_paise  INTEGER NOT NULL DEFAULT 0,
  discount_paise INTEGER NOT NULL DEFAULT 0,
  total_paise    INTEGER NOT NULL DEFAULT 0,
  payment_mode   TEXT,
  due_date       TEXT,
  created_at     TEXT NOT NULL DEFAULT (datetime('now')),
  UNIQUE(supplier_id, invoice_no)
);

CREATE TABLE purchase_lines (
  id            INTEGER PRIMARY KEY AUTOINCREMENT,
  purchase_id   INTEGER NOT NULL REFERENCES purchases(id),
  item_id       INTEGER NOT NULL REFERENCES items(id),
  batch_code    TEXT,
  expiry_date   TEXT,
  qty_units     INTEGER NOT NULL DEFAULT 0,
  qty_grams     INTEGER NOT NULL DEFAULT 0,
  free_units    INTEGER NOT NULL DEFAULT 0,
  free_grams    INTEGER NOT NULL DEFAULT 0,
  cost_paise    INTEGER NOT NULL DEFAULT 0,
  mrp_paise     INTEGER NOT NULL DEFAULT 0,
  value_paise   INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE shifts (
  id                     INTEGER PRIMARY KEY AUTOINCREMENT,
  counter_id             INTEGER NOT NULL,
  user_id                INTEGER NOT NULL REFERENCES users(id),
  opened_at              TEXT NOT NULL,
  closed_at              TEXT,
  opening_float_paise    INTEGER NOT NULL DEFAULT 0,
  expected_cash_paise    INTEGER NOT NULL DEFAULT 0,
  counted_cash_paise     INTEGER NOT NULL DEFAULT 0,
  difference_paise       INTEGER NOT NULL DEFAULT 0,
  status                 TEXT NOT NULL CHECK(status IN ('open','closed')) DEFAULT 'open'
);

CREATE TABLE cash_counts (
  id                  INTEGER PRIMARY KEY AUTOINCREMENT,
  shift_id            INTEGER NOT NULL REFERENCES shifts(id),
  denomination_paise  INTEGER NOT NULL,
  count               INTEGER NOT NULL
);

CREATE TABLE petty_cash (
  id            INTEGER PRIMARY KEY AUTOINCREMENT,
  shift_id      INTEGER NOT NULL REFERENCES shifts(id),
  amount_paise  INTEGER NOT NULL,
  note          TEXT,
  user_id       INTEGER NOT NULL REFERENCES users(id),
  at            TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE audit_log (
  id           INTEGER PRIMARY KEY AUTOINCREMENT,
  user_id      INTEGER NOT NULL REFERENCES users(id),
  action       TEXT NOT NULL,
  entity       TEXT NOT NULL,
  entity_id    INTEGER,
  before_json  TEXT,
  after_json   TEXT,
  at           TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Bill number sequence, gapless. Held in a settings row and issued inside a transaction.
INSERT INTO settings(key, value) VALUES ('next_bill_no', '1');
INSERT INTO settings(key, value) VALUES ('store_name', 'GROCERY STORE');
INSERT INTO settings(key, value) VALUES ('store_address_1', '');
INSERT INTO settings(key, value) VALUES ('store_address_2', '');
INSERT INTO settings(key, value) VALUES ('store_gstin', '');
INSERT INTO settings(key, value) VALUES ('counter_id', '1');
INSERT INTO settings(key, value) VALUES ('printer_name', '');
INSERT INTO settings(key, value) VALUES ('drawer_enabled', '0');
INSERT INTO settings(key, value) VALUES ('drawer_pin', '0');
INSERT INTO settings(key, value) VALUES ('scale_port', 'COM1');
INSERT INTO settings(key, value) VALUES ('scale_baud', '9600');
INSERT INTO settings(key, value) VALUES ('scale_databits', '8');
INSERT INTO settings(key, value) VALUES ('scale_parity', 'None');
INSERT INTO settings(key, value) VALUES ('scale_stopbits', '1');
INSERT INTO settings(key, value) VALUES ('scale_regex', '(?<sign>[+-]?)\s*(?<value>\d+(?:\.\d+)?)\s*(?<unit>kg|g)?');
INSERT INTO settings(key, value) VALUES ('scale_poll', '');
INSERT INTO settings(key, value) VALUES ('discount_cap_percent', '5');
INSERT INTO settings(key, value) VALUES ('loyalty_points_per_100rupees', '1');

-- schema_migrations is created by the migrator itself.
