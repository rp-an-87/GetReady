# GetReady

BepInEx client plugin for SPT 4.1.x: a "Get ready for raid" window in the hideout and main menu. It reads your stash and what you carry, shows a plan of what is missing, and moves it with one button.

Open it with the blue button above the pin button of your stash (the same place as the old kit button; it can be turned off in F12), or with an optional hotkey. Settings and templates are kept in `BepInEx\config\GetReady\settings.json` and edited in the window.

## Steps 

- **Special slots**: save what your pockets' special slots hold (CMS kits), and the missing ones are brought from the stash. In `settings.json` a slot can list a fallback item after the first.
- **Injector case**: capture the injectors in your injector case, cell by cell, and the empty cells are refilled with the same injector. The template is bound to that specific case, so a second injector case (a loot one, say) is never touched.
(flares and other special-slot items are part of the special slots template: put them in the slot and save)

- **Healing**: a target for healing kits (health points, AFAK > Salewa > IFAK > Car > AI-2), bandages, heavy bleed items and Propital. What you carry counts towards it, worse or used kits are swapped for better ones, and the stash fills the rest. Each row has its own destination (pockets, vest, backpack, in the order you pick).
- **Grenades**: an amount per grenade type, plus a total that is topped up with random ones.

The button: a click gets you ready right away, Ctrl+click opens the window.

Planned: ammo and magazine top-up, repair, emptying loot into the stash, MergeConsumables support, buying what is missing from the flea market.

## Status

Builds; untested in game.
