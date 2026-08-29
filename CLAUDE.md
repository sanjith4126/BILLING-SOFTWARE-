# Standing instructions — apply to every task in this repository

Read this block before any task. These constraints hold regardless of what is being built.

## Platform

- **Target:** .NET Framework 4.8, C#, WinForms. Runs on **Windows 7 SP1**.
- **Every NuGet package must support `net48`.** Do not use packages that require .NET Standard 2.1+, .NET 5+, or newer. Do not use `System.Text.Json` — use Newtonsoft.Json. Check package compatibility before adding it, not after.
- Do not suggest Electron, .NET 8, WebView2, or any browser-based UI. They do not run on the target machine.

## Data rules — these are not negotiable

- **Money is stored as integer paise.** Never `float`, never `double`. Format only at the display layer.
- **Weight is stored as integer grams.** Never a floating point type.
- **`customer_ledger` is append-only.** No `UPDATE`, no `DELETE`, enforced in the data access layer rather than only in the UI. Corrections are new reversing rows.
- **All stock movement goes through `stock_ledger`.** Never write directly to batch quantities.
- **Every privileged action writes to `audit_log`** with a user id: bill cancellation, discount override, credit limit override, price change, wastage, stock adjustment, shift close, write-off.
- **Bill numbers are sequential, gapless and immutable.** Cancelled bills keep their number.
- Never delete transactional data. Cancel, void or reverse.

## Hardware facts already confirmed — do not re-derive these

- Receipt printer: **TVS RP 3230 over USB**, ESC/POS via `WritePrinter` P/Invoke to `winspool.drv`.
- Receipt width: **48 characters at Font A.** Not 42. Assert this in tests.
- Printer code page: **PC437.** Print `Rs.`, never `₹`. Encode with `Encoding.GetEncoding(437)`, never UTF-8.
- Printer auto line feed is **disabled** — emit an explicit `LF` (0x0A) on every line. Text does not wrap, it truncates.
- Scale: **ES 510 over RS-232**, DB9 into the PC's onboard COM port (likely COM1). Serial frame format not yet captured.
- Scanner: **USB keyboard-wedge.** No integration code needed, only correct focus handling.
- Scanner is **1D only** — no QR code features anywhere.

## How to work with me

- **Explain your approach before writing code** for anything structural: schema, ledger design, batch selection, tax calculation, the receipt formatter.
- **Do not move to the next phase unless I ask.** Build exactly what the current prompt covers and stop.
- Prefer clear, boring code over clever code. This will be maintained by someone else on a slow machine.
- Keep business logic and data access in plain class libraries with no WinForms dependency, so the UI can be replaced later without a rewrite.
- If something in the specification below is ambiguous or looks wrong, say so rather than guessing.

---

# Grocery Billing Software — Build Specification

Hand this document to Claude Code as the project brief. It defines scope, stack, data model, hardware integration and business rules for a single-store retail grocery billing system.

**Status:** client has approved 9 of 10 screens. Returns/exchange/refund is dropped. All four hardware devices are confirmed and their connections verified from photographs (section 4). The only unknown left is the scale's serial frame format, which is a Phase 0 capture task, not a blocker. Messaging reminders are deferred.

---

## 1. What this is

A Windows desktop billing and stock system for a neighbourhood grocery store in India. One or more billing counters, barcode scanning, weight-based selling for loose goods, thermal receipt printing, batch-level stock, supplier purchase entry, customer credit ledger, cash reconciliation and GST reporting.

**Two hard requirements that shape every decision:**

1. **Offline-first.** Billing must never stop because the internet dropped. All reads and writes go to a local database. Cloud sync, if added later, is a background process that can fail silently without blocking the counter.
2. **Keyboard-driven.** A cashier must complete a full bill including payment without touching the mouse. Every billing action needs a function-key shortcut and correct focus management.

---

## 2. Target platform — Windows 7

**The client requires Windows 7.** This is a hard constraint and it eliminates most modern stacks. Read this before choosing anything.

| Constraint | Consequence |
|---|---|
| Windows 7 reached end of life in January 2020 | No security patches. The billing machine should not be exposed to the open internet |
| .NET 5 and later dropped Windows 7 | .NET 8 / .NET 9 are unusable |
| Electron 22 was the last Win7-capable release, EOL October 2023 | Electron is out |
| Modern Chrome, Edge and WebView2 dropped Win7 | Any embedded-browser UI is out |
| TLS 1.2 not enabled by default on older Win7 builds | Must be enabled via registry before any HTTPS call — affects SMS/WhatsApp APIs and future sync |
| SHA-2 code signing update required | Needed before printer and USB-serial drivers will install |

**Confirm with the client:** Windows 7 **SP1**, and 32-bit or 64-bit. SP1 is mandatory for .NET Framework 4.8. Any pre-SP1 machine must be updated first.

## 3. Recommended stack

| Layer | Choice | Why |
|---|---|---|
| Runtime | **.NET Framework 4.8** | Last .NET Framework supporting Win7 SP1, still serviced by Microsoft as a Windows component |
| Language | **C#** | |
| UI | **WinForms** | Dense grids and keyboard shortcuts are its strength, and it renders fast on old hardware. WPF is heavier and buys nothing here |
| Database | **SQLite** via `System.Data.SQLite` (ADO.NET provider) | Local file, transactional, zero administration |
| Data access | **Dapper** | Thin and fast, no EF6 overhead on modest hardware |
| Serial (scale) | `System.IO.Ports.SerialPort` | Built into the framework |
| Printing | Raw bytes via `WritePrinter` P/Invoke to winspool.drv | Sends ESC/POS straight to the device, bypassing the print dialog |
| Installer | Inno Setup or WiX | Must bundle or check for .NET Framework 4.8 |
| Migrations | Plain numbered SQL files applied in order at startup | Schema versioned from day one |

Do not build this as a browser-based web app — it needs local hardware access, and Win7 has no modern browser engine.

**Avoid on this target:** any package requiring .NET Standard 2.1 or later, `System.Text.Json` (use Newtonsoft.Json), and async-heavy libraries built for modern runtimes. Check every NuGet package for `net48` compatibility before adding it.

**Hardware realism:** a store still on Windows 7 likely has 2–4 GB RAM and a mechanical hard disk. Virtualise the item grid, index the SQLite tables properly, and never load the full 1,500-item catalogue into memory on every keystroke.

**Money handling:** store all currency as **integer paise**, never floats. Format at the display layer only.

**Weight handling:** store as **integer grams**, never floats.

---

## 4. Hardware inventory

### Confirmed

All four devices verified against photographs of the actual units on 29 Aug 2026.

| Device | Model | Connection | Integration |
|---|---|---|---|
| Thermal printer | **TVS RP 3230**, 24V 2A, 80mm | **USB type B** | ESC/POS raw bytes via `WritePrinter` |
| Barcode scanner | **BS-C101 STAR** (Taiwan) | **USB** | Keyboard-wedge, no code needed. 1D laser — **cannot read QR codes** |
| Counter scale | **ES 510** (Actway), Class III, Max 30/60 kg, Min 100 g, e = 5 g / 10 g | **RS-232, DB9, into the PC's onboard COM port** | Serial read — **Mode A confirmed** |
| Platform load cell | **CZL-601AC, 200 kg, precision 0.02** | — | Goods-inward weighing, not billing |

**TVS RP 3230 rear panel, confirmed present:** RJ11 cash drawer (marked DK), DC24V power, USB type B, RS232 DB9 male, RJ45 LAN. The printer's own self-test slip confirms all three interfaces, firmware SV1.00.20, 48 columns at Font A, code page PC437, auto-cutter enabled and drawer support present — full settings in section 6.

- The printer is on **USB**, so use the spooler `RAW` path, not `SerialPort`.
- **The cash drawer port exists.** The drawer-kick command will work whenever a drawer is connected. Build the feature and leave it switchable in settings.
- LAN is available as a fallback if USB ever proves unreliable, but there is no reason to use it.

**Scale connection, confirmed:** the scale terminates in a DB9 and plugs into the **PC's native onboard serial port** — not a USB-to-serial adapter. This is significantly better than the alternative: no adapter driver to install, no counterfeit PL2303 risk, no SHA-2 driver signing problem for this device, and the port is stable across reboots. It will almost certainly enumerate as **COM1**. Make it a setting anyway.

**Cable caution:** the DB9 shell in the supplied photograph is a screw-together hood being hand-assembled, so the cable is custom-made rather than a factory lead. **Do not assume the pinout.** See section 5.1.

### What the scale spec forces

- **Minimum weighable quantity is 100 g.** Reject any counter-weighed sale below 100 g with a clear message. Items sold in smaller amounts must be pre-packed and barcoded.
- **Scale division `e` is 5 g up to 30 kg and 10 g above.** Weight must be rounded to the nearest 5 g (or 10 g above 30 kg). Never display or charge a finer figure than the scale can legally produce. Make the rounding step a per-item setting, defaulting to 5 g.
- **Class III, Legal Metrology stamped.** The software must never adjust, correct or calibrate the weight it receives. Whatever the scale reports is the legal figure. Log the raw reading alongside the rounded one.

### Scanner implications

The BS-C101 is 1D only. **Drop all QR code features** — no QR on the printed bill, no "scan the bill QR" lookup. Bill lookup is by typed bill number.

---

## 5. Scale integration — RS-232 confirmed

**The ES 510 outputs RS-232 over DB9 into the PC's onboard COM port.** Mode A is the primary implementation and the one to build first.

Keep the interface below anyway. Mode C (manual) is a required fallback for the day the cable fails or the scale is away for Legal Metrology verification, and Mode B costs almost nothing to leave stubbed if a label scale is ever added at the vegetable counter.

```csharp
public enum WeightMode { Serial, Label, Manual }

public struct WeightReading
{
    public int Grams;          // raw from device, never a float
    public bool Stable;        // device-reported stability flag
    public DateTime At;
}

public class ParsedWeightBarcode
{
    public string ItemCode;
    public int Grams;
}

public interface IWeightSource : IDisposable
{
    WeightMode Mode { get; }

    // Serial mode only. Raised on every reading; UI shows the latest.
    event EventHandler<WeightReading> ReadingReceived;

    // Label mode only. Returns null if the barcode is not a weight barcode.
    ParsedWeightBarcode ParseBarcode(string barcode);
}
```

### 5.1 The cable — verify before writing any parser

The DB9 hood on the scale cable is a hand-assembled screw-together shell, so it is a custom lead, not a factory one. Nothing about its wiring can be assumed.

Only three pins matter for reading a scale:

| DB9 pin | Signal | Direction |
|---|---|---|
| 2 | RXD | PC receives — **this is the one that carries the weight** |
| 3 | TXD | PC sends — only needed if the scale must be polled |
| 5 | GND | Common ground, mandatory |

Two wiring conventions exist and they are not compatible:

- **Straight-through** — scale TX to PC pin 2. Works if the cable was made for a PC.
- **Null-modem / crossover** — pins 2 and 3 swapped. Works if the cable was made for a printer or another DTE device.

If no data appears, **swap pins 2 and 3 before assuming the software is wrong.** This single issue accounts for most "the scale doesn't work" reports. A cheap DB9 null-modem adapter from any computer shop tests the theory in seconds.

Check continuity on pin 5 as well. A missing ground produces intermittent garbage rather than clean silence, which is far more confusing to debug.

### 5.2 Mode A — serial read

Open the configured COM port, read continuously on a background thread, parse the frame, expose the latest stable reading.

**Do not hardcode a frame format.** The ES 510's output format is not yet captured. Build a configurable parser and a raw-data view so the actual frame can be read on site and the parser matched to it.

**Settings required:** port (default COM1), baud, data bits, parity, stop bits, plus a **Test read** panel showing incoming bytes as both hex and ASCII, live.

**Serial parameters to try, in order.** Indian counter scales are almost always one of these:

| Baud | Data | Parity | Stop |
|---|---|---|---|
| 9600 | 8 | None | 1 |
| 4800 | 8 | None | 1 |
| 2400 | 8 | None | 1 |
| 9600 | 7 | Even | 1 |

Build a **Detect** button that cycles these combinations, listens for two seconds each, and reports which one produced printable ASCII. It takes an hour to write and saves a long afternoon on site.

**Two output behaviours exist.** Determine which one this scale uses:

- **Continuous stream** — the scale sends a reading several times a second, unprompted. Just listen.
- **Poll on request** — the scale stays silent until the PC sends a command, commonly `P`, `W`, `ENQ` (0x05) or `CR`. If nothing arrives after opening the port, try sending each of these and watch for a response.

Make the poll command a setting, blank meaning continuous mode.

**Frame parsing.** Typical scale output is one ASCII line per reading, CR or CRLF terminated, often carrying a stability prefix such as `ST` for stable and `US` for unstable, and a weight-type marker such as `GS` for gross or `NT` for net:

```
ST,GS,+  1.240kg

US,GS,+  0.860kg

```

Some units send only the number. Implement the parser as a configurable regex with named groups for `sign`, `value`, `unit` and `status`, defaulting to something permissive that extracts the first signed decimal on the line.

**Rules for the parser:**

- Convert to **integer grams** immediately. Never hold weight as a float.
- Honour the scale's own stability flag. If the frame carries no stability marker, treat a reading as stable only after the same value repeats for roughly 500 ms.
- **Only a stable reading may be committed to a bill line.** The live panel can show unstable values; the Add action must refuse them.
- Discard malformed lines silently rather than throwing. Serial data arrives fragmented and the first partial line after opening a port is normal.
- Log the last 200 raw frames in a ring buffer, dumpable from the settings screen. This is how you diagnose the scale six months later without a site visit.

**Threading:** use `SerialPort.DataReceived`, buffer into a `StringBuilder`, split on the terminator, and marshal completed frames to the UI with `Control.Invoke`. A scale sending 10 readings a second will lock a WinForms UI if handled inline.

**Robustness:** the port must survive the scale being switched off and on. Catch `IOException` and `UnauthorizedAccessException`, close cleanly, and retry the open every few seconds. The billing screen shows a plain *Scale disconnected* indicator and falls back to manual entry rather than blocking the sale.

**Windows 7 note:** an onboard COM port needs no driver, which is one less thing to break on this machine. If the port does not appear, it is disabled in the BIOS rather than missing.

### 5.3 Mode B — weight barcode label (not needed today)

**Not required for this store as configured.** Leave the parser stubbed behind the interface. Build it only if a label-printing scale is later added at the vegetable counter.

Used when a separate label-printing scale sits at the vegetable counter. The sticker is an EAN-13 whose digits carry the item and the weight.

Default format, **configurable**, because scale brands differ:

```
Prefix  Item code  Weight       Check
2       PPPPP      WWWWW        C
digit 1 digits 2-6 digits 7-11  digit 13
```

- Prefix `20`–`29` marks a weight barcode. Any barcode starting in that range routes to the parser, not to the product lookup.
- `WWWWW` is weight in **grams**, so `01240` = 1.240 kg.
- Some scales embed **price** rather than weight in those digits. Make this a setting: `embeds: 'weight' | 'price'`.
- Always validate the EAN-13 check digit before accepting.

### 5.4 Mode C — manual fallback (still required)

The scale has a working data port, but this mode is not optional. Build it for the day the cable fails, the scale is away for Legal Metrology verification, or the port stops responding mid-shift. Billing must never stop because a peripheral died.

The cashier types the weight. This mode must:
- Enforce the 100 g minimum and the 5 g rounding step.
- Flag manually entered weights in the bill line and in the audit log, so the owner can report on them. Manual weight entry is a shrinkage risk and needs visibility.

---

## 6. Thermal printing — TVS RP 3230

Print by writing ESC/POS bytes directly to the device. Do not use `System.Drawing.Printing` — it rasterises the receipt as a graphic, which is slow on Win7-era hardware and produces a worse result than the printer's own text mode.

**How to send raw bytes on .NET Framework:** P/Invoke into `winspool.drv` — `OpenPrinter`, `StartDocPrinter` with datatype `"RAW"`, `StartPagePrinter`, `WritePrinter`, then close in reverse. This is the standard approach and works against the installed printer queue by name.

**Transport is confirmed as USB**, so use the spooler `RAW` path against the installed queue name. The printer also exposes RS232 and LAN, but there is no reason to use either. Wrap the transport behind one `IReceiptPrinter` interface anyway, so switching to LAN later is a new class rather than a rewrite — and note the PC's only onboard COM port is taken by the scale.

**Confirmed from the printer's own self-test slip, 29 Aug 2026:**

| Setting | Value | Consequence |
|---|---|---|
| Firmware | SV1.00.20, CG SV1.00.15 | Record it; behaviour differs between firmware revisions |
| Serial number | WBF75T001727 | |
| Interfaces | USB **and** Serial **and** Ethernet | All three present. Using USB |
| **Characters per line** | **48 at Font A, 64 at Font B** | **Design every layout to 48 columns, not 42** |
| Print resolution | 203 dpi | Standard. Logos must be 1-bit bitmaps at this density |
| Print speed | max 230 mm/s | Fast enough that print time is never the bottleneck |
| **Code page** | **PC437 (USA / Standard Europe)** | **No rupee glyph, no Indian scripts.** See below |
| Auto cutter | Enabled | `GS V` works. Do not emulate a cut with line feeds |
| **Auto line feed** | **Disabled** | Every line must end with an explicit `LF` (0x0A). Nothing wraps for you |
| Drawer | YES | Drawer support present in firmware |
| Beeper / buzzer | Enabled, standard volume | Usable as an error signal |
| Ethernet | DHCP on, no IP assigned, MAC `6C:C1:47:28:21:08` | Not currently on the network. Available as a fallback transport |
| Serial defaults | 115200, 8, N, 1, DTR/DSR | Only relevant if the printer is ever moved off USB |

**Paper:** 80mm, **48 characters per line at Font A** (12×24 dots). Design all receipt layouts to 48 columns. Font B gives 64 columns in a smaller face and is useful for the HSN sub-line under each item.

**Command set needed:**

| Purpose | Bytes |
|---|---|
| Initialise | `ESC @` → `1B 40` |
| Align left / center | `ESC a 0` / `ESC a 1` |
| Bold on / off | `ESC E 1` / `ESC E 0` |
| Double height+width | `GS ! 0x11`, reset with `GS ! 0x00` |
| Feed n lines | `ESC d n` |
| Full cut | `GS V 0` |
| Cash drawer kick | `ESC p 0 25 250` → `1B 70 00 19 FA` |

**Cash drawer — port confirmed present.** The RP 3230 rear panel carries an RJ11 drawer port marked `DK`. A drawer wired into it opens on the kick command sent as part of the print job.

- Fire the kick automatically when a bill is settled with **cash**, and on **credit payment received in cash**. Never on UPI or card.
- Also fire it on shift open and during the day-close count, so staff are not prying the drawer open by hand.
- Make it a setting, defaulting off until a drawer is actually connected, so the command is not sent into an empty port.
- Confirm whether the store owns a cash drawer. If not, the software is ready whenever one is bought — no code change needed.
- Pin 2 and pin 5 wiring differs between drawer makes; if a connected drawer does not open, try the alternate pulse command `ESC p 1 25 250` → `1B 70 01 19 FA`. Make the pin a setting.

**Bill reprint** must print `DUPLICATE COPY` in the header. Log every reprint with user and timestamp.

**Printer failure must not lose the sale.** Save the bill first, then print. If printing fails, the bill stands and a reprint option appears.

**Windows 7 driver note:** install the TVS RP 3230 Win7 driver from TVS Electronics over USB, not a generic one, and never hardcode the queue name — read the installed printer list and make it a setting.

### 6.1 Character set — PC437 confirmed, and what it forbids

The printer reports **Code Page PC437 (USA, Standard Europe)**. This is a hard constraint on everything that gets printed.

- **The rupee sign `₹` does not exist in PC437.** Sending it produces a wrong glyph or a blank. **Print `Rs.` everywhere** — bills, statements, Z reports. This is now confirmed, not a precaution.
- **No Indian scripts.** Tamil, Kannada, Hindi and Devanagari cannot be printed as text. If the store ever wants item names or a header in a local script, the only route is rendering that text to a 1-bit bitmap at 203 dpi and sending it as a raster image with `GS v 0`. Slower, and worth avoiding unless the client specifically asks.
- **Encode strings as CP437 bytes**, not UTF-8. In C#: `Encoding.GetEncoding(437)`. Sending UTF-8 to this printer produces garbage on any non-ASCII character.
- **Sanitise before printing.** Item names entered by staff may contain characters outside CP437 — smart quotes pasted from Excel are the usual culprit. Strip or transliterate them at the print layer rather than letting them reach the printer.
- Other code pages can be selected with `ESC t n`, but there is no page here that covers Indian scripts. Do not go down that path.

### 6.2 Line endings — auto line feed is disabled

The self-test reports **Auto Line Feed: Disable**. Nothing wraps automatically and nothing advances the paper for you. Every line the software emits must terminate with an explicit `LF` (0x0A).

This also means **long text does not wrap — it is truncated.** Item names must be padded or trimmed to fit the 48-column layout in code. This is exactly why the item master carries a separate short print name.

### 6.3 The 48-column layout

Build the receipt as fixed-width text. Columns, totalling 48:

```
Item name (left, truncated)              22
Qty (right)                               9
Rate (right)                              7
Amount (right)                           10
                                     ------
                                         48
```

The HSN code goes on a sub-line under the item name, in Font B if a smaller face is wanted. Example at true width:

```
123456789012345678901234567890123456789012345678
        SRI BALAJI SUPER MARKET
      No. 24, Gandhi Bazaar Main Rd
      Basavanagudi, Bengaluru 560004
         GSTIN: 29ABCDE1234F1Z5
- - - - - - - TAX INVOICE - - - - - - - - - - -
Bill: INV-2451              28/08/26 18:42
Cashier: Ravi                          Ctr 1
------------------------------------------------
Item                       Qty   Rate    Amount
------------------------------------------------
Aashirvaad atta 5kg
  HSN 1101                   1 285.00    285.00
Tomato loose
  HSN 0702             1.240kg  40.00     49.60
------------------------------------------------
Taxable value                             709.70
CGST 9%     14.95      SGST 9%             14.95
Round off                                  +0.50
NET PAYABLE                          Rs. 770.00
------------------------------------------------
```

Write a small formatting helper — `PadRight`, `PadLeft` and a truncate — and unit-test that every emitted line is exactly 48 characters or fewer. A single over-length line wraps badly and ruins the alignment of everything below it on the roll.

Use Font A for the body and reserve **double height and width** (`GS ! 0x11`) for the net payable figure only. On a busy counter the customer looks at exactly one number. The SHA-2 code signing update must be present or the driver install will be refused. Rupee sign support in the printer's character set is unreliable on older firmware; print `Rs.` rather than `₹` unless it is confirmed working on the actual unit.

---

## 7. Data model

Core tables. All monetary columns are integer paise. All weight columns are integer grams. Every table gets `created_at`, `updated_at`, and where relevant `created_by`.

```sql
-- Catalogue
items(id, sku, name, print_name, category_id, brand, rack,
      sold_by CHECK IN ('piece','weight','volume'), unit,      -- 'kg','g','l','ml','pc'
      tax_rate_bp,          -- basis points: 1800 = 18%
      hsn_code,
      reorder_level, max_level, default_supplier_id,
      track_batch BOOL, track_expiry BOOL, allow_discount BOOL,
      weigh_at_counter BOOL,
      tare_grams, round_to_grams DEFAULT 5, min_sale_grams DEFAULT 100,
      is_active BOOL)

item_barcodes(id, item_id, barcode, is_primary)    -- one item, many barcodes

-- Stock is held PER BATCH, never per item
batches(id, item_id, batch_code, expiry_date,
        cost_paise, mrp_paise, selling_paise,
        qty_grams,           -- for weight items
        qty_units,           -- for piece items
        supplier_id, purchase_line_id, received_at)

stock_ledger(id, item_id, batch_id, change_units, change_grams,
             reason CHECK IN ('sale','purchase','return_to_supplier',
                              'damage','wastage','stock_take','conversion'),
             ref_table, ref_id, user_id, at)
-- Every stock movement writes here. Current stock is derivable and auditable.

-- Selling
bills(id, bill_no UNIQUE, counter_id, user_id, customer_id,
      billed_at, status CHECK IN ('completed','cancelled'),
      subtotal_paise, discount_paise, taxable_paise,
      cgst_paise, sgst_paise, round_off_paise, net_paise,
      cancelled_by, cancelled_at, cancel_reason)

bill_lines(id, bill_id, line_no, item_id, batch_id,
           qty_units, qty_grams,
           weight_source CHECK IN ('scale','label','manual','na'),
           raw_grams,                 -- pre-rounding, for audit
           rate_paise, discount_paise, tax_rate_bp, tax_paise, amount_paise,
           hsn_code)

payments(id, bill_id, mode CHECK IN ('cash','upi','card','khata'),
         amount_paise, reference)     -- split payment = multiple rows

-- Buying
suppliers(id, name, phone, gstin, address, payment_terms_days)
purchases(id, supplier_id, invoice_no, invoice_date,
          goods_paise, tax_paise, freight_paise, discount_paise, total_paise,
          payment_mode, due_date, UNIQUE(supplier_id, invoice_no))
purchase_lines(id, purchase_id, item_id, batch_code, expiry_date,
               qty_units, qty_grams, free_units, free_grams,
               cost_paise, mrp_paise, value_paise)

-- Customers and credit
customers(id, phone UNIQUE, name, credit_limit_paise, loyalty_points,
          since, is_active)
customer_ledger(id, customer_id, at, type CHECK IN ('sale','payment','discount','adjustment'),
                ref_table, ref_id, debit_paise, credit_paise, balance_paise, note)

-- Shifts and cash
shifts(id, counter_id, user_id, opened_at, closed_at,
       opening_float_paise, expected_cash_paise, counted_cash_paise,
       difference_paise, status)
cash_counts(id, shift_id, denomination_paise, count)
petty_cash(id, shift_id, amount_paise, note, user_id, at)

-- Admin
users(id, name, pin_hash, role CHECK IN ('owner','manager','cashier'), is_active)
settings(key PRIMARY KEY, value)
audit_log(id, user_id, action, entity, entity_id, before_json, after_json, at)
```

**Batch selection on sale:** default to FIFO by expiry date, nearest expiry first. Where two batches carry different MRPs, the cashier must be able to override and pick the batch, because the customer is holding a specific packet with a specific printed price.

---

## 8. Screens to build

Nine screens. Numbered as the client saw them.

### 1. Billing counter

The primary screen. Layout: scan field on top, item grid on the left, totals and payment panel on the right, held-bills list below the grid.

- Scan field holds focus at all times. It regains focus after every action.
- A barcode starting `20`–`29` routes to the weight parser. Everything else is a product lookup.
- Typing text searches by name and print name.
- Function keys: `F2` hold, `F3` recall, `F4` weigh from scale, `F5` discount, `F9` payment, `Del` remove line, `Esc` clear.
- Live scale reading panel, visible only in serial mode.
- Payment: cash, UPI, card, khata. Split payment across modes. Cash tendered → change calculation.
- Customer lookup by phone. Show name, points, outstanding.
- **Khata block:** if a credit sale would push the customer past their credit limit, stop and require a manager PIN.
- Save the bill, then print. Never the other way round.

**Bill cancellation (kept, despite dropping returns).** The client dropped the returns screen, but cancellation must stay or wrong bills have no exit and the stock and cash figures drift. Implement it minimally:

- Cancel whole bill only. No partial returns, no exchanges, no refund methods.
- Requires manager or owner PIN.
- Sets `status = 'cancelled'`. **Never delete the row and never reuse the bill number** — the invoice series must stay unbroken for GST.
- Reverses the stock ledger entries and any khata ledger entry.
- Writes to `audit_log` with the reason.
- Same-day only by default; older bills need owner role.

### 2. Weight and scale setup

Settings screen. Mode selector (serial / label / manual), serial port configuration with a raw-data test view, weight barcode format configuration, and a loose item pricing table showing rate per unit, tare, minimum sale weight and rounding step per item.

### 3. Bill print format

80mm ESC/POS layout, **48 columns at Font A** (confirmed from the printer self-test). Header with store name, address, GSTIN. `TAX INVOICE` title. Bill number, date/time, cashier. Per line: name, HSN, quantity (weight shown as `1.245kg` for loose), rate, amount. Then taxable value, CGST and SGST shown separately, round off, net payable, payment mode with reference. Footer with return policy. **No QR code** — the scanner cannot read it.

### 4. Item master

Add and edit items. Barcode field accepts a scan. Multiple barcodes per item. Print name separate from full name. Sold-by selector driving unit and weight fields. Purchase, selling and MRP with **live margin display while typing**. Tax rate and HSN. Reorder levels. Batch/expiry/discount flags. Tare, rounding step and minimum sale weight for loose items.

**Bulk import from CSV/Excel is required, not optional.** The store carries roughly 1,500 items and hand entry is not viable. Build a mapping step, a dry-run preview, and a row-level error report.

**MRP guard:** block saving a selling price above MRP.

### 5. Stock and inventory

Summary cards: stock value, active SKUs, count below reorder, count expiring within 30 days. Filterable batch-level table showing item, batch, expiry, MRP, quantity, status. Fractional quantities for loose items. Near-expiry and reorder reports.

Also needed here: **stock take** (counting screen recording counted vs expected, writing the difference to the ledger) and **damage/wastage entry** with a reason. Without wastage the stock figure drifts from reality within a month.

**Unit conversion:** a 50 kg bag received as 1 unit becomes 50,000 g of loose stock. Record the conversion in the stock ledger with reason `conversion`.

### 6. Purchase entry

Supplier, invoice number and date, with duplicate invoice detection per supplier. Line grid with batch, expiry, quantity, **free quantity**, cost, MRP and value. Free quantity adds to stock but not to cost — it changes the effective margin and must be handled correctly. Invoice-level freight and discount. Credit terms with due date, feeding a payables list. Purchase return for damaged or expired goods going back.

Optionally read the CZL-601AC platform scale here for goods-inward weight verification against the supplier's stated quantity.

### 7. Customer khata and loyalty

**Specified in full in section 12 — build from there, not from this summary.**

Lookup by phone. Outstanding, credit limit, available credit, oldest unpaid bill age. Append-only ledger with a running balance where every row names who recorded it and every reference drills into the actual bill and its item lines. Record part payments of any amount, allocated against bills oldest-first. Statement printing on thermal and A4. Ageing view across all customers. Limit override logging.

Messaging reminders are deferred and are not part of this build.

Loyalty: points per rupee spent, redeemable as a discount. Keep it simple enough that a cashier can explain it in one sentence.

### 8. Day close and cash tally

Opening float. Expected cash computed from cash sales minus cash refunds minus petty cash paid out. Denomination-wise count entry. Difference shown as short or over. Non-cash totals for UPI, card and khata listed for matching against provider statements. Z report printed on close. **Once closed, the shift is locked and cannot be edited.**

### 9. Reports, accounts and GST

Dashboard with sales, bill count, average bill, gross margin, cash in hand, and a sales trend chart. Reports: sales register, item movement, margin and profit, stock valuation, dead stock at 90 days, tax summary by HSN, GSTR-1 and GSTR-3B exports, cashier performance.

**Tally export is the accounting strategy.** Do not build ledgers, journals and a balance sheet. The store's accountant works in Tally; export in a format Tally can import.

---

## 9. Business rules

1. **Bill numbers are sequential, gapless and immutable.** Generate inside the same transaction that writes the bill. Cancelled bills keep their number.
2. **Never delete transactional data.** Cancel, void or reverse. Deletion breaks GST and audit.
3. **All stock movement goes through `stock_ledger`.** No direct updates to batch quantities outside a ledger write.
4. **Weight rounding** to the item's `round_to_grams`, default 5 g. Store both raw and rounded.
5. **Minimum counter-weighed sale is 100 g.** Reject below it.
6. **Selling price may not exceed MRP.** Block at item master and at billing.
7. **Discount above the configured limit requires a manager PIN.**
8. **Manual weight entry is flagged** in the line and the audit log.
9. **Tax is computed per line** using the item's rate, then summed. Do not apply an average rate to the bill total.
10. **Round off** to the nearest rupee at bill level, recorded in its own column.
11. **Printing never blocks a sale.** Save first, print second.
12. **Every privileged action writes to `audit_log`** — cancellation, discount override, price change, wastage, stock adjustment, shift close.

---

## 10. Build order

**Phase 0 — prove the hardware, capture the scale frame**
Half a day on the actual store machine. Everything downstream assumes this works.

1. Confirm Windows 7 **SP1**, and 32- or 64-bit. Install .NET Framework 4.8.
2. Install the TVS RP 3230 USB driver. Send raw ESC/POS bytes to the queue and get a receipt out. Test the cut command. Print a 48-character ruler line and confirm it fits the roll exactly. (The printer's self-test is already captured — hold FEED while powering on to reproduce it.)
3. If a cash drawer exists, fire the kick and confirm it opens. Try both pin variants.
4. Open Device Manager and confirm the onboard COM port is present and enabled. If missing, enable it in the BIOS.
5. **Capture the scale output.** Open the port with PuTTY or a small throwaway C# console app. Try 9600 8N1 first, then the other combinations in 5.2. If silent, send `P`, `W`, `ENQ`, `CR` and watch. If still silent, swap DB9 pins 2 and 3 with a null-modem adapter and repeat.
6. **Save a text dump of the raw frames** with a known weight on the pan — put a 1 kg packet on it and record exactly what comes out. That dump is what the parser gets written against, and it means the parser can be finished off site.
7. Plug in the scanner, open Notepad, scan a product and confirm the digits appear followed by Enter.
8. Enable TLS 1.2 in the registry while the machine is open, so it is done.

Write down the answers. Steps 5 and 6 unblock the entire weight feature.

**Phase 1 — foundation**
Schema and migrations. Settings. Users, roles, PIN login. Audit log. Item master with CSV import. Category and supplier masters.

**Phase 2 — the counter**
Billing screen with scanning, cart, totals, tax. ESC/POS printing. Payments including split. Hold and recall. Bill cancellation with PIN.

**Phase 3 — weight**
Scale abstraction with all three drivers. Weight barcode parser with configurable format. Loose item pricing, tare, rounding, minimum weight. Manual entry flagging.

**Phase 4 — stock**
Batch-level stock. Stock ledger wired to sales. Purchase entry with free quantity and bag-to-loose conversion. Wastage, stock take, reorder and near-expiry reports.

**Phase 5 — money and control**
Credit management in full per section 12 — ledger, limits and overrides, payments with allocation, statements, ageing, write-offs, opening balance migration. Loyalty points. Shift open/close, denomination count, Z report.

**Phase 6 — reporting**
Dashboard, all reports, GST summaries, Tally export.

Ship phases 1 to 4 before the store goes live. Phases 5 and 6 can follow while the counter is already running.

---

## 11. Open items

### Resolved 29 Aug 2026

| Question | Answer |
|---|---|
| Does the ES 510 have a data port? | **Yes — RS-232 DB9, into the PC's onboard COM port.** Mode A is primary |
| Printer transport | **USB type B.** RS232 and LAN also present but unused |
| Cash drawer port on the printer | **Present — RJ11 marked DK, and firmware reports `Drawer: YES`.** Drawer kick will work |
| Scanner transport | **USB keyboard-wedge.** No integration code needed |
| Receipt width | **48 columns at Font A**, 64 at Font B, 203 dpi |
| Printer character set | **PC437.** No rupee glyph, no Indian scripts — print `Rs.` |
| Auto cutter / auto line feed | Cutter **enabled**; auto line feed **disabled**, so emit explicit `LF` |

### Still outstanding

| # | Question | Blocks |
|---|---|---|
| 1 | **The scale's serial output format** — baud, and the actual bytes on the wire | Parser configuration. Capture during Phase 0 with the raw-data view; nothing else is blocked |
| 2 | Does the scale stream continuously, or must it be polled with a command? | Whether a poll command is needed |
| 3 | Is the DB9 cable straight-through or null-modem? | Nothing, if pin swapping is tested on site as described in 5.1 |
| 4 | Does the store own a cash drawer, or will one be bought? | Whether to enable the kick by default |
| 5 | Store GSTIN, legal name, address, and the tax slabs in use | Bill header, tax setup |
| 6 | Existing item list — Tally, Excel, or old software export | Bulk import mapping |
| 7 | Number of billing counters running at once | Whether multi-counter sync is needed in phase 1 |
| 8 | Maximum discount a cashier may give without approval | Discount rule |
| 9 | Where does the 200 kg CZL-601AC platform live — goods entrance? | Purchase entry integration |
| 10 | Accountant's software — Tally, Busy, Zoho? | Export format |
| 11 | **Is every machine Windows 7 SP1? 32-bit or 64-bit?** | .NET Framework 4.8 will not install without SP1 |
| 12 | RAM and disk on the billing machine | Performance budget |
| 13 | Will the machine ever be connected to the internet? | Cloud backup, any future messaging |
| 14 | **The existing khata notebook — how many customers, how much outstanding?** | Opening balance migration, which must finish before go-live |

Items 1 to 3 are answered by an hour with the machine, not by asking the client. They are Phase 0 tasks. Nothing in the build waits on them: implement Mode C first so billing is testable end to end, then switch Mode A on once the frame format is captured.

---

## 12. Credit management (khata) — full specification

This replaces the store's paper khata notebook. It is the highest-trust part of the system: it holds money owed to the store by name, and disputes about it happen face to face at the counter. Every figure must be defensible.

**WhatsApp reminders are deferred.** Build the ledger first. The messaging layer is specified separately and can be added later without touching this design.

### 12.1 The governing principle — who, when, what

Every rupee of movement must answer four questions without anyone having to reconstruct it:

| Question | Answered by |
|---|---|
| **Who** owes it, and **who** recorded it | `customer_id` and `user_id` on every row |
| **When** it happened | `at` timestamp, never editable |
| **What** it was for | `ref_table` + `ref_id` pointing to the actual bill, drillable down to the item lines |
| **How much** is left | Running `balance_paise` on every row |

The ledger is **append-only**. Nothing is edited, nothing is deleted. A mistake is corrected by writing a reversing entry, so the trail shows both the error and the correction. This is what makes the balance defensible when a customer says "I already paid that".

### 12.2 Data model

```sql
-- Extend the customer record
customers(
  id, phone UNIQUE, name, address,
  credit_limit_paise,
  credit_allowed BOOL DEFAULT 0,        -- must be explicitly enabled
  opening_balance_paise,                -- carried in from the paper notebook
  opening_balance_at,
  current_balance_paise,                -- cached; ledger remains the truth
  loyalty_points,
  since, last_txn_at,
  notes,
  is_active BOOL,
  created_by, created_at, updated_at
)

-- Append-only. One row per money movement.
customer_ledger(
  id,
  customer_id,
  at                    NOT NULL,       -- when it happened
  type                  CHECK IN ('opening','credit_sale','payment','discount',
                                  'write_off','adjustment','reversal'),
  ref_table,                            -- 'bills', 'credit_payments', 'customer_ledger'
  ref_id,                               -- drill target: the actual bill or payment
  description,                          -- human line: "Bill INV-2451, 5 items"
  debit_paise            DEFAULT 0,     -- increases what the customer owes
  credit_paise           DEFAULT 0,     -- reduces it
  balance_paise          NOT NULL,      -- running balance AFTER this row
  reverses_ledger_id,                   -- set on reversal rows
  user_id                NOT NULL,      -- who recorded it
  counter_id,
  created_at             NOT NULL
)
CREATE INDEX ix_ledger_cust_at ON customer_ledger(customer_id, at, id);

-- Payments received against credit
credit_payments(
  id, customer_id,
  amount_paise,
  mode                  CHECK IN ('cash','upi','card','adjustment'),
  reference,                            -- UPI ref / cheque no
  received_at, received_by,             -- who took the money
  shift_id,                             -- ties collections into the day close
  note,
  is_reversed BOOL DEFAULT 0,
  created_at
)

-- Which bills a payment settled. Drives ageing.
credit_allocations(
  id, credit_payment_id, bill_id,
  allocated_paise,
  created_at
)

-- Every time the limit was overridden at the counter
credit_limit_events(
  id, customer_id,
  event_type            CHECK IN ('limit_set','limit_changed','override_allowed',
                                 'override_refused','credit_enabled','credit_disabled'),
  old_limit_paise, new_limit_paise,
  bill_id,                              -- the bill that triggered an override
  attempted_paise, balance_at_time_paise,
  reason,
  authorised_by,                        -- the manager/owner whose PIN was used
  requested_by,                         -- the cashier who asked
  at
)
```

**`bills` already carries `customer_id`.** Add `is_credit_sale BOOL` so credit sales are filterable without joining payments.

### 12.3 Balance integrity

- The **ledger is the single source of truth**. `customers.current_balance_paise` is a cache for fast list rendering.
- Write the ledger row and update the cached balance **in the same SQLite transaction**. Never one without the other.
- `balance_paise` on each row is computed as the previous row's balance plus debit minus credit, inside that transaction, so the running balance is stored rather than recalculated at read time.
- Ship a **reconciliation check** in settings: recompute every customer's balance from the ledger and report any mismatch against the cache. Run it on startup weekly and after any crash recovery. A silent drift here destroys trust in the whole system.
- Balances may be negative (customer overpaid or holds a credit). Handle it, display it as *advance* rather than a minus sign, and let it offset the next sale.

### 12.4 Opening balances — migrating the notebook

The store already runs a khata book with real outstanding money. This is a one-time task that must happen before go-live, and it is usually underestimated.

- Provide a dedicated **opening balance import**: customer name, phone, opening amount, as-of date. CSV plus a manual entry screen.
- Each import writes a `type = 'opening'` ledger row with `user_id` set to whoever did the migration, and the description `Opening balance carried from khata book as on <date>`.
- **Print a statement for every customer immediately after import** and have the owner confirm each figure before go-live. Do not discover a disputed opening balance three months later.
- Lock opening balance entry behind the owner role, and disallow it entirely once the customer has any later transaction.

### 12.5 Credit sale at the counter

1. Cashier looks up the customer by phone on the billing screen.
2. If `credit_allowed = 0`, the khata payment button is disabled with a reason shown.
3. On selecting Khata, compute `current_balance + bill_total`.
4. If that exceeds `credit_limit_paise`, **stop**. Show current balance, limit, bill amount and the shortfall. Require a manager or owner PIN to proceed.
5. Whether allowed or refused, write a `credit_limit_events` row recording who asked, who authorised or refused, the amounts, and the reason.
6. On save, in one transaction: write the bill, write `payments` with mode `khata`, write a `credit_sale` ledger row referencing the bill, update the cached balance.
7. Print the bill with **the running balance on it**: `Previous balance`, `This bill`, `Total outstanding`. Customers dispute far less when the figure is printed in their hand each visit.

Partial credit is normal — a customer pays ₹500 cash and puts ₹270 on khata. The split payment already supports this; only the khata portion enters the ledger.

### 12.6 Receiving a payment

- Any amount, at any time, not just full settlement.
- Capture mode (cash, UPI, card), reference, and **who received it**. Link to the open `shift_id` so credit collections appear in the day close and in the cash tally.
- **Allocate against bills oldest-first (FIFO)** by default, writing `credit_allocations` rows. Allow the staff to override and allocate to a specific bill if the customer says "this is for last Tuesday's". Allocation is what makes the ageing report meaningful — without it, every ageing bucket is a guess.
- Print a **payment receipt** with the amount received, the new balance, the date and the name of the person who took the money. This matters: it protects both the customer and the staff member.
- Reversing a wrong payment writes a `reversal` ledger row and sets `is_reversed`, never a delete.

### 12.7 The customer ledger screen

The core "who, when, what" view. Header shows outstanding, credit limit, available credit, oldest unpaid bill age, last payment date, customer since, total lifetime business.

Ledger table, newest at the bottom, with a running balance:

| Date | Type | Reference | Description | Debit | Credit | Balance | By |
|---|---|---|---|---|---|---|---|
| 12 Aug 26 | Opening | — | Carried from khata book | 1,240.00 | — | 1,240.00 | Owner |
| 19 Aug 26 | Sale | INV-2318 | 14 items | 2,770.00 | — | 4,010.00 | Ravi |
| 22 Aug 26 | Payment | PAY-0142 | Cash, against INV-2201 | — | 1,500.00 | 2,510.00 | Suresh |
| 28 Aug 26 | Sale | INV-2451 | 5 items | 770.00 | — | 3,280.00 | Ravi |

Required behaviours:

- **Every reference is clickable.** Clicking a bill opens the full bill with its item lines — this is the "what" half of the requirement. A customer asking "what was that ₹2,770 for?" gets an answer in two clicks, not a hunt through a notebook.
- **The By column is always populated** and never blank. If a row cannot name a user, the design is wrong.
- Date range filter, and a type filter for showing only payments.
- Hover or a detail pane showing the exact timestamp, counter and, for overridden sales, who authorised the override.
- Actions: Record payment, Print statement, Adjust limit, Disable credit. No edit, no delete anywhere on this screen.

### 12.8 Customer list and ageing

The owner's collection screen. One row per customer with credit, sorted by outstanding descending by default.

Columns: name, phone, outstanding, credit limit, **days since oldest unpaid bill**, last payment date, last purchase date, status.

Ageing buckets computed from `credit_allocations`, so an unallocated amount is never silently treated as current:

| Bucket | Meaning |
|---|---|
| Current | 0–30 days |
| 31–60 | Getting slow |
| 61–90 | Needs chasing |
| Over 90 | Likely bad debt |

Show the totals per bucket at the top. Colour the over-90 rows. Filters for over-limit customers, dormant customers with a balance, and customers with no purchase in 60 days but money owed — the last group is where losses actually live.

### 12.9 Statements

Two formats, same data:

- **Thermal 80mm** for handing over at the counter. Header, period, opening balance, each line, closing balance, and a line for who printed it and when.
- **A4 / PDF** for a formal statement, for a customer who disputes a figure or a business account that wants records.

Both must print the store name, the customer name and phone, the period covered, and a generated-on timestamp. Log every statement print in `audit_log` — knowing a statement was issued on a given date settles arguments.

### 12.10 Write-offs and adjustments

Real stores eventually accept that some money will not come back.

- **Write-off** requires the owner role, a mandatory reason, and writes a `write_off` ledger row. It never deletes the history — the debt stays visible with a write-off entry closing it.
- **Adjustment** covers goodwill reductions and correcting a genuine error. Owner role, mandatory reason, always a new row.
- Written-off amounts appear in a separate report and in the margin report as a cost, so the owner sees the true profit impact rather than losing it in a general adjustments bucket.
- A written-off customer should be flagged so the counter warns before extending credit again.

### 12.11 Business rules

1. Credit is **off by default** on a new customer. It must be explicitly enabled by owner or manager.
2. A customer cannot be given credit without a phone number and a name.
3. The ledger is append-only. No UPDATE, no DELETE, ever. Enforce it in the data layer, not just the UI.
4. Every ledger row carries a `user_id`. No system-generated rows without an operator.
5. Limit breaches always require a PIN and are always logged, allowed or refused.
6. Payments are allocated to bills; unallocated payments are not permitted to sit in limbo.
7. Cached balances are recomputed and verified against the ledger on a schedule.
8. Credit collections belong to a shift and appear in the day close.
9. Deactivating a customer with a non-zero balance is blocked. Settle or write off first.
10. Only the owner may change a credit limit, write off, or enter an opening balance.

### 12.12 Reports

- **Outstanding summary** — total owed, customer count, average balance, oldest debt.
- **Ageing analysis** — the four buckets, per customer and in total.
- **Collections report** — money received in a period, by day, by mode, and **by staff member who received it**.
- **Credit sales by cashier** — who is extending the most credit. Worth watching.
- **Limit override log** — every breach, who asked, who approved. This is the fraud control.
- **Dormant debtors** — money owed by customers who have stopped shopping.
- **Write-off report** — what was written off, when, by whom, for what reason.

### 12.13 Where this sits in the build

Phase 5, alongside the day close, since credit collections feed the cash tally. But build the **customer record and the ledger tables in Phase 1**, because bills reference customers and retrofitting a customer link onto existing bill data is avoidable pain.

The opening balance migration must be scheduled explicitly with the client before go-live. It needs the owner sitting down with the notebook, and it usually takes longer than anyone plans for.

---

## 13. A note to raise with the client about Windows 7

Windows 7 has had no security updates since January 2020. For a machine that will hold sales records, customer phone numbers and credit balances, and that may connect to the internet for UPI reconciliation or backup, this is a real risk worth stating plainly and in writing.

It does not block the build. The stack above works on Win7 and .NET Framework 4.8 remains serviced. But two things follow:

1. **Keep the billing machine off the open internet** where possible. Backups to a USB drive or a local network share rather than a cloud service.
2. **Design so a later move to Windows 10 or 11 is cheap.** Keep all business logic and data access in plain C# class libraries with no UI dependency. If the UI is ever rebuilt, the logic layer moves across unchanged. Do not scatter SQL and business rules through WinForms event handlers.

If the client is open to it, a modern low-cost machine removes the constraint entirely and widens the stack options considerably. Worth asking once, then building for Win7 either way.
