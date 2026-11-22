# Project G001: Game Design Document

**Working title:** *Sealbound* (to confirm with a name check before the Steam page)
**Owner:** Pytt
**Status:** Core decisions locked (22 November 2026)
**Last updated:** 22 November 2026

This document describes what the game is, how it plays, and what we build first. It is written in plain language on purpose. Pytt's core creative decisions are recorded in section 16.

---

## 1. The game in one paragraph

You are a recruit in **The Sealer**, the monster-hunting army of the most powerful kingdom in a medieval world full of magic, dragons, sea beasts and gods. You choose a branch: the **Sky Force**, the **Realm Force** or the **Marine Force**. Then you pick a fighting class, go on missions alone or with friends, and *seal* (defeat) mythical creatures. Sealed creatures drop body parts you craft into one-of-a-kind weapons. Very rarely, they drop their **spirit**. With a spirit, you can **soul merge**: for a short time you transform into a form that looks like the creature, with its strength and its signature powers. Everything you earn can then be tested against other players in PVP arenas.

## 2. What makes this game special (design pillars)

Every design decision should support at least one of these five pillars. If a feature supports none of them, we cut it.

1. **Hunt the legend.** Every creature is a memorable boss with its own look, behaviour and weak points. Fighting one should feel like an event.
2. **Wear your trophies.** What you seal becomes your gear and, with luck, your new form. Your character tells the story of what you have hunted.
3. **The right force for the right fight.** Sky, Realm and Marine each own a battlefield: the air, the ground and the deep. Bringing the right team matters.
4. **Fluid, fast combat.** Movement flows smoothly from running to dodging to attacking to flying or swimming. Controls feel responsive and light.
5. **Prove it in the arena.** PVP gives players a reason to chase better gear, study what is strongest right now, and practise.

## 3. Big decisions already made

| Decision | Choice | Why, in plain words |
|---|---|---|
| View | 3D, third person (camera behind the character) | Air fights and underwater fights need real 3D space. Third person also shows off the merge transformations. |
| Platform | PC, sold on Steam | A fast 3D game with online co-op and PVP is too heavy to run well in a web browser. Steam is where PC players buy games. |
| Art style | Stylised 3D, inspired by the feel of *How To Train Your Dragon* | Bold shapes, expressive faces, rich colour, exaggerated creature designs. It looks great without needing a huge studio, and it leaves room for wild creature ideas. We match the *feel*, not that film's actual characters or designs. |
| Game engine | **Unity** (see section 13) | The engine is the ready-made toolkit games are built in. Unity has the largest shop of ready-made 3D characters, creatures and animations, which matters because Claude writes code but does not make 3D models. |
| Modes | Solo with bots, online co-op, PVP | As described by Pytt. |
| First goal | A small playable slice (section 14) | The full game is studio-sized. We prove the fun in one small piece first, then grow it. |

---

## 4. The world

### 4.1 Setting

A medieval world with castles, swords, sailing ships and candlelight, with almost no modern technology. Magic is real and common. The world sits beside many other **realms**, other worlds that press against ours like rooms behind thin walls. Sometimes the walls crack, and things come through.

### 4.2 Kingdoms

There are several kingdoms. Each has its own Sealer army. The story takes place in the strongest, richest one.

- **The Kingdom of Valdremor.** A proud, wealthy kingdom whose Sealer army is the best in the world. Its capital, also called Valdremor, is the players' home base (the "hub"): a walled city with the Sealer academy, forges, a soul shrine and an arena.
- **Rival and neighbour kingdoms** (to be designed later). They appear in the story as allies, rivals, or places that need help. Their Sealers can appear as characters, but players always belong to the main kingdom.

### 4.3 What is a "seal"?

Creatures from other realms cannot be fully killed in our world. When a Sealer defeats one, they **seal** it: the creature's body breaks apart and its essence is locked away. This is why defeated creatures leave behind parts and, sometimes, a spirit. It also gives the army its name.

### 4.4 The realms

Creatures come from these realms. The Realm Force also travels *into* them to stop breaches at the source.

| Realm | What it is | Example creatures |
|---|---|---|
| **The Mortal Realm** | Our world: forests, mountains, caves, ruins | Goblins, giant spiders, giant serpents, trolls, stone golems |
| **The Underworlds** | Fiery and shadowy realms below. Based on public-domain myths of the underworld (Greek, Norse, Mesopotamian and others) | Hellhounds, wraiths, fire demons, the restless dead |
| **The Storybound Realms** | Worlds born from the stories humans tell. When enough people believe a tale, it becomes a real place, with its own heroes and monsters. *(See note below.)* | Original creations only: wizard-academy wardens, desert sand-leviathans, giant colossi |
| **The Divine Realms** | Homes of old gods, demigods and the creatures of faith | Demigods, fallen gods, temple guardians, holy beasts turned monstrous |
| **The Sky Above** | The upper air, storm clouds, floating islands | Dragons, wyverns, rocs, thunderbirds, storm spirits |
| **The Deep Below** | The sea, trenches, drowned cities | Krakens, sea serpents, ancient sea beasts, things that make strange sounds in the deep |

> **Note on the Storybound Realms.** Pytt's idea includes realms based on anime and books like *Attack on Titan*, *Harry Potter* and *Dune*. We cannot put those characters, places or creatures into the game, because they are owned by their creators, and using them could get the game removed from Steam or bring legal trouble. The Storybound Realms keep the *spirit* of the idea, "worlds made from fiction", but everything in them is our own invention. For example, instead of *Attack on Titan* titans, we have our own **Colossi**; instead of *Dune*'s sandworms, our own **Dune Leviathan**. The same goes for internet horror stories ("creepypasta"): many famous ones are owned by someone, so we create our own deep-sea horrors in that style.

### 4.5 Religion, gods and faith-based creatures

Pytt wants religions portrayed as creatures and monsters. This is a powerful theme, and it needs care so the game does not offend real players or get banned in some countries. The approach:

- **Ancient mythologies that are no longer widely practised** (Greek, Norse, Egyptian, Mesopotamian, Celtic and similar) are fair game. Many popular games use them. Their gods, demigods and monsters can be bosses.
- **Living world religions** (Christianity, Islam, Judaism, Hinduism, Buddhism, Sikhism and others still practised by many people today) are **not** turned into monsters. We do not use their holy figures, prophets or gods as enemies.
- **The game's own invented faiths.** The world has its own religions, temples and gods. These can be as dark as we like: a god born from the fear of its worshippers, a church whose prayers summon a monster, a faith that *becomes* a creature when it grows too strong. This delivers Pytt's "religion as a monster" idea fully, with no real-world believers to offend.

---

## 5. The Sealer and its three forces

Every recruit trains at the Sealer academy. At the end of training (the tutorial), they choose one of three forces, much like trainees choosing a military branch. Each character keeps one main force. Later, they can train a second force through a special questline.

### 5.1 The Sky Force

*"We fight where the dragons fly."*

- **Handles:** dragons, wyverns, giant birds, storm spirits, flying beasts.
- **Battlefield:** mid-air, cliffs, storm clouds, floating islands.
- **Special movement:** the **Skyrig**, a wing harness powered by wind magic. It lets Sky Force soldiers glide, dive, boost upward and hover for short moments. At higher ranks they can also ride a tamed flying mount, such as a young dragon or a griffin (added after the first slices).
- **Feel:** fast, dramatic, three-dimensional. Dodging means rolling through the air.

### 5.2 The Realm Force

*"We hold the line, here and beyond."*

- **Handles:** ground creatures in our world, and anything trying to break out of the other realms. Goblins, golems, giant spiders and serpents, colossi, demigods, and, at the very top, gods.
- **Battlefield:** forests, caves, ruins, the Underworlds, the Storybound and Divine Realms.
- **Special movement:** the **Realm Anchor**, a magical tether that lets them grapple to walls and big creatures, climb a colossus, and resist being thrown or knocked down.
- **Feel:** grounded and heavy hitting. The most varied force, because it visits the most places.

### 5.3 The Marine Force

*"What the deep hides, we seal."*

- **Handles:** krakens, sea serpents, ancient sea beasts, mythical sea creatures, deep-sea horrors.
- **Battlefield:** underwater, sunken ships, drowned cities, deep trenches, and ship decks during surface fights.
- **Special movement:** the **Tidecharm**, an enchanted amulet that lets them breathe underwater, swim fast in any direction, and use bursts of water pressure to dash.
- **Feel:** mysterious and tense. Darkness, pressure, and huge shapes moving below you.

### 5.4 Fighting outside your force ("Terrain fit")

Anyone can join any mission, so mixed teams can play together. But each force is built for its own battlefield, so the others struggle.

| Your force | In the air | On the ground | Underwater |
|---|---|---|---|
| **Sky** | Full power | Normal, but slower to recover from knockdowns | Limited air, slow swimming, weaker attacks |
| **Realm** | Can only glide short distances with a basic kit | Full power | Limited air, slow swimming |
| **Marine** | Can only glide short distances with a basic kit | Normal, but slower on land | Full power |

How it works in play:

- A player outside their home terrain gets a visible **"Off-terrain" penalty**, for example less damage and slower movement, and in water a breath meter.
- Basic gear from other forces (a training glider, a basic breathing charm) reduces the penalty but never removes it.
- Missions show a **recommended force**. Teams that match get a reward bonus. Mixed teams are allowed and still fun, just harder.

Exact numbers will be tuned once we can play it.

---

## 6. Classes

Within each force, players choose a class. Classes are the same in every force, but each force gives them a twist so a Sky Force Mage plays differently from a Marine Force Mage.

| Class | Role | What it does |
|---|---|---|
| **Sword** | Close-range damage | Fast combos, parries and finishing moves. The easiest to learn. |
| **Shield** | Protector | Blocks big hits, draws the creature's attention, protects allies. |
| **Mage** | Magic damage and control | Area spells, slows, traps, elemental weak points. |
| **Ranger** | Long-range damage | Bows and crossbows. Hits weak spots from a distance. |
| **Monk** | Agile fighter and support | Unarmed or staff combat, fast movement, small heals and buffs. |
| **Cleric** *(added after the first slice)* | Healer | Heals and protects the team. Uses the game's invented faiths. |

**Force twists (examples):**

- **Sky Sword:** dive attacks from above, aerial combos.
- **Realm Shield:** plants the shield like a wall, anchors to stop a giant's charge.
- **Marine Ranger:** harpoon guns and spear-crossbows that pull creatures closer.
- **Sky Mage:** wind and lightning. **Realm Mage:** earth and fire. **Marine Mage:** water and ice.

For the first playable slice we build only **Sword** and **Mage** (section 14).

---

## 7. Creatures

### 7.1 Creature design rules

1. **Original or public domain only.** Myths, legends and folklore that belong to everyone (dragons, krakens, golems, Greek monsters, Norse giants) are fine. Characters owned by a company or author are not.
2. **Big silhouettes.** Every creature should be recognisable from its shadow alone.
3. **Readable attacks.** Each attack has a clear wind-up so skilled players can react.
4. **Weak points.** Every big creature has parts that can be broken (a horn, a wing, a tentacle). Breaking parts changes the fight and improves the drops.
5. **Tiered danger.** Creatures are ranked by tier, from common to god-level.

### 7.2 Creature tiers

| Tier | Name | Example | Spirit drop chance (starting idea) |
|---|---|---|---|
| 1 | Common | Goblin, giant spider | No spirit |
| 2 | Elite | Troll, wyvern, sea serpent | Very low (about 1%) |
| 3 | Great Beast | Stone golem, roc, young kraken | Low (about 3%) |
| 4 | Legend | Elder dragon, colossus, abyssal kraken | Low (about 2%) |
| 5 | Mythic | Demigod, ancient leviathan | Very low (about 1%) |
| 6 | Divine | A god of the invented faiths | Event-only, special rules |

Drop chances are starting points to tune later. A **"pity" system** guarantees a spirit after a long run of bad luck, so no one feels cheated (see 8.3).

### 7.3 Starter creature roster (examples)

**Sky Force**
- **Emberwing Wyvern** (Elite): a fast, fire-spitting wyvern that hunts in pairs.
- **Stormcrown Roc** (Great Beast): a giant bird whose wingbeats create gusts that throw players off course.
- **Ashmaw, the Elder Dragon** (Legend): an ancient dragon whose scales glow like cooling lava.

**Realm Force**
- **Burrow Goblins** (Common): small, sneaky, in groups.
- **Weaver Matriarch** (Elite): a giant spider that fills caves with web traps.
- **Bastion Golem** (Great Beast): a walking fortress of carved stone and old runes. **First playable slice boss.**
- **The Hollow Colossus** (Legend, Storybound Realm): a giant made from a forgotten war story, slow but enormous, climbed with the Realm Anchor. *Our own original giant.*
- **Dune Leviathan** (Legend, Storybound Realm): a vast sand creature that swims through desert dunes. *Our own original design.*

**Marine Force**
- **Reef Lurker** (Common): an eel-like ambusher.
- **Tidecoil Serpent** (Elite): a sea serpent that wraps around ships.
- **The Deep Kraken** (Legend): a classic kraken, inspired by Norse sea legends.
- **The Low Hum** (Mythic): a creature so large you only hear it at first, a deep sound from the trench. *Inspired by real-world mystery sounds from the ocean, as an original creature.*

**Divine and Underworld**
- **Gatehound of the Underworld** (Great Beast): a three-headed hound, inspired by Greek myth.
- **Fallen Demigod** (Mythic): a hero of an invented faith, twisted by the worship of its own followers.

---

## 8. Drops, crafting and soul merging

This is the heart of the game.

### 8.1 Drops

When a creature is sealed, it leaves:

- **Body parts:** scales, fangs, horns, cores, ink, feathers. Breaking weak points during the fight increases the number and quality of parts.
- **Essence:** a common crafting currency from every creature.
- **Spirit** (rare): the creature's soul, used for soul merging.

Parts have rarity levels: **Common, Fine, Rare, Epic, Legendary**.

### 8.2 Weapon crafting

At the kingdom's **forge**, players turn parts into gear.

- Each creature unlocks its own **weapon set** for every class. A Bastion Golem sword looks like carved runestone; an Emberwing bow glows with ember veins.
- Creature weapons carry a small piece of the creature's power, for example "Golem sword: hits break enemy armour".
- Weapons can be **upgraded** with more parts from the same creature, so players hunt favourite creatures again.
- Armour sets follow the same idea. Weapons come first; creature armour is added later.

### 8.3 Soul merging (the signature feature)

If a player obtains a creature's **spirit**, they can bind it at the **soul shrine**. In battle, they can then activate **Soul Merge**.

**What happens:**
- The player's character transforms into a form that resembles the creature: a Golem merge gives stone skin and glowing runes; a Kraken merge gives tentacles and sea-dark skin; a Dragon merge gives wings, horns and a fiery glow.
- Their stats rise, and they get **2 or 3 signature skills or passives** unique to that creature.
- The stronger the creature, the stronger the merge.

**Limits:**
- **Duration:** 30 to 60 seconds, depending on the creature's tier.
- **Soul Gauge:** instead of a fixed cooldown, a gauge fills as you fight (dealing damage, blocking, breaking weak points). When full, you can merge. This rewards active play.
- A player can **bind one spirit at a time** for battle, but can own many and swap between missions.
- Merges have their own levels. Using a merge often makes it stronger and can unlock a third skill.

**Example: Bastion Golem merge**
- **Look:** stone plates over the body, runes glowing on the arms, heavier footsteps.
- **Stats:** large boost to defence and health, small boost to attack, slower movement.
- **Skills:**
  1. *Rune Quake:* slam the ground and stun nearby enemies.
  2. *Fortress Stance (passive):* cannot be knocked down while merged.
  3. *Unbroken (unlocked at higher merge level):* survive one deadly hit with 1 health.

**Example: Emberwing Wyvern merge (Sky)**
- **Look:** leathery wings, ember-lit scales, a tail.
- **Skills:** a fire breath cone; a fast dive strike; passive: faster flight.

**Example: Deep Kraken merge (Marine)**
- **Look:** tentacles from the back, dark skin, glowing eyes.
- **Skills:** grab and pull a target; an ink cloud that blinds enemies; passive: faster swimming and attacks hit multiple targets.

**Fairness rules for rare spirits:**
- **Pity counter:** each failed attempt increases the next chance; after a set number of hunts, a spirit is guaranteed.
- **Spirit shards:** duplicate or failed drops give shards; enough shards craft the spirit.
- **No buying spirits with real money.** It would make PVP unfair and damage trust in the game (section 12).

---

## 9. Game modes

### 9.1 Story mode

A campaign told through missions and short scenes. It can be played solo with bots or in co-op.

**Story outline (first draft, for Pytt to shape):**
- **Act 1: The Recruit.** Training at the academy, choosing a force, first missions in the Mortal Realm. Breaches are growing more frequent, and nobody knows why.
- **Act 2: The Cracks.** Missions take the Sealers into the other realms. The breaches seem to be caused on purpose, by someone inside the kingdom.
- **Act 3: The Faith Beast.** One of the kingdom's own faiths has grown so powerful that its god is becoming real, and it is pulling the realms open. The Sealers must decide whether to seal a god.

Each force gets its own story missions in its own battlefield, and a shared final act where all three forces fight together.

### 9.2 Mission mode (hunts)

Repeatable missions chosen from a **mission board** in the hub.
- Each shows the creature, its tier, the battlefield and the recommended force.
- Missions have difficulty levels; higher difficulty means better drops and higher spirit chances.
- Extra goals ("break both wings", "finish in under 10 minutes") give bonus rewards.

### 9.3 Co-op and bots

- A party holds **up to 4 players**.
- Empty slots are filled by **bot companions**: Sealer squadmates controlled by the computer, each with a class and simple tactics. Players can recruit and upgrade favourite bot companions.
- Solo players can always finish everything with bots. Nothing is locked behind having friends online.

### 9.4 PVP arena

Players fight each other to prove their builds and skill.
- **Formats:** 1v1, 2v2, 3v3 and full squad (4v4, matching the party size).
- **Arenas:** small maps with mixed terrain, some with air space or water so every force has a chance to shine.
- **Two arena types:**
  - **Ranked:** stats are levelled out to keep it fair; your choices (weapon type, merge, skills) still matter, but long playtime alone does not win. Merges are allowed with a shorter duration.
  - **Open (unranked):** full stats, anything goes. This is where players show off their best gear.
- **Seasons:** every few months, with rewards and changes to keep the strongest builds (the "meta") fresh.

---

## 10. Combat and movement feel

Pytt wants combat that is smooth and fairly fast. Goals:

- **Fluid transitions.** Running, dodging, attacking, jumping, gliding and swimming flow into each other with no awkward stops. Attacks can be cancelled into a dodge.
- **Light, responsive controls.** Pressing a button gets an instant reaction.
- **Lock-on camera** for big creatures, which can be turned off.
- **Stamina** for dodging and sprinting, so fights have rhythm.
- **Weight and impact.** Big hits shake the camera slightly and freeze the action for a split second, so they feel powerful.
- **Controls:** mouse and keyboard first, with full controller support.

## 11. Art and sound direction

- **Look:** stylised 3D in the spirit of *How To Train Your Dragon*: bold, slightly exaggerated shapes, expressive characters, warm lighting, painterly skies, rich colour. Creatures can be cute, majestic or terrifying, but always full of personality. (We take inspiration from the *style*, but all designs are our own.)
- **Characters:** customisable Sealer recruits with uniforms that show their force: wind-blue and silver for Sky, earth-brown and iron for Realm, sea-teal and bronze for Marine.
- **Creature art:** each creature gets a concept sheet (front, side, back, weak points, merge form).
- **Merge transformations:** a short, flashy transformation effect, readable in a fight.
- **Music:** orchestral and folk, with themes per force and per legendary creature.

**Where the art comes from.** Claude writes the game's code, design and text, and can create concept descriptions. It does not make finished 3D models. Our plan:
1. **Prototype stage:** ready-made art packs from the Unity Asset Store, plus simple shapes.
2. **Later:** hire freelance 3D artists (for example through ArtStation or Fiverr) for the signature creatures and merge forms, and possibly use AI tools to help with early concept images.

## 12. Business model (starting recommendation)

- **Buy once:** a one-time price on Steam.
- **Optional extras:** cosmetic items (outfits, weapon skins, effects) and later expansion packs with new realms and creatures.
- **Never sell power:** no buying spirits, parts or stat boosts. Since PVP is a big draw, selling power would drive players away.

## 13. Technology, explained simply

- **Game engine:** an engine is the base software that games are built inside. It already handles graphics, physics, sound and controls, so we only build what is unique to our game. Think of it like a car chassis: we design the body and interior, but we do not reinvent the wheels.
- **Recommendation: Unity.** It is free to start (you only pay if the game earns a large amount of money), it is very common for stylised 3D games, it has proven tools for online multiplayer, and it has the biggest shop of ready-made 3D characters, creatures and animations. That last point matters most, because Claude can write code but cannot make 3D models.
- **Alternative: Godot.** Fully free and open, but its 3D and online tools are less mature, and it has far fewer ready-made art packs. A good engine, but a harder road for this game.
- **Online play:** at first, co-op is hosted by one player's computer (cheap and simple). Later, PVP needs our own servers so nobody can cheat, which costs money each month.
- **What Pytt needs later:** a Windows or Mac PC capable of running Unity, and the free Unity Hub app installed. Claude can guide that step by step when the time comes.

## 14. First playable slice

The goal: prove the core loop is fun, *hunt → seal → craft → merge*, before building anything big.

**Contents:**
- One **Realm Force** mission: *"The Runestone Ruins"*. A forest ruin with Burrow Goblins as warm-up enemies, ending in a boss fight against the **Bastion Golem**.
- Two classes: **Sword** and **Mage**.
- Solo play with **up to 3 bot companions**.
- One creature with full drops: Golem parts, one craftable Golem weapon per class, and the **Golem spirit** with a working soul merge.
- A tiny hub: a mission board, a forge and a soul shrine.
- Placeholder art from asset packs.

**Not included yet:** Sky and Marine forces, online co-op, PVP, story scenes, other kingdoms.

**Success test:** players want to replay the golem fight to craft the weapon, and the merge makes them grin.

## 15. Roadmap (rough order)

1. **Slice 1:** the first playable slice above.
2. **Slice 2:** add online co-op for the same mission.
3. **Slice 3:** add the **Sky Force** with one flying creature and air combat.
4. **Slice 4:** add the **Marine Force** with one underwater creature.
5. **Slice 5:** first PVP arena (1v1 and 2v2).
6. **Expand:** more classes, creatures, realms, the story campaign, and full squad PVP.
7. **Steam "Coming Soon" page and playtests** with real players.
8. **Early Access launch** on Steam, then keep growing the game.

---

## 16. Decisions locked by Pytt

Pytt approved all of these on 22 November 2026. Change them only with Pytt's say-so.

| # | Question | Decision |
|---|---|---|
| 1 | What is the main kingdom called? | **Valdremor.** |
| 2 | How many players in one party? | **4.** Bots fill empty slots. Full squad PVP is 4v4. |
| 3 | What is the game called? | **Sealbound** (working title). We check the name is free on Steam and online before the Steam page goes up. *The Sealers* is the backup. |
| 4 | Can a player change force after choosing? | **One main force per character**, and a second force can be trained later through a special questline. |
| 5 | Is there a Cleric (healer) class? | **Yes**, added after the first slice. |
| 6 | Does the Sky Force ride mounts? | **Yes, at higher ranks**, on top of the Skyrig. Not in the first slices. |
| 7 | Does crafting make armour too? | **Both weapons and armour**, weapons first. |
| 8 | Merge limit: fixed cooldown or Soul Gauge? | **Soul Gauge** that fills as you fight. |
| 9 | Are merges allowed in ranked PVP? | **Yes**, with a shorter duration and levelled stats. |
| 10 | How are religions handled? | **Ancient myths and the game's own invented faiths only.** Living religions are never portrayed as monsters. |
