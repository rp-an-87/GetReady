# GetReady

BepInEx client plugin for SPT 4.1.x: a "Get ready for raid" button and window in the stash screen (hideout and main menu). It reads your stash and what you carry, works out what is missing and moves it with one click.

- **Click** the button above the pin button of your stash: everything is brought at once. A sound and a short notification say it went well; if something is wrong, a red box lists it, with the item's icon.
- **Ctrl+click** opens the window. Esc or leaving the stash screen closes it.
- The button has a count on its corner (red: problems, gold: things to bring) and its tooltip lists the status.

Settings and templates are kept in `BepInEx\config\GetReady\settings.json` and edited in the window (F12 only has "show button" and an optional hotkey).

## The window

- **Ready**: what each step found, as progress bars (carried against wanted), what will be brought and what is wrong.
- **Setup**: what to bring and where. It follows your stash and gear live.

## Steps

- **Special slots**: save what the special slots of your pockets hold; missing items are brought, a partly used kit (CMS 2/3) is swapped for a fuller one.
- **Injector case**: save the injectors in your injector case, cell by cell; empty cells are refilled. The template is bound to that specific case, so a second injector case (a loot one) is never touched. Injectors inside an injector case are not counted by the supplies.
- **Healing**: health points (healing kits), bandages, heavy bleed items, splints, surgery kits and Propital. The items of each row are found in the game's item database (modded ones too); reorder them or switch them off under "Items". What you carry counts, worse or used kits are swapped for better ones, the stash fills the rest.
- **Grenades and flares**: an amount per grenade type, a total topped up with random ones (what is about to be brought counts too), and red and green signal cartridges.
- **Cash**: roubles, euros and dollars, typed in, with the case they go into: Wallet, WZ wallet, Docs case or SICC case (carried or in the pouch), or loose. Added to a stack in the case when there is room, otherwise a part of the stack is split off.

Every row has a "Goes to" list: click the places in the order you want them tried (Special slots, Pockets, Vest, Backpack, Pouch, and the cases for cash). Supplies fall back to pockets, vest and backpack when those are full; cash only goes where you pick.

## Notes

- Moves are placed by looking for a free spot in the container's grids when the game's quick placement fails, and each move is checked to have left the stash.
- Needs the SPT 4.1.x client (Assembly-CSharp, BepInEx 5). MIT license.

Planned: ammo and magazine top-up, repair, emptying loot into the stash, MergeConsumables support, buying what is missing from the flea market.
