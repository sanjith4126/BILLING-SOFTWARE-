# AKIL STORE — Billing Software Guide

Read Part 1 and Part 2. That is enough to run the counter all day.

The rest you can look up when you need it. Keep this book near the machine.

---

## WHAT IS IN THIS BOOK

| Part | What it covers | Who needs it |
|---|---|---|
| 1 | Starting up and signing in | Everyone |
| 2 | Making a bill | Whoever is at the counter |
| 3 | Weighing loose goods | Whoever is at the counter |
| 4 | Taking money, khata, holding a bill | Whoever is at the counter |
| 5 | Closing the day | Whoever closes up |
| 6 | Setting up — do this once | Owner |
| 7 | Connecting the weighing scale | Owner |
| 8 | Adding your products | Owner |
| 9 | Entering goods you bought | Owner |
| 10 | Counting stock and losses | Owner |
| 11 | Khata — money customers owe you | Owner |
| 12 | Reports for you and the accountant | Owner |
| 13 | When something goes wrong | Everyone |

---

# PART 1 — STARTING UP

### Turning it on

Double-click the **Billing** picture on the desktop.

If it is not there, ask whoever set the machine up to put it there. You should
never have to hunt for the program.

### Signing in

1. **Who is signing in** — pick your name from the list.
2. **PIN** — type your 4-number code and press **Enter**.

To begin with there is one user: **owner**, PIN **1234**.
Change that on your first day. Part 6 shows how.

### Who is allowed to do what

Each person gets their own name and PIN. That way the software can show you
later who sold what, who gave a discount, and who cancelled a bill.

| | Owner | Manager | Staff |
|---|---|---|---|
| Make bills, take money | Yes | Yes | Yes |
| Look up customers | Yes | Yes | Yes |
| Cancel a bill | Yes | Yes | Must ask a manager |
| Give a big discount | Yes | Yes | Must ask a manager |
| Change prices, add products | Yes | Yes | No |
| Throw away damaged stock | Yes | Yes | No |
| Change how much credit a customer gets | Yes | No | No |
| Add or remove staff | Yes | Yes | No |

### The main screen

After signing in you see coloured boxes in six groups:

- **Billing** — making bills, closing the day
- **Stock** — what you have, counting it, damage, sacks, expiry, reordering
- **Buying** — entering supplier bills, sending goods back
- **Customers and credit** — khata, who owes you, old balances
- **Products and reports** — your product list, reports
- **Setup** — the scale, shop details, staff

Click a box to open it. Close it to come back here.

### A normal day

| When | What you do |
|---|---|
| Morning | Tell the software how much cash you put in the drawer |
| All day | Make bills |
| Night | Count the drawer and close the day |

Everything else you only do now and then.

---

# PART 2 — MAKING A BILL

This is the job you do a hundred times a day. Learn this part properly and
you can ignore most of the rest.

### Opening the counter

1. Click **Billing counter**.
2. The first time each day it asks **"Open a shift?"** — click **Yes**, then
   type how much cash you are starting with, like `500`.
3. The billing screen opens.

The blinking line sits in the **Scan barcode or type an item name** box at the
top. It should stay there all day. If you lose it, click that box once.

### Putting items on the bill

Three ways. Use whichever is quickest:

- **Scan the barcode.** The item appears straight away.
- **Type the barcode numbers** and press **Enter**.
- **Type part of the name** — like `atta` — and press **Enter**. A list pops
  up and you pick the right one.

Each item becomes a line on the left. The total on the right goes up as you add.

### Fixing a mistake

| To do this | Press |
|---|---|
| Take off the line you clicked on | **Del** |
| Throw the whole bill away and start again | **Esc** |

### The keys you need

These are printed along the bottom of the billing screen, so you do not have
to remember them.

| Key | What it does |
|---|---|
| **F2** | Put this bill aside and start a new one |
| **F3** | Bring a bill back that you put aside |
| **F4** | Weigh the item you clicked on |
| **F5** | Give a discount on the item you clicked on |
| **F9** | Take the money and finish |
| **Del** | Take off the item you clicked on |
| **Esc** | Clear the whole bill |
| **Ctrl + K** | Find a customer by phone number |

A whole bill can be done without touching the mouse.

### Finishing

Press **F9**. Part 4 tells you what happens next.

---

# PART 3 — WEIGHING LOOSE GOODS

For anything you sell by weight — tomato, sugar, dal, loose atta.

### The normal way

1. Put the goods on the scale.
2. **Wait until the number on the scale stops moving.** This matters. The
   software will not take a reading that is still jumping about.
3. On the billing screen, add the item by scanning or typing its name.
4. Press **F4**.
5. The weight fills in and the amount works itself out.

### If the scale is not working

You can still sell. Press **F4** and type the weight in grams yourself.

Weights typed by hand are **marked in the records**, so the owner can see how
often it happened. That is on purpose — it is how money quietly goes missing.

### Two things the software will not let you do

- **Nothing under 100 grams.** Below that the scale is not accurate enough and
  the law does not allow it. Sell small amounts as ready-made packets with
  their own barcode.
- **Weights go in steps of 5 grams.** The scale can only officially report in
  those steps, so the software never charges for anything smaller.

### The weight is never changed

Whatever the scale says is what the customer is charged. The software writes
down exactly what the scale reported and never adjusts it. If anyone ever
questions a bill, the record will back you up.

---

# PART 4 — TAKING MONEY, KHATA AND HELD BILLS

### Taking the money

Press **F9**. A window opens showing what is owed.

| Paid by | What to do |
|---|---|
| **Cash** | Type what they handed you. The change is worked out for you. |
| **UPI** | Type the amount and the UPI number. |
| **Card** | Type the amount and the reference. |
| **Khata** | Puts it on the customer's account. See below. |

**Part cash, part khata** is normal — they pay Rs. 500 now and put Rs. 270 on
the account. Enter both. Only the khata part goes on their account.

Click **Save**. The bill is saved, then printed.

### If the printer fails, you do not lose the sale

The bill is saved **before** it is printed. If the paper runs out or the
printer is off, you will see **"Print failed (bill saved)"**.

The sale is safe. Sort the printer out and print it again.

### Selling on khata

1. Before pressing F9, press **Ctrl + K** and find the customer by phone.
2. Their name, what they owe, and how much more they can take shows at the top.
3. Press **F9**, choose **Khata**, save.

If it would take them over their limit, the software **stops** and shows you
what they owe, their limit, and how much over they are. A manager or owner
has to type their PIN to allow it. Every one of these is written down, whether
you allowed it or refused.

New customers start with **no credit**. The owner has to switch it on.

### Putting a bill aside

Someone goes back for something they forgot and there is a queue behind them.

- **F2** — the bill goes aside, you get a fresh one.
- **F3** — bring it back when they return.

Bills you put aside are listed along the bottom of the screen.

### Cancelling a bill

Click **Cancel a past bill**. A manager or owner PIN is needed.

Cancelling puts the stock back, undoes any khata entry, and **keeps the bill
number**, marked as cancelled.

Nothing is ever deleted. Bill numbers have to run without gaps for GST, so a
cancelled bill keeps its number instead of disappearing.

---

# PART 5 — CLOSING THE DAY

Click **Shift and day close**.

1. The screen lists every note and coin.
2. Count the drawer and type **how many** of each you have.
3. As you type it shows you:
   - what **should** be in the drawer
   - what you **actually counted**
   - whether you are **short** or **over**, in red
4. It also shows the UPI, card and khata totals so you can check them against
   your phone and card machine.
5. Click **Close the shift and print the day report**.
6. Say yes. The report prints.

**Once you close the day you cannot change it.** Count properly before you
say yes.

---

# PART 6 — SETTING UP (owner, once)

Do these in order before your first real sale.

### 1. Change the owner PIN

**Staff accounts** → click `owner` → **Change PIN**.

Do not leave it as 1234.

### 2. Add your staff

Same screen → **Add new user**. Give each person their own name and PIN, and
pick what they are allowed to do.

Everyone needs their own login. Every bill, discount and cancellation is
recorded against a name — which is worth nothing if three people share one
login.

### 3. Fill in your shop details

Click **Settings**.

| Box | What goes in it |
|---|---|
| Shop name | Printed at the top of every bill |
| Address line 1 and 2 | Printed under the name |
| Phone number | Printed on the bill |
| GSTIN | Your GST number. Leave empty if you are not registered |
| Bill title when no GSTIN | Usually `CASH BILL` |
| Footer line | Something like "Thank you, visit again" |

As you type the GST number the screen tells you whether your bills will say
**TAX INVOICE** or **CASH BILL**.

### 4. Set up the printer

Still in **Settings**, under **Receipt printer**:

1. Open the list. It shows the printers this computer knows about.
2. Pick your bill printer — the **TVS RP 3230**.
3. Click **Print a test slip**.

A slip prints with a row of numbers on it. **Check that the row fits the paper
in one line and does not spill onto a second.** If it fits, your printer is
set up right.

Leave the box empty if you do not want to print at all.

### 5. Cash drawer

Tick **A cash drawer is connected** only if one is actually plugged into the
printer.

If you tick it and the drawer does not open, change **Drawer pin** from 2 to 5
and try again. Different drawer makes are wired differently — this is normal
and it is not a fault.

The drawer opens by itself on a cash sale, on cash paid against khata, and at
day close. It never opens for UPI or card.

### 6. Counter rules

| Setting | What it means |
|---|---|
| Counter number | Leave at 1 unless you have more than one billing machine |
| Discount without approval | How much a staff member can knock off before needing a manager |
| Loyalty points per Rs. 100 | Put 0 if you do not want points |

Click **Save settings**.

### 7. The scale

Part 7 covers it.

---

# PART 7 — CONNECTING THE WEIGHING SCALE

Your scale sends the weight down a cable to the computer, so nobody has to
type it.

## Plugging it in

The scale's cable ends in a **plug with 9 pins**. It goes into the matching
**9-pin socket on the back of the computer**.

- It is **not** a USB plug. Do not look for a USB socket.
- Push it in and **tighten the two little screws** on the sides with your
  fingers. A loose plug gives readings that come and go, which is very hard to
  work out later.
- Switch the scale on. It needs its own power — the cable does not power it.

## Telling the software about it

Click **Scale and weight**, then the **Device** tab.

Set these four boxes:

| Box | Put |
|---|---|
| **Mode** | `Serial` |
| **Port** | `COM1` |
| **Baud** | `9600` |
| **Poll cmd** | leave empty |

Click **Save settings**.

## Checking it works

Click **Start live dump**, then press down on the scale pan.

Numbers should start rolling down the **Raw dump** box:

```
30 30 30 2E 32 37 30  |  000.270
30 30 30 2E 32 37 30  |  000.270
30 30 30 2E 32 36 35  |  000.265
```

Ignore the left half. The right half is the weight in kilos —
**`000.270` means 270 grams.**

**If numbers are rolling and they change when you press the pan, the scale is
connected properly.** Click **Stop**, then **Save settings**. You are done.

## If nothing appears

Work down this list. Stop as soon as numbers show up.

**1. Is the scale switched on?** Its own display should be lit.

**2. Is the plug pushed all the way in?** Tighten both screws.

**3. Is it the right socket?** In the **Port** box try `COM2`, then `COM3`,
clicking **Start live dump** after each.

**4. Try the Detect button.** It tries the usual settings one after another
and tells you which one the scale answers on.

**5. The wires may be the wrong way round.** This is the most common reason
and it is nobody's fault — there are two ways to make this cable and they do
not work with each other. Any computer shop sells a small part called a
**null-modem adapter** for very little money. Plug it in between the cable and
the computer and try again.

**6. If the Port box has no options at all**, the socket is switched off
inside the computer's settings, not broken. A computer technician turns it
back on in two minutes.

## You can still sell without it

If the scale is not working, billing carries on as normal. The screen shows
**Scale: manual** and pressing **F4** lets you type the weight in. Those bills
are marked so you can find them later.

## The Regex box — leave it alone

That long line of symbols tells the software how to read your scale's
messages. It is already correct for your scale.

Only change it if you buy a different make of scale, and then get whoever
sets it up to do it.

## The second tab

**Per-item weight** sets, for each loose item, the weight of the bag, the step
size, and the smallest amount you will sell. The standard settings suit almost
everything. Part 8 explains them.

---

# PART 8 — ADDING YOUR PRODUCTS

Click **Item master**, then **Add a new item**.

| Box | What goes in it |
|---|---|
| **Code (SKU)** | Your own short code, like `ATTA5`. How you find the item when a barcode will not scan |
| **Name** | The full name, for the screen |
| **Print name** | A shorter name for the bill. The paper is narrow, so long names get cut off |
| **Sold by** | `Piece` for packets, `Weight` for loose goods |
| **HSN** | The government's code for this type of goods. Your accountant or the supplier's bill will have it |
| **Tax rate** | GST, but written without the % sign and with two extra zeros. **5% is `500`. 12% is `1200`. 18% is `1800`.** |
| **Cost / Selling / MRP** | In rupees. Your profit shows as you type |
| **Barcodes** | One per line. An item can have more than one |

### Selling by weight — read this once

This is the part people get stuck on.

**You do not enter "half a kilo costs this much".** You enter **one price for
one kilogram**, and the software works out every weight from it.

Set **Sold by** to `Weight`. The unit changes to `kg` by itself and the price
box changes to say **Selling /kg**, so you can see what the number means.

For tomatoes at Rs. 40 a kilo:

| Box | Put |
|---|---|
| Sold by | `Weight` |
| Selling /kg | `40` |
| Cost | what you paid for a kilo |
| MRP | `0` — loose goods have no printed price |
| Weigh at counter | **tick it** |

Now every weight prices itself:

| Customer takes | They pay |
|---|---|
| half a kilo | Rs. 20.00 |
| 1.240 kg | Rs. 49.60 |
| 2 kilos | Rs. 80.00 |

When the market price changes, change that one number. Every sale after that
follows it.

### The four weight boxes

| Box | What it does | Normally |
|---|---|---|
| **Weigh at counter** | Lets this item read your scale when you press **F4**. Without the tick, the scale is ignored for this item. | **Tick it** for anything you weigh |
| **Tare grams** | The weight of the bag or tray, taken off by itself so the customer does not pay for the packing | `0`, unless you weigh into a tray |
| **Round to grams** | Weights go in steps of this many grams | `5` |
| **Min sale grams** | The software will not sell less than this | `100` |

These four are greyed out for packets, because they mean nothing there.

### Two things the software will not let you do

- **You cannot set a selling price above the MRP.** It refuses to save. That is
  the law, not a preference.
- **Selling below what you paid** gives you a warning and asks if you are sure.
  Sometimes you mean it. Usually it is a typing mistake.

### If you have hundreds of products

Do not type them one at a time. Click **Import from a spreadsheet** and load an
Excel file. It shows you what it is about to do before saving anything, and
lists any rows it could not read.

---

# PART 9 — ENTERING GOODS YOU BOUGHT

Click **Purchase entry**. This is how goods get into your stock.

**Nothing is in your stock until you enter the supplier's bill.** If the stock
screens look empty, this is why.

### Entering a delivery

1. **Supplier** — pick from the list. The first time, click **+ New supplier**.
2. **Invoice number** — copy it off the supplier's bill exactly. The software
   refuses the same bill twice, which stops goods being counted double.
3. **Invoice date**, how you are paying, and a **due date** if it is on credit.
4. For each thing they delivered, click **Add line** and fill in:

| Column | What goes in it |
|---|---|
| Item | Pick from the list |
| Batch | The supplier's batch code, if there is one on the packet |
| Expiry | Written as `2026-12-31`. Leave empty if it does not expire |
| Pieces / Grams | How much arrived. Pieces for packets, grams for loose |
| Free pc / Free g | Free goods. **They go into stock but cost you nothing** |
| Cost | What you paid for one |
| MRP | The price printed on the packet |
| Line value | Worked out for you |

5. Add any **Freight** and **Discount** on the whole bill.
6. Click **Save purchase**.

The goods are now in your stock and ready to sell.

### Why free goods matter

"Buy 10 get 1 free" — put 10 in Pieces and 1 in Free. All 11 go into stock but
only 10 cost you anything. That is what makes your real profit come out right.

---

# PART 10 — LOOKING AFTER YOUR STOCK

### Stock summary

Shows what you have, with four numbers across the top: what your stock is
worth, how many products you sell, how many are running low, and how many are
close to their expiry date. Things near expiry are coloured so they stand out.

### Counting your shelves

Click **Stock take**.

1. Everything you own is listed, with what the software **thinks** you have.
2. Walk the shelves and type what you **actually counted** in the cream
   coloured columns.
3. The **Difference** column tells you in plain words — "2 pc short",
   "0.150 kg extra". Short is red, extra is green.
4. Click **Save the count**. It shows you a summary before changing anything.

**Only lines you type in are changed.** Anything you leave alone stays as it
is, so you can do one shelf today and another tomorrow.

### Damage and wastage

For anything spoiled, broken or thrown out. Pick the item, say how much, and
**give a reason**. The reason is required, because in a month you will not
remember.

Skip this and your stock numbers drift away from reality within weeks.

### Opening a sack

A 50 kg rice bag comes in as one sack but sells loose by the kilo.

Pick the sealed bag as **Take from**, the loose one as **Add to**, then `1` bag
out and `50000` grams in. Both have to be the same product.

### Two lists worth a weekly look

- **Expiring soon** — sell these first or send them back
- **Items to reorder** — what has run low

---

# PART 11 — KHATA

This replaces the khata notebook. It holds money owed to you by name, so every
figure has to stand up when a customer argues at the counter.

### Adding a customer who buys on credit

**Customer khata** → **New customer**.

You need a phone number **and** a name. Then set how much credit they are
allowed and switch credit on. Nobody gets credit until you say so.

### Their page

Look them up by phone. At the top you see what they owe, their limit, how much
more they can take, and how old their oldest unpaid bill is.

Below that is every single transaction, oldest first, with a running total and
**who wrote it down**. Double-click any bill to see exactly what was bought.

A customer asking "what was that Rs. 2,770 for?" gets an answer in two clicks
instead of an argument.

### Taking a payment

**Record payment** — any amount, any time. Cash, UPI or card, and it records
who took the money. It goes against their oldest unpaid bills first.

Print them a receipt. It protects your staff as much as the customer.

### Nothing is ever rubbed out

A mistake is fixed by adding a correcting line, so you can see both the mistake
and the correction. That is what makes the balance something you can defend.

### Statements

**Print statement** gives the customer a printed record of everything. Every
statement you print is logged, which settles most arguments before they start.

### Who owes you, by age

Everyone who owes you money, sorted by how long it has been owed: under 30
days, 31 to 60, 61 to 90, and over 90. The over-90 rows are coloured.

That last group is where money is actually lost.

---

# PART 12 — REPORTS

Click **Reports and GST**.

- **Sales register** — every bill for a period
- **Item movement** — what sold and what did not
- **Margin and profit** — what you actually made
- **Stock valuation** — what your stock is worth today
- **Dead stock** — nothing sold in 90 days
- **Tax summary** — for your GST return
- **GST exports** — hand these to your accountant
- **Cashier performance** — who billed what

Your accountant works in Tally. Send them these files instead of retyping
everything by hand.

---

# PART 13 — WHEN SOMETHING GOES WRONG

| What you see | What to do |
|---|---|
| Nothing prints | Settings → check a printer is chosen → **Print a test slip** |
| "Print failed (bill saved)" | **The sale is safe.** Fix the printer and print it again |
| Printing comes out as nonsense | Wrong printer chosen. Pick the TVS one in Settings |
| Text spills onto two lines | Print a test slip. The row of numbers must fit one line |
| Screen says "Scale: manual" | Scale not connected — Part 7. Press F4 and type the weight |
| Scale shows nothing | Wait for it to settle. If still nothing, Part 7 |
| "Weight below minimum sale" | Under 100 grams. Sell it as a ready-made packet |
| Stock take screen is empty | You have not entered any supplier bills yet — Part 9 |
| An item is missing from the purchase list | Add it in Item master first |
| "Duplicate supplier invoice" | That bill is already entered. Check before entering it again |
| It will not save a selling price | It is higher than the MRP. That is not allowed |
| Drawer will not open | Settings → change Drawer pin from 2 to 5 |
| Staff cannot cancel a bill | That is correct. A manager or owner has to type their PIN |
| Someone forgot their PIN | The owner resets it in Staff accounts |

### Your information is safe

Everything is kept on this computer, not on the internet.

- The software **does not need the internet**. It works when the line is down.
- Updating the software **does not touch your information**.
- **Copy it to a pen drive every week.**

That last one matters more than anything else in this book. That file is your
whole business — every bill, every customer, every rupee owed to you. If the
computer's disk dies and you have no copy, it is all gone.

Ask whoever set the machine up to show you which file to copy, and to write it
on the inside cover of this book.

### Things the software will not let you do

These are not faults. They are deliberate.

- Delete a bill — you cancel it instead, so the numbers stay unbroken
- Rub out a khata entry — you add a correcting line instead
- Change stock without giving a reason
- Sell above the MRP
- Change a day after you have closed it
- Weigh and sell less than 100 grams

Every one of these exists to protect you — either from an argument with a
customer, or from a question from the GST office.

---

*Written for AKIL STORE. Keep it by the counter.*
