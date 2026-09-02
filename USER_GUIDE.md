# AKIL STORE — Billing Software Guide

Read Part 1 and Part 2 once. That is enough to run the counter all day.
The rest is for the owner, and you can look it up when you need it.

Keep this book near the billing machine.

---

## CONTENTS

| Part | What it covers | Who needs it |
|---|---|---|
| 1 | Starting the software, signing in | Everyone |
| 2 | Making a bill — the main job | Cashier |
| 3 | Weighing loose goods | Cashier |
| 4 | Taking payment, khata, holding bills | Cashier |
| 5 | Closing the day | Cashier or owner |
| 6 | First-time setup — do this once | Owner |
| 7 | Connecting the weighing scale | Owner |
| 8 | Adding products | Owner |
| 9 | Buying stock from suppliers | Owner |
| 10 | Counting stock, wastage, sacks | Owner |
| 11 | Customer khata (credit) | Owner |
| 12 | Reports and GST | Owner |
| 13 | When something goes wrong | Everyone |

---

# PART 1 — STARTING THE SOFTWARE

### Turning it on

Double-click the **Billing** shortcut on the desktop.

If there is no shortcut, the program is at:
`C:\dev\grocery-pos\publish\GroceryPos.App.exe`

Right-click that file → **Send to** → **Desktop (create shortcut)** so you only
have to do this once.

### Signing in

The sign-in window asks two things:

1. **Who is signing in** — pick your name from the drop-down list.
2. **PIN** — type your 4-digit number and press **Enter**.

The first time ever, the only user is **owner** with PIN **1234**.
Change that PIN on day one. See Part 6.

### Who can do what

| | Owner | Manager | Cashier |
|---|---|---|---|
| Make bills, take payment | Yes | Yes | Yes |
| Look up customers | Yes | Yes | Yes |
| Cancel a bill | Yes | Yes | Needs a manager PIN |
| Give a big discount | Yes | Yes | Needs a manager PIN |
| Change prices, add products | Yes | Yes | No |
| Write off stock | Yes | Yes | No |
| Change credit limits, write off debt | Yes | No | No |
| Add or remove staff | Yes | Yes | No |

### The main menu

After signing in you see coloured tiles grouped under six headings:

- **Billing** — Billing counter, Shift and day close
- **Stock** — Stock summary, Stock take, Damage and wastage, Open a sack,
  Expiring soon, Items to reorder
- **Buying** — Purchase entry, Return to supplier
- **Customers and credit** — Customer khata, Money owed by age, Opening balances
- **Products and reports** — Item master, Reports and GST
- **Setup** — Scale and weight, Settings, Staff accounts

Click a tile to open that screen. Close the screen to come back to the menu.

### The shape of a normal day

| When | What |
|---|---|
| Morning | Open a shift — tell the software how much cash is in the drawer |
| All day | Make bills at the Billing counter |
| Night | Close the shift — count the cash, print the day report |

Everything else is occasional.

---

# PART 2 — MAKING A BILL

This is the job you will do a hundred times a day. Learn this part properly.

### Opening the counter

1. From the main menu click **Billing counter**.
2. The first time each day it asks **"Open a shift?"** → click **Yes** →
   type how much cash is in the drawer to start with (for example `500`) → OK.
3. The billing screen opens.

The blinking cursor sits in the **Scan barcode or type an item name** box at
the top left. It should stay there all day. If you ever lose it, click that
box once.

### Adding items — three ways

- **Scan the barcode.** The item appears on the bill straight away.
- **Type the barcode digits** and press **Enter**.
- **Type part of the name** — for example `atta` — and press **Enter**.
  A list pops up; pick the right one.

Each item appears as a line in the table on the left. The totals on the right
update as you go.

### Fixing mistakes

| To do this | Press |
|---|---|
| Remove the line you clicked on | **Del** |
| Throw away the whole bill and start again | **Esc** |

### The keys you actually need

These are printed along the bottom of the billing screen too, so you do not
have to remember them.

| Key | What it does |
|---|---|
| **F2** | Hold this bill and start a new one |
| **F3** | Bring a held bill back |
| **F4** | Weigh the selected line on the scale |
| **F5** | Give a discount on the selected line |
| **F9** | Take payment and finish the bill |
| **Del** | Remove the selected line |
| **Esc** | Clear the whole bill |
| **Ctrl + K** | Look up a customer by phone number |

A full bill can be done without touching the mouse.

### Finishing the bill

Press **F9**. See Part 4 for what happens next.

---

# PART 3 — WEIGHING LOOSE GOODS

For anything sold by weight — tomato, sugar, dal, loose atta.

### With the scale connected

1. Put the goods on the scale pan.
2. **Wait for the number on the scale to stop moving.** This matters — the
   software will refuse a reading that is still wobbling.
3. In the billing screen, add the item by scanning or searching.
4. Press **F4**.
5. The weight is filled in and the amount is worked out.

### If the scale is not working

You can still sell. Press **F4** and type the weight in grams by hand.

A weight typed by hand is **marked in the bill and in the records**, so the
owner can see later how often it happened. That is deliberate — hand-typed
weights are how money goes missing.

### Two rules the software enforces

- **Minimum 100 g.** Anything lighter is refused. The scale itself is not
  accurate below that, and the law does not allow it. Sell small quantities
  as pre-packed items with their own barcode.
- **Rounded to 5 g.** The scale can only legally report to the nearest 5 g,
  so the software never charges for a finer amount than that.

### Weight is never adjusted

Whatever the scale reports is the legal figure. The software records the raw
reading alongside the rounded one and never corrects it.

---

# PART 4 — PAYMENT, KHATA AND HELD BILLS

### Taking payment

Press **F9**. The payment window opens showing what is owed.

| Paid by | What to do |
|---|---|
| **Cash** | Type what the customer handed over. The change is worked out for you. |
| **UPI** | Enter the amount and the UPI reference number. |
| **Card** | Enter the amount and the reference. |
| **Khata** | Puts the amount on the customer's account. See below. |

**Part cash, part khata** is normal — a customer pays Rs. 500 now and puts
Rs. 270 on the account. Enter both lines. Only the khata part goes on their
account.

Click **Save**. The bill is saved first, then printed.

### Important: the bill is saved before it prints

If the printer jams, runs out of paper or is switched off, **the sale is still
recorded**. You will see "Print failed (bill saved)". Fix the printer and
reprint. You never lose a sale because of the printer.

### Selling on khata (credit)

1. Before pressing F9, press **Ctrl + K** and find the customer by phone number.
2. Their name, balance and available credit appear at the top.
3. Press **F9**, choose **Khata**, save.

If the sale would push them past their credit limit, the software **stops** and
shows their balance, their limit and the shortfall. A manager or owner PIN is
needed to go ahead. Every one of these — allowed or refused — is recorded.

Credit is **off** for a new customer. The owner must switch it on.

### Holding a bill

A customer goes back for something they forgot, and there is a queue behind them.

- **F2** — the bill is put aside and you get a fresh one.
- **F3** — bring it back when they return.

Held bills are listed along the bottom of the screen.

### Cancelling a bill

Click **Cancel a past bill** on the right. A manager or owner PIN is required.

Cancelling:
- puts the stock back on the shelf
- reverses any khata entry
- **keeps the bill number** — it is marked cancelled, never deleted

The bill number series must stay unbroken for GST. Nothing is ever deleted.

---

# PART 5 — CLOSING THE DAY

From the main menu click **Shift and day close**.

1. The screen lists every note and coin.
2. Count the drawer and type **how many** of each you have.
3. As you type, the software shows:
   - **Expected in the drawer** — what it should be
   - **You counted** — what you actually have
   - whether you are **SHORT** or **OVER**, in red
4. Also shown: UPI, card and khata totals, to check against your phone or
   card machine.
5. Click **Close the shift and print the Z report**.
6. Confirm. The Z report prints.

**Once closed, a shift cannot be changed.** Count carefully before confirming.

---

# PART 6 — FIRST-TIME SETUP (owner, do this once)

Do these in order before the first real sale.

### 1. Change the owner PIN

Main menu → **Staff accounts** → select `owner` → **Change PIN**.
Do not leave it as 1234.

### 2. Add your staff

Same screen → **Add new user**. Give each person their own name and PIN.
Choose the right role — a cashier cannot cancel bills or change prices.

Everyone must have their own login. Every bill, every discount and every
cancellation is recorded against a person's name, which only works if they
are not sharing one login.

### 3. Fill in your shop details

Main menu → **Settings**.

| Field | What it is |
|---|---|
| Shop name | Printed at the top of every bill |
| Address line 1 and 2 | Printed under the name |
| Phone number | Printed on the bill |
| GSTIN | Leave blank if you are not GST registered |
| Bill title when no GSTIN | Usually `CASH BILL` |
| Footer line | For example "Thank you, visit again" |

The GSTIN box tells you as you type whether bills will be titled
**TAX INVOICE** or **CASH BILL**.

### 4. Set up the printer

Still in **Settings**, under **Receipt printer**:

1. Open the drop-down. It lists the printers installed in Windows.
2. Choose the **TVS RP 3230**.
3. Click **Print a test slip**.

The test slip prints a row of digits. **Check that the row fits the paper
exactly and does not wrap onto a second line.** If it fits, your printer is
set up correctly.

Leave the box blank to turn printing off.

### 5. Cash drawer

Tick **A cash drawer is connected** only if one is actually plugged into the
printer. If you tick it and the drawer does not open, change **Drawer pin**
from 2 to 5 and try again — drawer makers wire this differently.

The drawer opens by itself on a cash sale, on a cash khata payment, and at
day close. It never opens on UPI or card.

### 6. Counter rules

| Setting | What it means |
|---|---|
| Counter number | Leave at 1 unless you have more than one billing machine |
| Discount without approval (%) | Above this a cashier must get a manager PIN |
| Loyalty points per Rs. 100 | Set to 0 to turn loyalty off |

Click **Save settings**.

### 7. Connect the scale

See Part 7 — it has its own section.

---

# PART 7 — CONNECTING THE WEIGHING SCALE

Your scale is an **ES 510**. It sends the weight to the computer down a cable,
so the billing screen can read it without anyone typing.

## The cable

The scale has a **9-pin plug (DB9)** on its cable. It goes into the matching
**9-pin socket on the back of the computer** — the COM port, sometimes marked
`|O|O|` or `COM1`.

- It is **not** a USB plug. Do not look for a USB socket.
- Push it in and **tighten the two small screws** either side by hand. A loose
  plug gives dropped readings that are very confusing to chase later.
- Switch the scale on. It needs its own power — the cable does not power it.

## Setting it up in the software

Main menu → **Scale and weight** → **Device** tab.

Set these four boxes:

| Box | Set to |
|---|---|
| **Mode** | `Serial` |
| **Port** | `COM1` |
| **Baud** | `9600` |
| **Poll cmd** | leave empty |

Then click **Save settings**.

## Checking it actually works

Click **Start live dump**, then put something on the scale pan.

Numbers should start scrolling in the **Raw dump** box, like this:

```
30 30 30 2E 32 37 30  |  000.270
30 30 30 2E 32 37 30  |  000.270
30 30 30 2E 32 36 35  |  000.265
```

The right-hand column is the weight in kilograms. `000.270` means 270 grams.

**If you see numbers scrolling and they change when you press on the pan, the
scale is connected correctly.** Click **Stop**, then **Save settings**, and you
are finished.

## If the Raw dump box stays empty

Work through these in order. Stop as soon as numbers appear.

**1. Is the scale switched on?**
Its own display should be lit.

**2. Is the plug tight?**
Push it fully home and tighten both screws.

**3. Is it the right port?**
Try `COM2` and `COM3` in the Port box, clicking **Start live dump** after each.

**4. Try the Detect button.**
It tries the common speed settings one after another and tells you which one
the scale answers on.

**5. Swap pins 2 and 3.**
This is the most common cause and it is not your fault. There are two ways to
wire a 9-pin cable and they are not compatible. Any computer shop sells a
**null-modem adapter** for very little — plug it in between the cable and the
computer and try again.

**6. If the port is missing entirely from the Port list**, it is switched off
in the computer's BIOS rather than broken. That needs a technician for two
minutes.

## Meanwhile, you can still sell

If the scale is not working, billing carries on. The screen shows
**Scale: manual**, and pressing **F4** lets the cashier type the weight in.
Those bills are marked as hand-typed so you can see them later.

## What the Regex box is

You will not normally touch it. It tells the software how to read the scale's
message. It is already set correctly for your ES 510:

```
(?<status>ST|US)?[,\s]*(?:GS|NT)?[,\s]*(?<sign>[+-])?\s*(?<value>\d+(?:\.\d+)?)\s*(?<unit>kg|g)?
```

Leave it alone unless you replace the scale with a different make.

## Per-item weight settings

The second tab, **Per-item weight**, sets for each loose item:

- **Tare** — the weight of the bag or tray, subtracted automatically
- **Round to** — normally 5 g
- **Minimum sale** — normally 100 g

The defaults are correct for most goods.

---

# PART 8 — ADDING PRODUCTS

Main menu → **Item master**.

### Adding one item

Click **Add a new item** and fill in:

| Field | Notes |
|---|---|
| **Code (SKU)** | Your own short code. How you find the item if a barcode will not scan |
| **Name** | The full name, for the screen |
| **Print name** | Shorter name for the bill — the paper is only 48 characters wide |
| **Sold by** | `Piece` for packets, `Weight` for loose goods |
| **HSN** | The GST code for this kind of goods |
| **Tax rate** | In basis points — `500` means 5%, `1200` means 12% |
| **Cost / Selling / MRP** | In rupees. The margin is shown as you type |
| **Barcodes** | One per line. An item can have several |

### Pricing goods sold by weight

This is the part people get stuck on, so read it once carefully.

**You do not enter "this much weight costs this much".** You enter **one price
per kilogram**, and the software works out every weight from it.

Set **Sold by** to `Weight`. The Unit changes to `kg` and the price box
relabels itself to **Selling /kg**, so you can see what the number means.

Tomatoes at Rs. 40 a kilo:

| Box | Put |
|---|---|
| Sold by | `Weight` |
| Selling /kg | `40` |
| Cost | what you paid per kilo |
| MRP | `0` — loose goods have no printed price |
| Weigh at counter | **ticked** |

Now every weight prices itself:

| Customer takes | Software charges |
|---|---|
| 500 g | Rs. 20.00 |
| 1.240 kg | Rs. 49.60 |
| 2 kg | Rs. 80.00 |

To change the price when the market moves, edit that one number. Every future
sale follows it.

### The four weight boxes explained

| Box | What it does | Normal setting |
|---|---|---|
| **Weigh at counter** | Lets this item take a reading from your scale with **F4**. Without it, the scale is ignored for this item. | **Tick it** for anything you weigh |
| **Tare grams** | Weight of the bag or tray, taken off automatically so the customer does not pay for packaging. | `0`, unless you weigh into a tray |
| **Round to grams** | Your scale can only legally report in 5 g steps, so prices are never finer than that. | `5` |
| **Min sale grams** | Refuses anything lighter. The scale is not accurate below this and the law does not allow it. | `100` |

These boxes are greyed out for items sold by the piece, because they mean
nothing for a sealed packet.

### Two rules the software enforces

- **Selling price can never be above MRP.** Saving is blocked. This is the law,
  not a preference.
- **Selling below cost** gives a warning and asks you to confirm. Sometimes you
  mean it, but usually it is a typing mistake.

### Adding 1,500 items

Do not type them one by one. Click **Import from CSV** and load a spreadsheet.
There is a preview before anything is saved, and a list of any rows that
failed.

---

# PART 9 — BUYING STOCK FROM SUPPLIERS

Main menu → **Purchase entry**. This is how goods get into your stock.

**Nothing appears in your stock until you record the purchase.** If Stock take
is empty, this is why.

### Recording a delivery

1. **Supplier** — pick from the list. First time, click **+ New supplier**.
2. **Invoice number** — copy it from the supplier's bill exactly. The software
   refuses the same invoice twice, which is what stops goods being counted
   double.
3. **Invoice date**, **Payment mode**, and a **due date** if it is on credit.
4. For each item on the delivery, click **Add line** (or press **F2**) and fill in:

| Column | What to put |
|---|---|
| Item | Pick from the drop-down |
| Batch | The supplier's batch code, if there is one |
| Expiry | As `2026-12-31`. Leave blank if it does not expire |
| Pieces / Grams | How much arrived. Pieces for packets, grams for loose |
| Free pc / Free g | Free goods. **Adds to stock but costs nothing** |
| Cost | What you paid per unit |
| MRP | The printed price |
| Line value | Worked out for you |

5. Add **Freight** and any **Discount** on the whole invoice.
6. Click **Save purchase** (or press **F12**).

The goods are now in your stock and available to sell.

### Free goods matter

"Buy 10 get 1 free" — put 10 in Pieces and 1 in Free. All 11 go into stock,
but only 10 are paid for. That is what makes your real margin come out right.

---

# PART 10 — LOOKING AFTER STOCK

### Stock summary

What you have, batch by batch, with four figures across the top: total value,
how many products, how many are below their reorder level, and how many
batches expire within 30 days. Expiring items are coloured.

### Stock take — counting the shelves

Main menu → **Stock take**.

1. Every batch you own is listed with what the software **expects**.
2. Walk the shelves and type what you **actually counted** in the cream
   coloured columns.
3. The **Difference** column shows in plain words — "2 pc short",
   "0.150 kg extra". Short is red, extra is green.
4. Click **Save the count**. You are shown a summary before anything changes.

**Only lines you change are touched.** Anything you do not type stays as it is,
so you can count one shelf at a time.

### Damage and wastage

For anything spoiled, broken or thrown away. Choose the batch, the amount, and
**give a reason** — the reason is required, because in a month you will not
remember.

Without this, your stock figures drift away from reality within weeks.

### Open a sack

A 50 kg rice bag arrives as one piece but is sold loose by the kilo.

Choose the sealed bag as **Take from**, the loose batch as **Add to**, then
`1` bag out and `50000` grams in. Both must be the same product.

### Expiring soon and Items to reorder

Two lists worth checking weekly. Expiring soon shows what to sell or send back.
Items to reorder shows what has fallen below the level you set on the item.

---

# PART 11 — CUSTOMER KHATA (CREDIT)

This replaces the paper khata notebook. It holds money owed to you by name, so
every figure has to stand up when the customer disagrees at the counter.

### Adding a credit customer

Main menu → **Customer khata** → **New customer**.

A phone number **and** a name are required for credit. Then set their
**credit limit** and switch credit on. Credit is off by default for everyone.

### The ledger

Look a customer up by phone. You see their outstanding balance, credit limit,
available credit and how old their oldest unpaid bill is.

Below that is every transaction, oldest at the top, with a running balance and
**who recorded it**. Double-click any bill reference to see exactly what was
bought. A customer asking "what was that Rs. 2,770 for?" gets an answer in two
clicks.

### Taking a payment

**Record payment** — any amount, at any time. Choose cash, UPI or card, and
record who took the money. It is matched against their oldest unpaid bills
first. Print a receipt — it protects your staff as much as the customer.

### Nothing is ever edited or deleted

A mistake is fixed by adding a correcting entry, so the trail shows both the
error and the correction. This is what makes the balance defensible.

### Statements

**Print statement** gives the customer a printed record. Every statement
printed is logged, which settles most arguments before they start.

### Money owed by age

The owner's collection screen. Everyone who owes you money, sorted by how long
it has been owed: under 30 days, 31–60, 61–90, over 90. The over-90 rows are
coloured — that is where losses actually live.

---

# PART 12 — REPORTS AND GST

Main menu → **Reports and GST**.

- **Sales register** — every bill for a period
- **Item movement** — what sold, what did not
- **Margin and profit** — what you actually made
- **Stock valuation** — what your stock is worth
- **Dead stock** — nothing sold in 90 days
- **Tax summary by HSN** — for your GST return
- **GSTR-1 and GSTR-3B exports** — hand these to your accountant
- **Cashier performance** — who billed what

Your accountant works in Tally. Export from here rather than retyping.

---

# PART 13 — WHEN SOMETHING GOES WRONG

| What you see | What to do |
|---|---|
| Nothing prints | Settings → check the printer is chosen → **Print a test slip** |
| "Print failed (bill saved)" | **The sale is safe.** Fix the printer, then reprint |
| Printing is garbled | Wrong printer chosen. Pick the TVS RP 3230 in Settings |
| Text runs onto two lines | Print a test slip. The digits must fit one line exactly |
| Screen says "Scale: manual" | Scale not connected — see Part 7. Press F4 and type the weight |
| Scale gives no reading | Wait for it to settle. If nothing, see Part 7 |
| "Weight below minimum sale" | Under 100 g. Sell it pre-packed instead |
| Stock take is empty | You have no stock recorded. Do Part 9 first |
| Item not in the purchase drop-down | Add it in Item master first |
| "Duplicate supplier invoice" | That bill is already entered. Check before entering again |
| Cannot save a selling price | It is above the MRP. That is not allowed |
| Drawer will not open | Settings → change Drawer pin from 2 to 5 |
| Cashier cannot cancel a bill | Correct. A manager or owner PIN is needed |
| Forgotten PIN | The owner resets it in Staff accounts |
| Software will not start | Check Windows has .NET Framework 4.8 installed |

### Your data is safe

Everything is stored on this computer at:
`C:\Users\<your user>\AppData\Local\GroceryPos\grocery.sqlite`

- The software **never needs the internet**. It works with the line down.
- Updating the program does **not** touch your data.
- **Copy that file to a pen drive every week.** It is your entire business —
  every bill, every customer, every rupee owed to you. If the hard disk dies
  without a copy, it is all gone.

### Things the software will not let you do

These are not faults. They are there on purpose.

- Delete a bill — cancel it instead, so the number series stays unbroken
- Edit or delete a khata entry — add a correcting entry instead
- Change stock without a reason being recorded
- Sell above MRP
- Change a shift after it is closed
- Sell counter-weighed goods under 100 g

---

*Guide written for AKIL STORE. Keep it by the counter.*
