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
- **Injector case**: save the injectors in your injector case, cell by cell; empty cells are refilled. The template is bound to that specific case, so a second injector case (a loot one) is never touched. Injectors inside an injector case are not counted by the supplies.
- **Medicine to bring**: health points (healing kits), bandages, heavy bleed items, splints, surgery kits and Propital. The items of each row are found in the game's item database (modded ones too); reorder them or switch them off under "Items". What you carry counts, worse or used kits are swapped for better ones, the stash fills the rest.
- **Grenades and flares**: an amount per grenade type, a total topped up with random ones (what is about to be brought counts too), and red and green signal cartridges.
- **Cash**: roubles, euros and dollars, typed in, with the case they go into: Wallet, WZ wallet, Docs case or SICC case (carried or in the pouch), or loose. Added to a stack in the case when there is room, otherwise a part of the stack is split off.

- **Repair**: the armor you wear and, with its own switch, your weapons. Repair kits first (the game's own list of usable kits, least resource first, one after the other until the piece is whole; a kit is never run dry between two parts, which the server cannot take); a piece with no usable kit goes to a trader for roubles (the order of the traders is set in Setup, all or nothing). A kit that is there but too small is an error, so is not having the money. Pieces can be excluded from repairs (a quest hand-in): that list holds the very pieces, not their type.
- **Ammo**: fills the magazine in each equipped weapon (taken out, filled and put back, since the game does not load a magazine that sits in a weapon), puts one of your magazines into a weapon that has none, fills your spare magazines, chambers a round in a weapon with an empty chamber, and tops up the loose stacks of ammo you carry from the stash. It follows, it does not choose: the ammo a magazine already holds, else what the things around it hold (loose ammo in the same container, the nearest magazine, an equipped weapon that takes it; the order is set in Setup), never "the best". When there is nothing to follow, or the weapons that take a magazine disagree, it fills nothing and says so. Every line says what goes where, how much, why and from where.

- **Full auto**: puts the weapon in each slot you pick (sling, back, holster) on full auto, when it has that mode.
- **Treat injuries**: uses the medicine in the stash on whatever is hurt (HP, bleeding, fractures, destroyed parts), in your priority order and only with the items you leave switched on ("Only" keeps a single one, say Grizzly). It runs last, so what Get ready just brought is not used up. Off by default.

Each supply can also go on a **quick-slot key** (4 to 9, 0 or G): after getting ready the best one you carry is bound to it (pockets and tactical vest only, the game's rule). Under "Items" each item has a **Max**: with 700 HP wanted and a max of 1 AFAK, the rest is made up with other kits, and only if nothing else can, a second AFAK is brought (the plan line says so).

Every row has a "Goes to" list: click the places in the order you want them tried (Special slots, Pockets, Vest, Backpack, Pouch, and the cases for cash). Supplies fall back to pockets, vest and backpack when those are full; cash only goes where you pick.

## Quick actions

Hotkeys on a hovered item (F12 > GetReady > 2. Quick actions); their hints show in the item tooltip through CustomRpTweaks.Client:

- **T**, magazine top-up (stash and raid): fills the magazine by the same rules as the Ammo step.
- **R**, repair with a kit (stash): the same repair loop as the Repair step, kits only.

## Notes

- Moves are placed by looking for a free spot in the container's grids when the game's quick placement fails, and each move is checked to have left the stash.
- Needs the SPT 4.1.x client (Assembly-CSharp, BepInEx 5). MIT license.

## Not built yet

- Emptying loot into the stash, MergeConsumables support (combine stacks instead of swapping), buying what is missing from the flea market.
