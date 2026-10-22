# Axe Path — design (Unity slice)

## Premise

You are no one, in a village at the foot of a hill forest no peasant
crosses. Somewhere up there, past the treeline smoke, a dwarf smith
lives alone in a cave he cut himself. Nobody teaches you anything. The
library hints. The forest tests. The dwarf watches your edge, not your
words. This slice is one complete path: the axe.

## Pillars

- **No one tells you how.** No tutorials, markers, compass, or quest log.
  Knowledge lives in books, marks, NPC lines, and attention.
- **Books hint, never grant.** The route sketch helps; it does not
  complete anything. Written things can be stale, wrong, or lying.
- **Trust, not stats.** The dwarf thaws when he sees a honestly dulled
  axe on someone who walked here — never from a points total.
- **Death keeps the world.** Generations turn over; the valley keeps its
  changes. Kill the dwarf and his knowledge is gone for good.
- **Suggestive, never explicit.** Allure is part of the art direction;
  explicit scenes are out. Catholic-values-friendly throughout.

## The v1 loop

1. Wake in the village. Take the lumber axe from the yard.
2. Take the route sketch at the book nook (the book stays with the
   librarian). Hear the route: pines, split rock, smoke.
3. Climb the 40m forest ramp. Chop pines to learn the swing — every chop
   dulls the edge (9/chop, 6/wolf-hit; 0–100).
4. Mind the wolves (hear walking at 30u, running at 90u; leash at
   150u): three at the den west of the path, two ranging the forest.
   Three wounds kill you. A timed hop clears a lunge near its apex.
5. Reach the unmarked cave. Cold dwarf while your edge is keen.
6. Past 40 dullness, talk again: arrival, thaw to civil. He names the
   price: one ironwood log, past the cave to the east.
7. Find the weathered guide mark on the wall the path walks into, turn
   east, take the fallen log from the lit grove (E within 5u).
8. Turn it in: the edge comes back sharp, permanently better at holding
   (20% dulling thereafter). Loop complete; the forest remains.

## The valley (north = -Z, cave ≈ (0,-280))

Village flats (h 0): spawn, lumber yard, book-nook library, two
huts — Riverwood-style, on the west bank of a river that runs the
whole valley south. A lumber mill with a water wheel works the bank;
a bridge crosses to a cold camp on the east side.
Forest ramp (40m climb): 1450 seeded pines, oaks, and birches, wolf
pack west of the path, a winding path with one landmark per leg
(three pines, split rock, rune waystone), axe blazes along the path,
approach rocks/pines, flower drifts and berry bushes throughout.
Cave mountain (h 40): the dwarf's hall cut into a seven-cone massif —
stone arch mouth with ember glow, forge + anvil + smoke inside,
dwarf at (-4,-312). Ironwood grove east past the cave: pale
shimmering trunks, dark crowns, one fallen log under cool light.
Backdrop: three snow-capped ranges beyond the north bound, night sky
with moon, Milky Way, and drifting cloud banks, exp-fogged treeline.
Weather: one global gust steers streaming leaves and blinking
fireflies (lowlands + grove); current streaks texture the river.

## Knowledge systems

- **Route sketch** (hands strip): terse landmark list. True but thin.
- **Axe blazes** (gold, on trunks): previous climbers' marks. Reading
  them is flavor that rewards attention, never a requirement.
- **Guide mark** (gold, cave wall face): one doubled east-turn mark with
  tallies — the turnoff even the dwarf's line can't give you.
- **Waystone rune** (gold, on the standing stone): one gilded rune with
  tallied climbers beneath. Flavor for the attentive, never required.
- **NPC lines**: librarian route, dwarf trust-gated lines. Short, locked
  per stage; walk >4.5u cancels dialogue.
- **REWORK PENDING (playtest):** note text quality and mark placement
  need a dedicated pass — see ROADMAP.

## HUD and screens

Crosshair dot (warms gold when a swing would connect, flashes on
connects); health pips + 3-step
edge meter bottom-left; hands strip (axe/sketch/log) bottom-right;
context prompt bottom-center within reach (3.4u, log 5u). No compass,
no minimap, no damage numbers. Title with 3 save slots + controls
card; pause overlay with resume, sensitivity and volume sliders,
mute toggle, and quit to title; staged boot shows a loading overlay
naming each world stage; handhelds get a desktop-only notice on
title; death screen with "wake as a new nobody"; dwarf-dead permanence
note; session-only storage banner; strict save-import rejection modal.

## Feedback rules

- Dullness reads in 3 channels: edge color chips, chop pitch drop,
  dwarf arrival line. No bare numbers for it.
- Wolf hits read instantly: yelp + recoil + ember pulse + crosshair.
  Every connect lands weight: hit-stop freeze, camera kick. Blood
  bursts from the wound (more on a kill). Kills confirm longer and
  lower; bites answer with their own sound.
- Wolf lunges telegraph: plant-and-crouch windup with a rising snarl,
  dodgeable by stepping out; a gated lunge snaps, never lands
  silent; corpses keel over, eyes dead, and linger 2.5s.
- Chops land weight: trunk shudders, chips burst and settle, impact
  stops and kicks lighter than flesh; sharp edges crack, dull thud.
  Wooden walls chip without shuddering; rock and metal throw
  sparks and clang instead ("Sparks fly."). Bursts erupt where the
  eye sees contact: the look-line aims a confirmation ray at the
  visual face, so bursts never stage beside the player on big
  rocks or hide inside thin trunks.
- Swings read in 3 beats: windup pull-back, accelerating chop
  that sweeps through center to the crosshair, settle to rest.
  Every swing whooshes on the click; impact
  resolves at contact (k=0.62), when the axe visibly arrives — then
  the wolf answers inside the instant-read rule above: flinch, flash,
  knockback, and a yelp trailing the hit crack.
- Trust shifts land inside the conversation that earns them.
- No critical cue is audio-only: howls (15–60u from den) pair with a
  prompt; growls pair with eyes and posture.

## Wayfinding guarantees (every lighting state v1 ships)

Smoke over the treeline; ember mouth glow after dark; blazes along the
path line; grove light pool visible from the cave's east shoulder;
howls within earshot of the cave. At least one cue active always.

## Explicitly not v1

Day cycle, other roles/paths, towns/races/housing, BBEG arc, trading,
crafting beyond sharpening, multiplayer, VR (engine choice keeps it
possible), remappable keys, touch support.
