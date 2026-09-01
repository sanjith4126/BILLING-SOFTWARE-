# AKIL STORE — Billing Software Guide

Read this once. Keep it near the counter. Anyone should be able to make a bill after reading Part 2.

---

## PART 1 — THE BASICS EVERYONE NEEDS TO KNOW

### Opening the software

1. Double-click the **Billing** shortcut on the desktop.
2. The Login screen appears. Type your **User** name and **PIN** (4 digits).
3. Press Enter or click Login.
4. The Main Menu opens with 20 buttons.

### The two people who use this system

- **Owner** — full access. Can do everything: change prices, cancel bills, enable khata, view reports, close the shift.
- **Cashier / Staff** — can only make bills, look up customers, take payments. Cannot cancel bills or give discounts without the owner's PIN.

### The two things you'll do every day

- **Morning:** Open a shift (put the starting cash into the drawer, tell the software how much).
- **All day:** Make bills at the Billing Counter (button 1).
- **Night:** Close the shift (count the cash in the drawer, print the day report).

That's it. Everything else is occasional.

---

## PART 2 — HOW TO MAKE A BILL (the main job)

### Open the billing screen

1. From the Main Menu, click **1. Billing counter**.
2. First time each day → the software will ask "Open a shift?" → say **Yes** → type how much cash is in the drawer (e.g. `500` for ₹500) → OK.
3. The billing screen opens. The white box at the top-left is the **Scan / Search** box. It should always have a blinking cursor in it.

### Adding items to the bill

Three ways, use whichever is fastest:

- **Scan the barcode** with the scanner → item appears on the bill instantly.
- **Type the barcode digits** and press Enter.
- **Type part of the item name** (e.g. `atta`) and press Enter → pick from the list that pops up.

Every item you add appears in the grid on the left. The total updates on the right.

### Removing an item you added by mistake

- Click on the item row in the grid.
- Press **Del** (or click the **Remove (Del)** button).

### Clearing the whole bill

- Press **Esc** (or click **Clear bill (Esc)**).

### Weight items (cucumber, tomato, atta loose, etc.)

1. Put the item on the weighing scale.
2. Wait until the reading stops changing.
3. In the billing screen, add the item by scanning/searching.
4. Press **F4** (or click **Weigh selected (F4)**).
5. The current weight from the scale locks onto that item.
6. The amount = weight × rate is calculated automatically.

If the scale is broken or not connected, F4 opens a small box asking you to type the weight in grams (minimum 100 g).

### Taking the payment

1. Press **F9** (or click the big green **PAY (F9)** button).
2. The Payment window opens.
3. Enter how much money the customer is paying, and in which mode: **Cash / UPI / Card / Khata**.
4. If they mix (e.g. ₹200 cash + ₹150 UPI), enter both rows.
5. If they give more cash than the total → the change is shown.
6. Click **Confirm** (or press Enter).
7. The bill is saved and printed on the thermal printer.
8. Hand over the receipt.

The screen clears itself. Ready for the next customer.

### Loyalty points (getting the customer's phone number)

**Do this every single bill.** It's how customers earn points and keep coming back.

1. Before pressing F9, press **Ctrl+K** (or click **Customer**).
2. Type the customer's phone number.
3. Enter.
4. First-ever visit: software says "not found — create?" → click **Yes**. Done, no need to ask their name.
5. Any later visit: software finds them and shows "Points: 47" at the top.
6. Continue the bill normally. Points are added automatically.

The receipt will print their phone number and points at the bottom.

**How points work:**
- Earn 1 point for every ₹100 spent.
- 1 point = ₹1 off any future bill.
- Points never expire.

### Customer wants to use their points

1. Attach the customer with Ctrl+K first.
2. Press **F5** (Discount).
3. Type the number of points they want to use (e.g. 50 for ₹50 off).
4. If the discount is above 5% of the bill, the software will ask for the owner's PIN.

### Holding a bill (customer forgot something and ran back)

- Press **F2** to hold the current bill → it moves to the "Held bills" list at the bottom.
- Serve the next customer normally.
- When the first customer comes back, press **F3** → pick their held bill from the list → continue where you left off.

### Cancelling a wrong bill

Only the owner or manager can do this.

1. On the billing screen, click **Cancel a bill (manager PIN)**.
2. Type the bill number (e.g. `24`).
3. Type the reason (e.g. "wrong item").
4. Enter the owner's PIN.
5. The bill is marked as cancelled. Stock goes back to what it was. The bill number is **not** re-used.

---

## PART 3 — END OF DAY (must do every night)

### Closing the shift

1. Count all the cash in the drawer.
2. Main Menu → **8. Shift / day close**.
3. Click **Close current shift**.
4. A denomination grid appears. Type how many of each note/coin you have:
   - ₹2000 × ___
   - ₹500 × ___
   - ₹200 × ___
   - ₹100 × ___
   - ...down to ₹1 coins
5. The software calculates the total.
6. It also shows **Expected cash** = opening float + cash sales - cash refunds.
7. If the two match → click **Close**. Difference is 0.
8. If they don't match → **Difference** is shown as "Short" or "Over". Either way you must close — write down why on paper if you need to.
9. A short summary (Z report) prints.
10. The shift is now locked. You cannot change it later.

Now you can safely close the software and turn off the PC.

### Petty cash (paying small things from the drawer)

If you take money out of the drawer for something (buying tea, paying an autorickshaw), record it:

1. Main Menu → **8. Shift / day close** → **Petty cash entry**.
2. Amount and short note (e.g. `Tea shop 40`).
3. Save.

This way the end-of-day cash count still matches.

---

## PART 4 — OWNER'S JOBS (things staff cannot do)

### Creating staff accounts (cashiers, managers)

**Do this on Day 1**, before the shop opens. Never share the owner PIN with staff. Give each cashier their own account so the audit log can tell who did what.

**To add a new cashier:**

1. Login as owner (user: `owner`, PIN: `1234` on first launch — change it immediately, see below).
2. Main Menu → **Users (staff logins)**.
3. Click **Add new user (owner)**.
4. Type a login name — lowercase, no spaces (e.g. `ramesh`, `priya`, `suresh`). Enter.
5. Type a 4-digit PIN they will use to log in. Enter.
6. Type the role: `cashier` (normal staff), `manager` (can approve discounts/cancellations), or `owner` (full access). For staff, use `cashier`. Enter.
7. Confirmation appears. The user can now log in.

**To change your own PIN (recommended immediately after first login):**

1. Main Menu → **Users (staff logins)**.
2. Click your row in the grid (e.g. `owner`).
3. Click **Change PIN**.
4. Type a new PIN (at least 4 digits). Enter.
5. Next time you log in, use the new PIN.

**To disable a cashier who left the shop:**

1. Main Menu → **Users (staff logins)**.
2. Click their row.
3. Click **Enable / Disable (owner)**.
4. They can no longer log in. Their old bills stay in the system with their name.

**To promote a cashier to manager:**

1. Main Menu → **Users (staff logins)**.
2. Click their row.
3. Click **Change role (owner)**.
4. Type `manager`. Enter.

**Rules:**
- Cashiers can only change their own PIN. They cannot add or disable users.
- Only the owner can add, disable, or change roles.
- You cannot disable yourself (safety).
- User names must be unique. If you try to add `ramesh` twice, the system will say "already exists."

**What the roles can do:**

| Action | Cashier | Manager | Owner |
|---|---|---|---|
| Make bills | Yes | Yes | Yes |
| Take khata payment | Yes | Yes | Yes |
| Cancel a bill | No (needs manager/owner PIN) | Yes | Yes |
| Give discount >5% | No (needs manager/owner PIN) | Yes | Yes |
| Allow khata over limit | No | Yes | Yes |
| Add items | No | Yes | Yes |
| Add stock (purchase entry) | No | Yes | Yes |
| Enable a customer for khata | No | No | Yes |
| Write off a khata debt | No | No | Yes |
| Add/disable users | No | No | Yes |
| View reports | No | Yes | Yes |
| Close the shift | Yes | Yes | Yes |

### Adding a new item to the shop

Main Menu → **4. Item master** → **Add new**.

Fill in:
- **Barcode** (scan it if the item has one, or leave blank for loose items)
- **Name** (full name, e.g. "Aashirvaad Atta 5kg")
- **Print name** (short name that fits on receipt, e.g. "Aashirvaad Atta")
- **Sold by** — Piece / Weight / Volume
- **Unit** — kg / g / l / ml / pc
- **Tax rate** — 0 for now (we're not doing GST yet)
- **Reorder level** — when stock drops to this, it shows in the reorder report
- **Allow discount** — tick this if points can be redeemed on this item

Click **Save**.

### Bulk-loading many items at once (CSV)

For loading 100s or 1000s of items:

1. Prepare a CSV file with this exact header row:
   ```
   sku,name,sold_by,unit,tax_bp,hsn
   ```
   Then one row per item. Example:
   ```
   ATTA001,Aashirvaad Atta 5kg,piece,pc,0,1101
   TOMATO,Tomato loose,weight,g,0,0702
   ```
2. Main Menu → **4. Item master** → **Import CSV**.
3. Pick the file.
4. Preview shows what will be added, and any errors row by row.
5. Click **Import** if it looks right.

### Adding stock (recording a purchase from the supplier)

Every time goods come in from a supplier:

1. Main Menu → **6a. Purchase entry**.
2. Pick the supplier (or add a new one first via Suppliers).
3. Type the invoice number and date.
4. Add a row for each item:
   - Pick the item
   - Batch code (write the supplier's batch or make one up, e.g. `AUG-01`)
   - Expiry date (if the item has one)
   - Quantity, cost per unit, MRP
   - Free quantity (if any — extra units the supplier gave free)
5. Enter freight and any invoice-level discount.
6. Credit terms (if you're paying later) — due date.
7. Save.

The stock automatically increases by the quantities you entered.

### Returning bad goods to the supplier

Main Menu → **6b. Purchase return** → pick supplier and batch, enter quantity and reason (damaged / expired), Save.

### Damage or wastage (throwing something out)

Main Menu → **5c. Damage / wastage** → pick item and batch, quantity, reason (e.g. "spoiled tomatoes"), Save.

Stock reduces. Nothing gets billed.

### Big bag became loose weight (50kg bag opened into loose atta)

Main Menu → **5d. Unit conversion (bag to loose)** → pick the source bag batch → target unit (grams) → confirm.

The bag is removed from stock; equivalent grams are added to the loose stock.

### Physical stock count (once a week/month)

Main Menu → **5b. Stock take** → for each item enter the **counted** quantity → Save.

The system compares against what it thinks is in stock and writes the difference to the ledger. Useful for catching theft/shrinkage.

### What's running low? What's about to expire?

- Main Menu → **5f. Reorder report** — everything below reorder level.
- Main Menu → **5e. Near-expiry report** — everything expiring in the next 30 days.
- Main Menu → **5a. Stock summary** — full picture, filterable.

### Khata / Credit customers

**Setting up a khata customer for the first time:**

1. Make sure they exist as a normal customer first (attach them on any bill with Ctrl+K).
2. Main Menu → **7a. Customer khata (ledger)** → look them up by phone.
3. First-time: click **Edit customer** → add their **Name** (khata customers must have a name — for chasing debts).
4. Click **Adjust limit (owner)** → set their credit limit (e.g. `10000` for ₹10,000).
5. Tick **Credit allowed** → owner PIN → Save.

**When they buy on khata:**
- Cashier does normal billing.
- At F9 payment, choose **Khata** as the mode.
- If their current balance + this bill goes over their limit, the software will ask for owner PIN.
- The receipt prints their previous balance, this bill, and new total outstanding.

**When they come to pay their khata:**
1. Main Menu → **7a. Customer khata (ledger)** → look them up.
2. Click **Record payment**.
3. Amount, mode (cash/UPI/card), reference (UPI transaction ID if applicable).
4. The system automatically clears the oldest bills first (or you can pick which bills).
5. Thermal receipt prints. Give it to the customer.

**Checking who owes what:**
- Main Menu → **7c. Ageing report** — shows every khata customer, how much they owe, how old the debt is (0-30 days / 31-60 / 61-90 / 90+ days). Chase the 90+ ones first.

**Loading old khata notebook into the system (one-time, before go-live):**
- Main Menu → **7b. Opening balance import** — CSV with columns `phone,name,opening_paise,as_of_date`. **Note:** amounts are in paise, not rupees. ₹1,240 = `124000`. Print each customer's statement immediately and have them confirm before you rely on it.

### Reports (looking at your business)

Main Menu → **9. Reports GST** — opens a sub-menu with:

- **Sales register** — every bill in a period
- **Item movement** — what sold, how much
- **Margin** — profit per item
- **Stock valuation** — total value of stock on hand right now
- **Dead stock** — items with no sale in 90 days (get rid of these)
- **Tax by HSN** — for when GST starts
- **Cashier performance** — how many bills / total sales per cashier
- **Collections** — money received against khata, by day/mode/staff
- **Limit overrides** — every time someone allowed a khata beyond the limit
- **Write-offs** — every debt you gave up on

All have a **Date range** picker at the top and an **Export CSV** button (opens in Excel).

**Dashboard** — from the main menu → **9. Reports GST** → **Dashboard** — one-page daily summary: today's sales, bill count, average bill, cash in hand, 7-day sales chart.

### Settings (change store info, PIN, etc.)

Main Menu → **Settings**:

- Store name, phone, footer text
- Printer name (pick your TVS printer from the dropdown)
- Cash drawer on/off
- Scale settings (see next section)
- Discount cap % (default 5%)
- Loyalty points per ₹100 (default 1)

### Scale setup (only if you have the weighing scale connected)

Main Menu → **2. Scale weight setup**:

- **Mode** = Serial (if the scale is plugged in via cable) or Manual (if you'll type weights).
- **Port** = COM1
- **Baud** = 9600, **Data** = 8, **Parity** = None, **Stop** = 1
- **Regex** = `(?<value>\d+\.\d+)` (already set)
- **Live view** shows the raw bytes coming from the scale. Put a 1kg pack on and check the display.
- **Per-item weight settings** tab: for each loose item, set tare (empty container weight), rounding step (5g), minimum sale weight (100g).

If the scale is misbehaving on site, run `ScaleCapture.exe` (in the tools folder) and send the log to Sanjith.

---

## PART 5 — TROUBLESHOOTING

### The bill printer isn't printing

1. Check the printer is on (green light).
2. Check paper roll isn't finished.
3. Main Menu → **Settings** → check **Printer name** is picked correctly from the dropdown.
4. If bills are saving but not printing → check Control Panel → Devices and Printers → is the TVS printer there and set as default?
5. Meanwhile, the software still saves every bill. When the printer works again, use **Reprint** on the billing screen to print the missed ones. Reprints are marked **DUPLICATE COPY** so nobody double-counts.

### The scale isn't reading

1. Is the scale switched on?
2. Is the DB9 cable plugged into the back of the PC?
3. Try Main Menu → **2. Scale weight setup** → check the live view.
4. If empty, quit and reopen the software.
5. If still empty, put a 1kg packet on the scale, run `ScaleCapture.exe` on the desktop, send the file to Sanjith.
6. **Meanwhile** switch to Mode = Manual in scale setup, and the cashier can type weights by hand.

### The barcode scanner isn't working

1. Open Notepad on the desktop.
2. Scan any product.
3. Digits should appear followed by a new line.
4. If yes → scanner is fine. Restart the billing software.
5. If no → check the USB cable, try a different USB port, restart PC.

### The software crashed / "stopped working"

1. Reopen it. Everything is saved. You will not lose any bill.
2. Note the time it crashed.
3. Windows key → type "Event Viewer" → Open.
4. Left side: Windows Logs → Application.
5. Look for the red **Error** with source **.NET Runtime** near the time of the crash.
6. Take a photo of the details. Send to Sanjith.

### "The system's slow / hanging"

- Close and reopen the software.
- If still slow, restart the PC.

### The PC won't start / hard disk failed

This is why you take **backups**. If you have yesterday's `grocery.sqlite` on a USB stick:
- Install the software on any new PC.
- Copy the backup file into `C:\Users\<user>\AppData\Local\GroceryPos\` (create the folder if it doesn't exist), overwriting the fresh one.
- Open the app. All your data is back.

**Take a USB backup at least once a week. Daily is better.**

To back up: close the software, copy `C:\Users\<user>\AppData\Local\GroceryPos\grocery.sqlite` to a pendrive. That one file is your entire business.

---

## PART 6 — QUICK REFERENCE (print this and stick it near the counter)

### Cashier keyboard shortcuts

| Key | What it does |
|---|---|
| Scan / type | Add item to bill |
| Ctrl+K | Attach customer (ask for phone) |
| F4 | Weigh the selected item from scale |
| F5 | Give a discount (>5% needs owner PIN) |
| F2 | Hold current bill |
| F3 | Recall a held bill |
| Del | Remove selected line |
| Esc | Clear the whole bill |
| F9 | Pay (open payment window) |

### Every-day routine

**Morning:** Open software → login → click Billing → enter opening cash → start billing.
**All day:** Ask phone → Ctrl+K → add items → F9 → take payment → hand receipt.
**Evening:** Menu → Shift/day close → count cash → close shift → print Z report → close software.

### Never do this

- Never share the owner PIN with staff. Create separate cashier accounts.
- Never open or edit the `grocery.sqlite` file with any other program.
- Never delete bills. Cancel them instead.
- Never turn off the PC while a bill is being saved. Wait for the "saved" message.

### Menu buttons at a glance

| Button | Who uses it | How often |
|---|---|---|
| 1. Billing counter | Cashier | All day, every day |
| 2. Scale weight setup | Owner | Once at setup, rare after |
| 4. Item master | Owner | Whenever a new product arrives |
| 5a. Stock summary | Owner | Anytime |
| 5b. Stock take | Owner | Weekly/monthly |
| 5c. Damage/wastage | Owner | When something spoils |
| 5d. Unit conversion | Owner | When a bag is opened |
| 5e. Near-expiry | Owner | Weekly |
| 5f. Reorder | Owner | Before going to the wholesale market |
| 6a. Purchase entry | Owner | Every time goods arrive |
| 6b. Purchase return | Owner | When returning bad goods |
| 7a. Customer khata | Owner/Cashier | When customer wants credit or pays their khata |
| 7b. Opening balance import | Owner | Once at go-live only |
| 7c. Ageing report | Owner | Weekly to chase old debts |
| 8. Shift / day close | Cashier + Owner | Every morning (open) and every night (close) |
| 9. Reports GST | Owner | Weekly/monthly review |
| Settings | Owner | Rarely |
| Users (staff logins) | Owner | When adding/removing staff |
| Sign out | Everyone | End of shift |

---

**When in doubt, ask.** Sanjith: `9677131741` (or whichever number). Better to ask than guess.
