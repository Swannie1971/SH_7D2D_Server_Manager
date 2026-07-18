# 7D2D Server Manager — Lag Spike Test

Thanks for helping test this. We're chasing the random lag spikes. The manager now has
CPU **priority** and **affinity** controls, plus a **Diagnostics** button that copies a
report for us. Below is exactly what to click.

> **Important:** priority and affinity only take effect when the server **starts**, not while
> it's running. After changing either, **stop and start the server** for it to apply.

---

## Where the settings are

1. Open the manager and **select the server** on the left.
2. Go to the **Game Settings** tab.
3. Scroll down to the panel headed **"PERFORMANCE — CPU PRIORITY & AFFINITY"** (blue, near
   the bottom, just above "SANDBOX CODE"). Click it to **expand** it.

Inside you'll find:

- **Process Priority** — a dropdown: *Normal*, *Above Normal*, *High*.
- **Pin to specific cores (CPU affinity)** — an on/off **toggle** on the right.
- **Cores to allow** — a dropdown that lights up only when the affinity toggle is **on**.

There's a **Save** button at the bottom of the tab — you must click it after any change.

---

## The test — please do it in this order

Doing these one at a time is the whole point: if we change everything at once and it improves,
we won't know which thing did it.

### Round 1 — Priority only (do this first)

1. Set **Process Priority → Above Normal**.
2. Leave the affinity toggle **OFF**.
3. Click **Save**.
4. **Stop the server, then Start it again.**
5. Play for a while as normal. Note whether the spikes are better, worse, or the same.
6. When you've felt a spike (or played ~15–20 min), come back to Game Settings and click
   **COPY REPORT** in the red *DIAGNOSTICS (TEMPORARY)* box at the top of the tab.
   Paste that into Discord to me.

> On the report, check the two lines **"Configured priority"** and **"Applied priority"** —
> they should both say *AboveNormal*. If "Applied" still says *Normal*, that itself is a clue,
> so send it either way.

### Round 2 — Only if Round 1 didn't fully fix it: add affinity

1. Turn the **Pin to specific cores (CPU affinity)** toggle **ON**.
2. In **Cores to allow**, start **high** (e.g. all-but-two of your cores). Don't go low yet.
3. Click **Save**, then **Stop and Start** the server again.
4. Play again, then **COPY REPORT** and send it.
5. If it helped a little but not enough, try one step lower on the cores and repeat. If it made
   things *worse*, turn affinity back off — that's a useful result too, tell me.

---

## Sending the report

- **COPY REPORT** puts a text report on your clipboard — just paste it into Discord.
- **OPEN LOG FOLDER** opens the server's log file in Explorer if I ask you for the raw log too;
  you can drag that file straight into Discord.

For each report, a one-line note helps a lot, e.g. *"Round 1, Above Normal, still spiked around
the 10-minute mark."*

---

## Quick reference

| I want to…                    | Do this |
|-------------------------------|---------|
| Change priority               | Game Settings → PERFORMANCE panel → Process Priority dropdown |
| Turn affinity on/off          | Same panel → "Pin to specific cores" toggle |
| Choose how many cores         | Same panel → "Cores to allow" (only active when the toggle is on) |
| Apply a change                | Click **Save**, then **Stop + Start** the server |
| Send me a report              | Red DIAGNOSTICS box at the top of Game Settings → **COPY REPORT** → paste in Discord |

Don't worry about the red "TEMPORARY" box — it's a test-only tool I'll remove afterwards.
