-- Remove the dot-notation settings keys that duplicated seeded underscore ones.
--
-- Customer statements, payment receipts and the Z report read "printer.queue",
-- "store.name" and "store.address", while the billing screen and the seeded
-- defaults used "printer_name", "store_name" and "store_address_1". Setting the
-- store up through Settings therefore made bills print correctly while
-- statements printed the store name as "STORE" and went to no printer at all.
--
-- The reading code now uses the underscore spelling everywhere. This migration
-- carries across any value an existing install had saved under the dotted key
-- before removing it, so nothing already configured is lost.

UPDATE settings
   SET value = (SELECT value FROM settings WHERE key = 'printer.queue')
 WHERE key = 'printer_name'
   AND (value IS NULL OR value = '')
   AND EXISTS (SELECT 1 FROM settings WHERE key = 'printer.queue' AND value <> '');

UPDATE settings
   SET value = (SELECT value FROM settings WHERE key = 'store.name')
 WHERE key = 'store_name'
   AND (value IS NULL OR value = '')
   AND EXISTS (SELECT 1 FROM settings WHERE key = 'store.name' AND value <> '');

UPDATE settings
   SET value = (SELECT value FROM settings WHERE key = 'store.address')
 WHERE key = 'store_address_1'
   AND (value IS NULL OR value = '')
   AND EXISTS (SELECT 1 FROM settings WHERE key = 'store.address' AND value <> '');

DELETE FROM settings WHERE key IN ('printer.queue', 'store.name', 'store.address');
