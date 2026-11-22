# Plan: the first playable version

**Game:** Sealbound (working title)
**Engine:** Unity 6 (the long-term support version)
**Status:** Plan, 22 November 2026

This is the step-by-step plan for building the **first playable slice** described in section 14 of the [game design document](game-design.md): one Realm Force mission, *The Runestone Ruins*, with the Sword and Mage classes, Burrow Goblins, the **Bastion Golem** boss, golem drops, one craftable weapon per class, and a working **Golem soul merge**, played solo with up to 3 bot companions.

The goal is to answer one question: **is "hunt, seal, craft, merge" fun?** Everything else waits until the answer is yes.

---

## 1. Who does what

| Pytt | Claude |
|---|---|
| Installs the free tools on your PC (see [setup guide](setup-guide.md)). About one hour, once. | Writes all game code, game data, and setup. |
| Plays each new version and says what feels good or bad. | Fixes bugs and tunes the feel from your feedback. |
| Approves anything that costs money (art packs) or changes the game's direction. | Suggests options with a recommendation. |
| Opens Unity and presses Play to try things. | Works on your PC through the Claude app when Unity needs it, and saves every change to GitHub. |

You do not need to learn to code. When Claude needs you to click something in Unity, it will give numbered steps.

## 2. Milestones

Each milestone ends with something you can play. Rough times assume steady work and will change as we learn.

| # | Milestone | What you will be able to do | Rough time |
|---|---|---|---|
| 0 | **Setup** | Open the empty Sealbound project in Unity on your PC. | 1 to 2 days |
| 1 | **Move and look** | Run, jump, dodge and sprint a stand-in character through a test field with a smooth third-person camera. Keyboard, mouse and controller. | 1 week |
| 2 | **Sword combat** | Swing a sword in combos, dodge-cancel attacks, and hit training dummies with impact (small camera shake, hit pause). | 1 to 2 weeks |
| 3 | **Goblins** | Fight groups of Burrow Goblins that chase, surround and attack you. Health, damage and death work. | 1 week |
| 4 | **Mage** | Switch to the Mage class: a fire bolt, an area spell and a slowing trap. | 1 week |
| 5 | **Bastion Golem boss** | Fight the golem: slams, charges, a stomp, breakable arm runes, and a second phase. | 2 weeks |
| 6 | **Seal and drops** | Sealing the golem drops parts (Runestone Shards, Golem Core) and sometimes the Golem Spirit. An inventory shows what you have. | 1 week |
| 7 | **Forge** | Craft the Runestone Blade (Sword) or Golem Core Staff (Mage) from golem parts, and see it in your hands. | 1 week |
| 8 | **Soul merge** | Bind the Golem Spirit at the shrine, fill the Soul Gauge in a fight, and transform: stone skin, Rune Quake, Fortress Stance. | 2 weeks |
| 9 | **Bot companions** | Bring up to 3 computer-controlled Sealers who follow you, fight, and avoid the golem's big attacks. | 2 weeks |
| 10 | **Hub and mission** | A small square in Valdremor with a mission board, forge and soul shrine. Start the mission, finish it, come back with loot. | 1 to 2 weeks |
| 11 | **Polish and test** | Sound, effects, menus, a simple tutorial, and a version you can send to a few friends to try. | 2 weeks |

**Total:** roughly 4 to 5 months to a first version friends can play. The early milestones go faster; the boss, merge and bots take the most tuning.

## 3. Art for the first slice

Claude cannot make 3D models, so the first slice uses:

- **Stand-in shapes** (capsules and boxes) for milestones 1 to 4, so we can test the feel without spending money.
- **Ready-made art packs** from the Unity Asset Store from milestone 5 onward: a stylised fantasy hero, goblins, a stone golem (or a rock monster we can reshape), forest ruins, sword and staff models, and spell effects.

Before buying anything, Claude will put together a shortlist of specific packs with prices and a recommendation for you to approve. A rough budget for the first slice is **US$100 to $400**. Packs bought now can be swapped for custom art later, without changing the game's code.

## 4. Technical choices (for reference)

You don't need to understand these. They are written down so any future helper knows how the project is built.

- **Unity 6 LTS** with the **Universal Render Pipeline** (good for stylised looks and runs on most PCs).
- **Cinemachine** for the camera, the **Input System** for keyboard, mouse and controller.
- **Game data in data files** (Unity "ScriptableObjects"): every creature, drop, weapon and spirit is a data file, not hard-coded. Adding a new creature later means adding new data and art, not rewriting the game.
- **Unity's AI navigation** for goblins, the golem and bot companions.
- **Online play is not in this slice**, but the code keeps the player, bots and enemies separate so online co-op can be added in Slice 2 with Unity's networking tools.
- **Git and GitHub** keep every version of the project safe. Large art files use **Git LFS** (a GitHub feature for big files).

## 5. How we work week to week

1. Claude builds the next piece and saves it to GitHub.
2. Claude tells you in the project what's new and what to try.
3. You open Unity, press Play, and reply with what you think. Plain reactions like "the dodge feels slow" or "the golem is too easy" are exactly what's needed.
4. Repeat.

## 6. Risks and how we handle them

| Risk | How we handle it |
|---|---|
| Combat doesn't feel smooth enough | Movement and combat come first (milestones 1 and 2) and get the most tuning time. |
| Art packs don't match the *How To Train Your Dragon* style | We pick packs with a consistent stylised look and add a shared colour and lighting style. Custom art comes later. |
| The merge isn't exciting | The merge gets its own milestone with strong visual effects and big but short power, so it feels special. |
| The project gets too big | Anything not in section 14 of the design document waits for a later slice. |
