# GetReady

BepInEx client plugin for SPT 4.1.x: a "Get ready for raid" window in the hideout and main menu. It reads your stash and what you carry, shows a plan of what is missing, and moves it with one button.

Open it with the blue button above the pin button of your stash (the same place as the old kit button; it can be turned off in F12), or with an optional hotkey. Settings and templates are kept in `BepInEx\config\GetReady\settings.json` and edited in the window.

## Steps (0.1.0)

- **Special slots**: save what your pockets' special slots hold (CMS kits), and the missing ones are brought from the stash. In `settings.json` a slot can list a fallback item after the first.
- **Injector case**: capture the injectors in your injector case, cell by cell, and the empty cells are refilled with the same injector. The template is bound to that specific case, so a second injector case (a loot one, say) is never touched.
(flares and other special-slot items are part of the special slots template: put them in the slot and save)

Planned: meds by health points, grenades, ammo and magazine top-up, repair, emptying loot into the stash, MergeConsumables support, buying what is missing from the flea market.

## Status

Builds; untested in game.
