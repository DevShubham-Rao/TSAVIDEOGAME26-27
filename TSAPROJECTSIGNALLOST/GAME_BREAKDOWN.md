# Signal Lost: Static Sweeper — Full Breakdown

A reference for explaining the game and answering deep-dive questions.
Unity 2022.3 (URP, Input System, TextMeshPro). Almost all content is data-driven JSON in `Assets/StreamingAssets/Data`; all UI is built in code.

**Note on "why":** the reasons below come from three places: your playtest feedback (marked *[your feedback]*), the original spec PDF, and design reasoning I applied while building. Where it is my reasoning rather than your stated request, it is labelled *[design reasoning]*.

---

## 1. The 30-second pitch

You are the lone technician on **Relay Station Omega**, the last link in a chain of relay stations that carries Home Base's signal to survey ships beyond the edge of known space. A burst from deep space has scrambled the signal into glowing **Static**. You pop the Static, vacuum up the clean **Data bits** inside, sort them into colored chutes, and a Compressor crunches them into packets. Each night you read mail, make trade-off choices, and spend Data on one big skill tree. Over ~17 days the signal climbs to 100%, and a three-way loyalty choice (Hale / Dr. Sol / the Static) decides one of three endings.

It is an **idle/incremental game with an active core loop**: early on you do everything by hand; by late game drones, towers and auto-feeding do most of it, and your job shifts to events, combos and decisions.

---

## 2. Plot summary

### Cast
| Character | Role | Voice | What they want |
|---|---|---|---|
| **You** | Technician at Omega (silent protagonist) | Replies are your only words | — |
| **Commander Hale** | Home Base Operations | Gruff, busy, hates jokes | Reach 100% signal, then **wipe the channel** clean. Promotion on completion. |
| **Dr. Iva Sol** | Home Base Science | Curious, warm, nerdy | **Decode** the Static before it is wiped. Believes it is a language. |
| **The Static (???)** | Unknown sender | Speaks only in shapes: ▲ ● ★ | To be heard. Rebuilds itself every time it is crunched. |
| **Supply Bot** | Logistics unit | Extremely polite | Comic relief. Rubber ducks, humming packages, "crying oil." |

### Act by act
**Opening (Day 1).** At 03:12 station time a burst from deep space hits the dish. The receiver overloads and floods the screen with Static. Hale wakes you: if the Relay Chain goes dark, every survey ship past the edge goes deaf. He gives the loop in one breath: pop, sweep, sort, crunch. The tutorial teaches it live.

**Early days (2–3).** Hale is the boss; Dr. Sol appears as a friendly scientist. She notices that the Static's bits repeat in patterns — "that is not noise." Hale dangles a budget choice (bonus now vs. a crew that tunes the station).

**Escalation (4–8).** The first message from the **Static** arrives (▲●▲). By Day 5 Sol explains the twist: *every crunch saves a piece of a compressed message, and popping doesn't hurt the Static — it rebuilds inside your Compressor.* It keeps coming back **because it wants to be heard.** Day 6: Hale orders you to block Sol. Day 8: the Static asks a question (★ ?).

**Midpoint cutscene (Antenna 5, ~50% signal).** The Static stops moving and freezes into a shape. Sol's decoder produces the first full word: **HELLO**. Hale dismisses it as "interference shaped like a word." Sol: "It wants to be heard!" The narrator closes: *"Soon, someone will ask you to choose a side."*

**Crisis (Days 10–12).** Sol: Hale plans to wipe the channel permanently at 100% — the message would be lost forever. Hale: "Get me 100% and you're promoted… whatever Sol told you, it's interference." You can side with any of the three, or (with the Open Channel upgrade) ask the Static directly.

**Finale (Antenna 10 = 100%).** Every piece of Static freezes and drifts into one enormous shape. Hale's wipe command is armed, Sol's decoder is open, "something is waiting for you." Your single reply is "…Listen." The game tallies **trust** and plays an ending.

### The three endings
Highest trust wins; ties go to Iva, then Hale.
1. **Clear Channel (Hale).** You wipe the Static. Silent, clean, promotion approved. Omega "hums alone at the edge of known space. It is very, very quiet." — the *safe, lonely* ending.
2. **First Contact (Dr. Sol).** The decoder resolves the message: **"WE ARE LOST TOO."** Humanity makes first contact — the *hopeful, scientific* ending.
3. **Become the Signal (the Static).** You stop sweeping and listen. The blobs lift the station off the moon into a nebula. "You are part of the signal now. ★★★" — the *transcendent, strange* ending.

Each ending then pays **Echoes** and offers **Rebroadcast** (rebirth).

### Thematic reading
The game's irony is that you are *paid to destroy a message*. The sweeper loop is literally the act of silencing something that is trying to talk. The Static's final line ("We are lost too") reframes the title: "Signal Lost" is about both sides. This is why the ending is a *choice about what you do with the noise*, not a boss fight.

---

## 3. Core gameplay

### 3.1 The production line: Pop → Sweep → Sort → Crunch
1. **Pop (left-click).** Click Static. Tougher types need more clicks (Pop Power). Popping drops **Data bits**.
2. **Sweep (hold right-click).** The vacuum pulls loose bits into a tank (capacity upgradeable). *Toggle mode* in settings: right-click to start, right-click again to drop.
3. **Sort (release over a chute).** Three colored chutes (red / blue / green). Matching bits fly in; non-matching bits **spill beside the chute with no penalty**. Gold and Prism bits fit any chute. The **Universal Chute** upgrade merges all three into one.
4. **Crunch.** Sorted bits feed the **Compressor**. When full (or via Auto-Feed at 80%) it crunches and **pays Data** — this is the *only* place Data is earned.

**Static vs. bits (visual language).** Static = flickering TV-snow blobs (shader mode 0). Bits = small glowing octahedron gems (mode 1). *[your feedback]* — so players can tell "thing to click" from "thing to vacuum" at a glance.

### 3.2 Static types (colors)
| Type | Color | Unlock | Notes |
|---|---|---|---|
| Static | red | start | Basic, 1 bit |
| Hum | blue | Antenna 2 | Tougher, 3 bits, sine-wave drift |
| Buzz | green | Antenna 4 | Hops; splits into 3 Static when popped |
| Prism | rainbow | Antenna 1 (Deep Signal only) | Bits fit any chute; very high value |
| Mega | big | Antenna 5 | 7% chance; splits into 4 |
| **Gold** | gold | Antenna 3 | Fast, escapes after ~6 s, worth ~100x. One per day before Antenna 3 so it is a discovery, not noise. |

### 3.3 Combo
Popping in quick succession builds a combo. Bonus is delivered as **extra bits at pop time** (not a multiplier at crunch). Window starts at 1.5 s (Combo Window upgrades extend it).
- **Grace period (0.8 s):** after the bar empties it flickers instead of vanishing; the combo only breaks once grace is used. *[your feedback — "last longer, like coyote jump"]*
- **Wrong chute never costs combo.** *[your feedback — so you can vacuum two colors and deliver one, then the other.]*
- **QTE freeze:** combos freeze while an event puzzle is active so events never punish you.

### 3.4 Days (shifts)
Each day is one **60-second shift** (+Overtime upgrades) plus a 5 s "Last Call." Phases: Cutscene → Title Card → Shift → Last Call → Night.
- Spawn rate and max Static grow every day.
- **Night** = End-of-Day Report → Mail → Skill Tree (the tree is only purchasable here). "Undo Tonight" refunds that night's purchases.
- Target pacing is ~17 days (`target_days`); fast runs earn bonus Echoes.

### 3.5 Mail and trade-offs
Story mail on Days 1–6, 8, 10, 12 and at Antenna 10. Each reply gives a **real, stated trade-off** (shown in the hint text), e.g. *+20% Data tomorrow* vs. *Gold x2 but shift 10 s shorter*. Replies also shift **trust** toward Hale / Iva / Static — that hidden tally decides the ending. Flavor mail (23 messages, gated by Antenna level) gives small timed buffs and character color. The Static's mails are written entirely in symbols and decoded by hover/learning rather than text.

Effects are "tomorrow modifiers" (`data_mult`, `shift_seconds`, `golden_mult`, `spawn_mult`, `no_events`) plus timed buffs and instant payouts.

### 3.6 Skill tree (single, fog-of-war)
One tree, four branches. Branch colors are a pure UI theme and **unrelated to Static colors**. *[your feedback]*
| Branch | Theme | Examples |
|---|---|---|
| **Pop (Navy)** | Clicking | Pop Power, Pop Area, Crit Pop, Combo Window, Chain Spark, Shockwave Pop, Auto-Pop Tower |
| **Sweep (Pink)** | Vacuum + chutes | Tank Size, Suction Radius, Sweep Speed, Bit Magnet, Magnet Pulse, **Universal Chute**, Wide Nozzle |
| **Station (Orange)** | Compressor + drones | Crunch Bonus/Speed, Compressor Size, Auto-Feed, Drone Bay/Speed/Tank, Overclock, Jackpot Crunch |
| **Signal (Purple)** | Progress + economy | Overtime, **Antenna I–X**, Static Density, Data Value, Golden Chance, Night Storage, Lucky Shift, **Open Channel** |

Design rules: **fog of war** (you only see nodes next to what you own), **pan/zoom** viewport, **single-rank nodes** (a "tier" is a separate node, e.g. Pop Power I…V), and steep cost growth so choices matter. Antenna nodes show "All Data ×now → ×next" and what they unlock.

**Antenna** is the progression spine: each level is +50% all Data and unlocks new Static types and puzzle events. Antenna 10 ends the run.

### 3.7 Puzzle events (QTEs)
Unlocked by Antenna: **Solar Flare** (mash shield), **Power Surge** (repeat wire sequence), **Hull Leak** (patch every leak), **Signal Rush** (sweep 24 bits for 5x crunch data), **Golden Swarm** (pop every golden blob). Success pays buffs/Data; failure causes a small setback (freeze blobs, pause drones, reset combo). Events never block the whole screen without a timer.

### 3.8 Drones
Sweeper drones carry bits to the correct chute. This is the idle-game payoff: your hands become a manager of automation.

### 3.9 Rebirth: Rebroadcast, Echoes, Deep Signal
After any ending you earn **Echoes** (3 per ending, +5 for a first-time ending, +1 per day finished under 17). **Rebroadcast** resets the main tree, Data, Antenna, trust and story but **keeps** Echoes, endings seen and the **Deep Signal tree**:
- Generic: Head Start, Veteran Hands, Long Shift, Night Crew, Echo Amplifier, Prism Static.
- **Memory nodes**, one per ending, each unlocked by seeing that ending:
  - **Clear Channel Memory** (Hale): puzzle events still appear but Hale handles them — the overlay says "CLEAR CHANNEL / HALE HANDLES IT," no puzzle, reward still paid.
  - **First Contact Memory** (Sol): every story mail gets the extra "talk to the Static" reply.
  - **Become the Signal Memory** (Static): every popped Static drops 1 extra bit.

The ending screen shows **ENDING n/3**, and a prompt telling you to spend Echoes and chase the remaining endings. A debug menu (~ / F1 / DBG tab) can grant resources and reveal the whole tree.

---

## 4. Reasoning behind major decisions

### Story decisions
| Decision | Why |
|---|---|
| **Silent protagonist, replies as the only voice** | Keeps focus on the choice mechanic; lets the player project. [design reasoning] |
| **Three-way (not two-way) loyalty** | A Hale-vs-Sol split is a standard good/bad binary. Adding a third faction that *isn't human* makes "side with the noise" a real, unsettling option. [design reasoning] |
| **Static speaks only in symbols** | It communicates by being *felt*, not translated — and the "decoding" theme becomes a thing the player does. [design reasoning] |
| **The enemy is a message** | Turns the core loop (clearing noise) into a moral question without adding combat. The brief was "no combat wording." |
| **Hidden trust tally, visible trade-off hints** | Players see the *gameplay* cost of every reply, so choices feel mechanical and fair; the *story* consequence stays hidden so the ending feels earned. [design reasoning] |
| **Story gated to days/antenna levels** | Plot beats land on the right day; the finale fires at Antenna 10 so progression and story end together. |
| **Midpoint cutscene at Antenna 5** | Gives the long middle a landmark and plants "someone will ask you to choose a side." [design reasoning] |
| **Short, comic flavor mail (rubber ducks, humming packages)** | Contrast: the plot is melancholy, so the filler keeps the station feeling lived-in. [design reasoning] |
| **Lore pass: Static is "scrambled signal", bits are "clean data inside"** | Fixes "why am I popping/sweeping/crunching?" confusion. *[your feedback — narrative clarity]* |
| **Cutscenes click-to-advance** | Respects reading speed. *[your feedback]* |
| **Three endings, tracked as n/3** | Gives rebirth a goal and makes replay concrete. *[your feedback — ending screen N/3 + Echoes prompt]* |
| **Each memory upgrade rewards one ending** | Replays feel like collecting perspectives, and each ending leaves a permanent trace in play style. [design reasoning] |

### Gameplay decisions
| Decision | Why |
|---|---|
| **Day-by-day shifts instead of one endless session** | Gives natural pacing, a place for story, and a spending moment. *[your feedback — day-by-day redesign]* |
| **Start with just Pop → Sweep → Sort → Crunch, no extras** | "Less to start with" so new players aren't overloaded. *[your feedback]* |
| **Three color chutes, with a Universal Chute upgrade that removes them** | Chutes add sorting skill early; the upgrade is a satisfying "my annoying chore is gone" purchase. *[your feedback — you liked the 3 chutes being annoying and removable]* |
| **Left-click pops, right-click sucks** | Matches the usual "primary action / secondary action" mouse expectation. *[your feedback]* |
| **Data only pays at the Crunch** | One clear payday per cycle; avoids double-counting and makes Compressor upgrades matter. |
| **Combo gives extra bits at pop, not a payout multiplier** | A multiplier at crunch double-counted and was hard to read; extra bits are visible. [design reasoning, from a bug fix] |
| **Combo grace + wrong chute doesn't break combo** | Mistakes shouldn't erase a streak in a game about flow. *[your feedback]* |
| **Aim by mouse position, larger catch radius** | Players aim at the visible chute rim, not the bit's world position. *[your feedback]* |
| **Fog-of-war, pan/zoom, single-rank tree** | Reduces overwhelm; makes every node feel like a deliberate purchase. *[your feedback]* |
| **Tree colors independent of Static colors** | Prevents the false link "orange branch = orange Static." *[your feedback]* |
| **Buy only at night; Undo Tonight** | Keeps play and shopping separate; undo removes buyer's remorse. [design reasoning] |
| **Gold messaging gated + one Gold per day early** | Players kept missing Gold or being told about it before it existed. *[your feedback]* |
| **Antenna as the spine** | One number doubles as pacing, power curve, content gate and story trigger. [design reasoning] |
| **Event timers + QTE combo freeze** | Puzzles are a break, not a punishment. *[your feedback]* |
| **Antenna descriptions show real multipliers** | The most powerful upgrades were the least understood. *[your feedback]* |
| **Audio voice limiter (3 per sound / 10 total per 0.1 s)** | Late game has hundreds of simultaneous pops; unlimited audio is mud and clipping. *[your feedback]* |
| **Tutorial reading time scales with text length; box moved to top-left, non-blocking** | Lore text was too fast, and the box covered the chutes and mail choices. *[your feedback]* |
| **Rebirth keeps Echoes/Deep tree** | Idle games need a prestige loop; tying it to *endings* instead of a raw reset makes it narrative. *[your feedback — "better rebirth"]* |

---

## 5. Technical architecture (for dev questions)
- **Singletons** (`X.I`): `GameManager`, `DayManager`, `SkillTree`, `MailDirector`, `EventDirector`, `TutorialDirector`, `AudioManager`, `Vacuum`, `Compressor`, etc. Static `EventBus` for decoupled events.
- **Data:** `Balance.B` reads `balance.json` (all tunables), plus `tree.json`, `mail.json`, `events.json`, `tutorial.json`, `cutscenes.json`. Adding a tree node with an existing stat needs no code.
- **Stats:** `GameStats` resolves a stat as `(base + add) × (1 + pct) × mult` from owned nodes via `SkillTree.Stat()`.
- **Input:** `InputReader` wraps the Input System; the cursor maps to a play plane at y = 1.5; chute detection also has a screen-space test.
- **Visuals:** custom `SignalLost/BlobWobble` shader (static snow vs. gem mode); UI built entirely in code with `UIFactory`.
- **Save:** `SaveManager`, version 5.
- **Testing:** a scripted autoplay bot drives a real player build (virtual mouse); the latest run passed 74 checks with 0 errors, 60 fps under stress.

---

## 6. Likely deep-dive questions and answers
- **How many endings?** Three: Clear Channel, First Contact, Become the Signal.
- **How is the ending chosen?** Highest of three hidden trust scores (Hale, Iva, Static); ties go Iva then Hale. Each story reply adds or subtracts trust.
- **Can I see all endings in one run?** No; you can influence trust but only one ending plays. Rebroadcast to see the others.
- **What does Rebroadcast reset?** Main tree, Data, Antenna, trust, story. It keeps Echoes, endings seen, Deep Signal tree, settings.
- **What is the point of the Open Channel upgrade?** It unlocks the "talk to the Static" reply in story mail, which is the main way to build Static trust. Without it, the Static ending is much harder to reach.
- **Why are the Static's messages in symbols?** It is not human; the player *learns* it rather than reading it.
- **Is there combat?** No. Popping Static is framed as clearing interference and the Static just "rebuilds"; all text avoids combat wording.
- **How long is a run?** About 17 in-game days of 60 s shifts plus night screens.
- **What breaks the economy?** Economy is *not fully tuned*: a simulated greedy buyer reaches Antenna 10 at ~day 17 (the target), but real play was only checked with a bot on Days 1–2.

## 7. Known gaps (be honest when asked)
- Economy and pacing past the early days are simulated, not playtested.
- The Clear Channel Memory behaviour is my interpretation of your request ("have the thing pop up but say Hale handles it").
- Cutscene click-through and the Antenna details panel were implemented but I did not view them in screenshots.
- The night-time tutorial box's second line sits slightly under its Skip button.
- Nothing is committed to git.
