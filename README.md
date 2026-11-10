# GetReady

BepInEx client plugin for SPT 4.1.x: a "Get ready for raid" button and window in the stash screen (hideout and main menu). It reads your stash and what you carry, works out what is missing and moves it with one click.

- **Click** the button above the pin button of your stash: everything is brought at once. A sound and a short notification say it went well; if something is wrong, a red box lists it, with the item's icon.
- **Ctrl+click** opens the window. Esc or leaving the stash screen closes it.
- The button has a count on its corner (red: problems, gold: things to bring) and its tooltip lists the status.

Settings and templates are kept in `BepInEx\config\GetReady\profiles\<profile id>.json` (one file per profile) and edited in the window (F12 only has "show button" and an optional hotkey).

## The window

- **Ready**: what each step found, as progress bars (carried against wanted), what will be brought and what is wrong.
- **Setup**: what to bring and where. It follows your stash and gear live.

## Steps

- **Special slots**: save what the special slots of your pockets hold; missing items are brought, a partly used kit (CMS 2/3) is swapped for a fuller one.
- **Cases** (the injector case is one of them, see below): templates of what a case holds.
- **Medicine to bring**: health points (healing kits), bandages, heavy bleed items, splints, surgery kits and Propital. The items of each row are found in the game's item database (modded ones too); reorder them or switch them off under "Items". What you carry counts, worse or used kits are swapped for better ones, the stash fills the rest.
- **Grenades and flares**: an amount per grenade type, a total topped up with random ones (what is about to be brought counts too), and red and green signal cartridges.
- **Cash**: roubles, euros and dollars, typed in, brought loose to pockets, vest, backpack or pouch (the order is yours). Money you keep in a case is set up with the case (see Cases), and a case that stops cash, or has money recorded, turns this step off while you carry it.

- **Repair**: the armor you wear and, with its own switch, your weapons. Repair kits first (the game's own list of usable kits, least resource first, one after the other until the piece is whole; a kit is never run dry between two parts, which the server cannot take); a piece with no usable kit goes to a trader for roubles (the order of the traders is set in Setup, all or nothing). A kit that is there but too small is an error, so is not having the money. Pieces can be excluded from repairs (a quest hand-in): that list holds the very pieces, not their type.
- **Ammo**: fills the magazine in each equipped weapon (taken out, filled and put back, since the game does not load a magazine that sits in a weapon), puts one of your magazines into a weapon that has none, fills your spare magazines, chambers a round in a weapon with an empty chamber, and tops up the loose stacks of ammo you carry from the stash. It follows, it does not choose: the ammo a magazine already holds, else what the things around it hold (loose ammo in the same container, the nearest magazine, an equipped weapon that takes it; the order is set in Setup), never "the best". When there is nothing to follow, or the weapons that take a magazine disagree, it fills nothing and says so. Every line says what goes where, how much, why and from where.

- **Full auto**: puts the weapon in each slot you pick (sling, back, holster) on full auto, when it has that mode.
- **Treat injuries**: uses the medicine in the stash on whatever is hurt (HP, bleeding, fractures, destroyed parts), in your priority order and only with the items you leave switched on ("Only" keeps a single one, say Grizzly). It runs last, so what Get ready just brought is not used up. Off by default.

Each supply can also go on a **quick-slot key** (4 to 9, 0 or G): after getting ready the best one you carry is bound to it (pockets and tactical vest only, the game's rule). Under "Items" each item has a **Max**: with 700 HP wanted and a max of 1 AFAK, the rest is made up with other kits, and only if nothing else can, a second AFAK is brought (the plan line says so).

Every row has a "Goes to" list: click the places in the order you want them tried (Special slots, Pockets, Vest, Backpack, Pouch, and the cases for cash). Supplies fall back to pockets, vest and backpack when those are full; cash only goes where you pick.

## Cases

Setup > Cases lists every container you carry (a SICC, a docs case, a keytool, a wallet, an injector case) and what you set up for each. A case is that very container (not its type), so a second SICC is left alone until you set it up.

- **Track items inside / Update tracked items**: remembers what the case holds now: keys, keycards, other items and money, and where each item sits. Get ready then brings back what is missing from the stash (an item the stash does not have is a problem, in red). A container inside a case (a card holder) is an item like any other for the case around it, and tracks its own. **Stop tracking items** drops it (nothing moves).
- **Take out the rest** (on by default): what is not tracked goes out to the stash, money too: a tracked amount keeps that much and the extra goes out, a currency that is not tracked goes out whole. Off: only what is missing is brought.
- **Exact positions**: each item goes back to the cell it was in when tracked and a wrong item in a cell goes to the stash (what the injector case needs). Off: only the type and the count matter.
- **Money** in a case is topped up to the tracked amount, shown as the game's stack icon.
- **Skip cash step**: while you carry this case the Cash step does nothing. Money a case holds counts toward the Cash step's target and the step brings only what is still missing; money a case gives up (Take out the rest) can be brought on from the stash in the same run.
- **Maps**: the maps the case is for. On the map screen a red bar over the screen shows, as icons, the cases you carry that are for other maps ("wrong containers for this map!") and the cases set for the picked map that are not on you ("Missing"). A case with no maps is for any; scav raids are skipped.
- **Cards**: a case you carry has a card you can collapse with the "-" button (it stays collapsed until you open it, whatever you do with the case). A saved case you are not carrying is only a line; it is set up when you carry it. The **x** clears everything saved for a case (what it tracks, its maps and Skip cash step), and the case leaves the list.
- "Take out the rest" and "Exact positions" only change how the tracked items are used; they never track the case again.
- **Keep tracked items up to date**: items you put in or take out of a case you track, by hand in the stash, change what is tracked. What Get ready itself moves is never tracked, and neither are cases you never tracked.
- Tagging or untagging a case updates it everywhere.

## Quick actions

Hotkeys on a hovered item (F12 > GetReady > 2. Quick actions); their hints show in the item tooltip through CustomRpTweaks.Client:

- **T**, magazine top-up (stash and raid): fills the magazine by the same rules as the Ammo step.
- **R**, repair with a kit (stash): the same repair loop as the Repair step, kits only.

## Notes

- Moves are placed by looking for a free spot in the container's grids when the game's quick placement fails, and each move is checked to have left the stash.
- Needs the SPT 4.1.x client (Assembly-CSharp, BepInEx 5).
## Not built yet

- Emptying loot into the stash, MergeConsumables support (combine stacks instead of swapping), buying what is missing from the flea market.

## License and support

GPL-3.0-or-later, see `LICENSE`. Anyone who distributes this mod or a modified version, free or paid, must release the full source under the same license and keep the copyright notice. The name "GetReady" and the author's name are not licensed: rename your fork.

Made for my own use and shared as is. No support, no feature requests. Use it as it is or modify it. Written with AI assistance (Claude). Tested on SPT 4.1.6 only.
