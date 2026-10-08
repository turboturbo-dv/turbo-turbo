Your model captures real phenomena, but it's built around one mechanism (wall cooling) when white smoke actually has several distinct causes. The most important missing pieces are what happens to the air before the fuel even arrives, the problem of fuel mixing too lean to burn at light load, and the fact that white smoke is mostly condensed vapor rather than ejected liquid. I'll go through your points in order, then fill in what's missing.

## What white smoke actually is

White smoke is an aerosol: very fine liquid droplets, typically around a micron in size, that scatter light strongly. In the diesel context, there are two main sources:

- **Fuel smoke.** Unburned or partially burned fuel, often with some lubricating oil. It smells of raw diesel, stings the eyes, and lingers in the air.
- **Water.** This can be harmless condensation of the water produced by combustion (roughly a kilogram of water per kilogram of fuel burned), which shows up in cold, humid weather and disappears within a few meters of the stack. Or it can be coolant leaking into the cylinders or air system, which is a serious fault. It tends to smell sweetish rather than of fuel.

Blue smoke usually means lubricating oil. Its droplets are typically smaller, which gives the bluish tint, though in practice blue and white smoke often mix.

Black smoke is something different altogether: solid carbon particles (soot). Soot forms when fuel burns in rich, oxygen-poor zones at high temperature, roughly above 1,500 K. White smoke appears when temperatures are too low for fuel to burn or even to form soot.

Your phrase "without burning or carbonising" is exactly right on this point. White smoke is fuel that never got hot enough for either.

## Your first point: cold walls

This is partly right, but the main effect of cold walls happens before injection, and it's about the air rather than the fuel.

**The compression temperature problem.** A diesel ignites because compression heats the air above the fuel's autoignition temperature. With an ideal compression from 20 °C at a compression ratio around 16:1, the air would reach roughly 500 °C. In reality, several factors reduce that:

- **Cold intake air** starts the process lower. Starting from −20 °C instead of 20 °C knocks around 100 °C off the final temperature.
- **Cold walls absorb heat during compression.** This is the main effect of a cold block. The air loses heat to the liner, head, and piston throughout the compression stroke.
- **Low cranking speed** gives more time for heat loss and for compressed air to leak past the piston rings.
- **Low boost** at idle and cranking means less air mass to begin with.

If the compressed air ends up only marginally above the autoignition temperature, the **ignition delay** (the time between the start of injection and ignition) gets much longer. If it's not hot enough at all, the cylinder misfires entirely.

**Long ignition delay has two consequences.** Fuel that eventually ignites after a long delay has had time to mix extensively and burns all at once, giving the sharp knocking sound typical of a cold diesel. Fuel that doesn't ignite, or ignites so late that the piston is already well down its stroke and the gas is cooling, leaves the cylinder as vapor and partly decomposed fuel.

**Wall effects on the fuel itself** are real but secondary:

- **Spray impingement.** Fuel spray hitting cold liner or piston surfaces forms a liquid film that evaporates too slowly to burn in time. Some leaves as vapor, and some washes past the rings into the oil, causing fuel dilution.
- **Flame quenching.** Near cold walls, the flame is extinguished in a thin layer of gas because the wall draws away heat. Fuel in that layer stays unburned.

So the wall-cooling mechanism you described exists, but the bigger effect of cold walls is reducing compression temperature, which affects all the fuel injected, not just fuel that touches the walls.

## Your second point: warming up

This is right in direction, but the details matter.

**Two warm-up processes run at different speeds.** The bulk metal and coolant warm up slowly, over many minutes for a large engine. The gas-side surfaces of the liner, piston crown, and head can heat up much faster, because they receive heat directly from combustion. How hot they get depends on heat flux, which depends on load.

**Surface temperature tracks load more than coolant temperature.** The gas-side surface of the liner sits above coolant temperature by an amount set by the heat flowing through it. At idle, little fuel is burned, the heat flux is small, and liner surfaces stay close to coolant temperature. At full load, liner surfaces near the top of the stroke and piston crowns run much hotter, with crowns reaching several hundred degrees Celsius.

**Idling may never get there.** A large diesel at idle burns very little fuel for its size and moves a lot of air. In cold weather, idling can fail to keep the coolant at operating temperature. The cylinder surfaces stay relatively cool, and the problem doesn't fully go away.

This is why the standard advice is to put load on a cold engine after a short warm-up rather than idling it at length. Moderate load heats the combustion surfaces quickly and raises compression temperatures, which clears white smoke much faster than idling does.

**Operating points where this matters most:** cold starts, extended idling, very light load, cold ambient temperature, and high altitude, where lower air density reduces compression pressure and temperature.

## A mechanism your model doesn't include: overmixing

Even in a fully warm engine, light load produces unburned fuel by a mechanism unrelated to wall temperature.

During the ignition delay, fuel mixes with air. At the edges of each spray plume, some fuel mixes so thoroughly that the mixture becomes too lean to burn: it falls below the lean flammability limit. When ignition occurs, the flame can't propagate into that over-lean region, and the fuel there escapes unburned.

At light load, this is a major source of unburned fuel. Little fuel is injected, the cylinder contains a large excess of air, and temperatures are low, so the lean regions are large and the delay is long enough for overmixing to occur.

Two-stroke engines are especially prone to this at idle. As discussed earlier, they blow a large volume of scavenging air through the cylinder, which keeps cylinder temperatures low and dilutes the small fuel charge further.

There's also an opposite mechanism, **undermixing**. Fuel that dribbles from the injector nozzle after the main injection, or that remains in the small volume inside the nozzle tip, enters the cylinder late, at low velocity and with poor atomization. It doesn't mix or burn properly and contributes to unburned fuel and smoke. Worn or dribbling injectors make this much worse.

## Your third point: condensation in a cool stack

This is right, with two refinements.

**It's not just fuel.** The material that collects inside a cold exhaust system is a mixture of heavier fuel fractions, lubricating oil (from oil passing the rings at light load and from turbocharger seals, which leak more readily when boost is low), soot, water, and acids. Diesel fuel boils across a range of roughly 180–360 °C. At idle, the exhaust of a large diesel can be well below the upper end of that range, so the heavier fractions condense readily.

**It collects throughout the exhaust path,** not just in the stack: manifolds, the turbocharger housing, and any cool surface along the way.

The result is called **wet stacking** (sometimes "slobbering"), a dark, oily, often sooty residue that can drip from manifold joints and the stack. It is a classic problem in locomotives left idling for long periods and in lightly loaded standby generators.

## Your fourth point: evaporation and shear on warming

This is right, but the outcome is more varied than just white smoke.

When load increases after a long idle, the exhaust temperature and velocity rise quickly, and three things happen to the accumulated deposits:

- **Evaporation.** The lighter components evaporate into the exhaust stream, then condense again into fine droplets as they cool and mix with outside air, producing white or bluish smoke.
- **Mechanical ejection.** High-velocity gas tears liquid off surfaces. This produces relatively large droplets that tend to spatter around the stack and onto the locomotive roof rather than form a visible haze.
- **Combustion in the exhaust.** Exhaust from a diesel still contains plenty of oxygen. Hot enough deposits can ignite in the manifold, turbocharger, or stack, producing black smoke, sparks, and sometimes flames from the stack. With EMD engines, accumulated oil in the exhaust system was a known source of stack fires after long idling.

So the smoke emitted when an idling locomotive is notched up is often a mix of white, blue, grey, and black, not white alone.

## Your fifth point: what makes the smoke visible

This needs one important correction. Most white smoke doesn't come from liquid fuel being ejected. It forms from vapor.

Hot exhaust carries fuel and oil as vapor, which is invisible. As the exhaust cools, either in the stack or after leaving it and mixing with cold outside air, the vapor becomes supersaturated and condenses into very fine droplets. Those droplets scatter light and create the visible white plume.

You can sometimes see this directly: the exhaust looks clear for a short distance above the stack, then turns white as it cools. Water vapor behaves the same way, which is why a harmless steam plume can look like fuel smoke at first glance. The difference is that a steam plume evaporates again within a few meters, while fuel smoke lingers.

Liquid fuel sheared off surfaces mostly forms larger droplets, which fall out quickly and contribute relatively little to the visible plume.

## Other causes worth knowing

**Fuel quality: cetane number.** Cetane number measures how readily a fuel ignites under compression. Low-cetane fuel has a longer ignition delay, which makes cold-start white smoke markedly worse. Cetane improvers in fuel were partly aimed at this.

**Injection timing.** Injecting too late means combustion starts as the piston descends and the gas cools, increasing unburned fuel. In mechanically timed engines of this era, timing was fixed, so it couldn't be advanced for cold starting the way later electronic systems could.

**Injector condition.** Poor atomization, low opening pressure, or dribbling nozzles all increase white smoke at light load (and black smoke at high load).

**Low compression.** Worn rings, leaking valves, or worn liners lower compression pressure and temperature, reducing the margin for ignition.

**Coolant leaks.** Coolant entering the cylinders or air system produces white smoke from steam. In EMD engines, leaking aftercoolers and liner seals were a recognized fault.

## The context for locomotives

Many of these mechanisms came together in locomotive practice. Engines were idled for long periods, partly to avoid freezing the plain-water cooling systems discussed earlier. Compression ratios were modest, so cold-start margins were narrow. Two-strokes moved large volumes of cool scavenge air at idle. Turbocharger seals leaked oil when boost was low. The result was white smoke at startup and on long idles, oily stacks from wet stacking, and smoky, sometimes sparking exhausts when an idling unit was finally put to work.

Large cylinders do help somewhat. A big cylinder has less wall area relative to its volume than a small one, so it loses proportionally less heat during compression. That is one reason large slow-speed engines start more easily, cylinder for cylinder, than small high-speed ones at the same compression ratio.

## A revised version of your model

1. White smoke is mostly unburned fuel (and some oil) that leaves the cylinder as vapor and condenses into fine droplets as the exhaust cools. Steam from combustion water or coolant leaks can look similar.
2. The main causes are low compression temperature (cold intake air, cold walls, slow cranking, low boost), which lengthens ignition delay or causes misfire; over-lean mixing at light load; poor late injection; and fuel hitting cold walls or being quenched near them.
3. Cylinder surfaces warm much faster under load than at idle, because surface temperature depends on heat flux. Idling a large diesel in cold weather may never fully warm it.
4. Fuel, oil, soot, and water condense throughout a cold exhaust system, not just in the stack.
5. When load increases, accumulated deposits evaporate, are ejected, or burn, producing a mix of smoke colors and sometimes sparks or flames.

---

Great info. Interesting that cold intake air drops the final temperature so much. Can you elaborate on this? Presumably a turbocharger helps with this, but does that mean this problem is more severe on blown / naturally aspirated diesels?

---

# Intake Air Temperature and Compression Temperature

## Why cold intake air matters so much

The key is that compression heating is **multiplicative**, not additive. During compression, the gas temperature rises by a factor that depends on the compression ratio. To a reasonable approximation:

T_end = T_start × CR^(n−1)

Here temperatures are absolute (in kelvin), CR is the compression ratio, and n is a polytropic exponent. For air with some heat loss, n is around 1.3–1.35. With n = 1.35 and a compression ratio of 16, the multiplier is about 2.64.

Because it multiplies, any change in starting temperature is amplified by the same factor. A 40 K drop in intake temperature becomes a drop of roughly 105 K at the end of compression.

Using the same simple model:

| Compression ratio | Intake at 20 °C | Intake at −20 °C | Difference |
|---|---|---|---|
| 13:1 | about 445 °C | about 350 °C | about 95 °C |
| 16:1 | about 500 °C | about 395 °C | about 105 °C |
| 20:1 | about 565 °C | about 450 °C | about 115 °C |

Real engines come out lower than this. Compression doesn't begin until the intake valve or ports close, which happens after bottom dead center, so the effective compression ratio is smaller than the geometric one. Heat losses to cold walls and leakage past the rings at cranking speed take more off.

## What temperature is actually needed

Diesel fuel's autoignition temperature measured in a laboratory flask is only around 210–260 °C. But that figure involves waiting a long time for ignition. An engine has only a few milliseconds. To ignite fast enough for the combustion to happen near top dead center, the compressed air must be much hotter, typically several hundred degrees Celsius.

Pressure matters too. Ignition delay shortens with both higher temperature and higher pressure (denser air means more oxygen molecules near each fuel droplet). Temperature has the stronger effect, roughly exponential, so a 100 °C loss can turn a short delay into a long one or into misfire.

## Does a turbocharger help?

Under load, yes, considerably. When the turbocharger is producing boost, the air entering the cylinders is at higher pressure, and it is often warmer than ambient even after intercooling. Both shorten ignition delay. A warm, loaded turbocharged engine ignites easily even in cold weather.

At cold start and idle, no. During cranking and at idle there is very little exhaust energy, so the turbocharger produces almost no boost. The engine effectively runs as if naturally aspirated.

### Turbocharged engines often start worse

This is the surprising part: turbocharged engines usually have **lower** geometric compression ratios than naturally aspirated ones. At full boost, the cylinder starts compression at high pressure, and a high compression ratio would push peak cylinder pressures beyond what the structure can handle. Designers lower the compression ratio to keep peak pressure in check. Large turbocharged diesels typically run around 12–16:1, while small naturally aspirated diesels often run 17–22:1.

At cold start, with no boost available, the turbocharged engine is left with a naturally aspirated engine's air supply and a lower compression ratio. From the table, that's the worst combination. This is one reason large turbocharged engines rely heavily on preheating: jacket water heaters on standby generators, preheating to around 60 °C before starting on many marine medium-speed engines, and long idling or layover heating for locomotives.

### The aftercooler can help or hurt

It depends on what cools it.

**Aftercooler on jacket water.** Many locomotive engines of the era, EMD's included, used the engine's main coolant circuit for the aftercooler. At full load, this coolant at around 80 °C cools the hot compressed air. At idle in cold weather, the incoming air is colder than the coolant, so the aftercooler **heats** it. The same device cools the charge at high load and warms it at low load, which is a useful side effect in cold weather.

**Aftercooler on a separate, colder circuit.** A low-temperature circuit gives better charge cooling at full load. But at light load in cold weather, it can chill the intake air further and make white smoke worse. Larger engines with this arrangement often include ways to warm or bypass the charge air at low load.

## Blown and naturally aspirated engines compared

**Naturally aspirated four-strokes** have no boost at any point, so they never benefit from charge heating or pressure. But they can use high compression ratios, which gives them the largest margin at cold start. They are generally the easiest diesels to start cold, which is why small NA diesels were traditionally regarded as good cold starters.

**Roots-blown two-strokes** have a different set of trade-offs:

- **Some compression heating.** The blower raises the air temperature a little, but at idle and cranking the pressure ratio is close to 1, so the gain is small, perhaps a few degrees.
- **The scavenging air is cold.** A two-stroke flushes its cylinder with large volumes of outside air every revolution. In a four-stroke, some hot residual gas stays in the cylinder and warms the fresh charge. A two-stroke clears most of it out, so the trapped charge is close to ambient temperature plus whatever it picks up from the walls.
- **Air supply is assured.** The gear-driven blower supplies air from the first turn of the crankshaft, so starting doesn't depend on exhaust energy.
- **Modest compression ratios.** EMD engines ran around 14.5–16:1 depending on the version, lower than small naturally aspirated diesels.

Two-strokes therefore don't really gain a cold-start advantage from their blowers. Their heavy scavenging makes it harder to keep the cylinder warm at idle, which ties back to why two-strokes produce more white smoke at idle and light load.

**Turbocharged engines with a gear-driven assist,** like EMD's turbocharged 645, behave like Roots-blown engines at starting and idle. The gear drive supplies the scavenging air, but at low pressure ratio there is little heating, and the compression ratio may be somewhat lower than a non-turbo equivalent.

## Summary

Cold intake air has an outsized effect because compression multiplies absolute temperature. A drop of 40 K at intake becomes roughly 100 K at the end of compression.

A turbocharger helps a great deal once the engine is producing boost, through higher pressure and warmer charge air. But at cold start and idle it provides almost nothing, and because turbocharged engines usually have lower compression ratios, they are often harder to start cold than naturally aspirated engines.

Blown two-strokes don't gain much either: their blowers add little heat at low pressure ratios, and their heavy scavenging replaces warm residual gas with cold outside air.

So the cold-start problem is not most severe on naturally aspirated engines, as you might expect. They usually cope best, thanks to their high compression ratios. It is generally most severe on turbocharged engines with low compression ratios, and on heavily scavenged two-strokes.

---

Great info. Interesting that cold intake air drops the final temperature so much. Can you elaborate on this? Presumably a turbocharger helps with this, but does that mean this problem is more severe on blown / naturally aspirated diesels?

---

# A Model of How Unburned Fuel Reaches the Exhaust Manifold

The clearest way to model this is to follow a parcel of fuel from the moment it leaves the injector to the moment the exhaust opens. At each stage, some fuel can escape burning, and whether it does depends on a small set of conditions inside the cylinder. Fuel reaches the manifold only if it survives every stage: it fails to ignite or burn during combustion, and then also fails to oxidize during expansion.

## The conditions that drive everything

A handful of state variables determine what happens at each stage:

**Charge temperature and pressure at injection.** These depend on intake air temperature, boost, effective compression ratio, heat lost to the walls during compression, leakage past the rings, cranking speed, and how much hot residual gas remains from the previous cycle. This is the single most important factor, through its effect on ignition delay.

**Wall temperatures** of the liner, piston crown, and head. These depend on coolant temperature and on heat flux, which in turn depends on load.

**Fuel quantity per cycle.** This depends on load, and it sets both how much heat is released and how much excess air there is.

**Injection quality:** timing, injection pressure, atomization, nozzle condition, and how cleanly injection ends. In the mechanical injection systems of this era, these were not independent of speed and load, as discussed below.

**Fuel properties,** mainly cetane number (ignition quality) and volatility.

**Engine speed,** which sets the time available for each stage.

**Air motion and mixing** in the cylinder. Large medium-speed engines typically have relatively quiescent air and rely on the injection spray itself to do most of the mixing.

## Stage 1: ignition

When fuel is injected, it atomizes, evaporates, and mixes with hot air. After the ignition delay, it ignites. Three outcomes are possible.

**Normal ignition.** A short delay; combustion starts near top dead center, and most fuel burns well.

**Long delay.** The charge is only marginally hot enough. A lot of fuel mixes before ignition, so when it does ignite, much of it burns at once, which gives the knocking sound of a cold diesel. Combustion also starts later and may extend well into the expansion stroke, when gas is cooling.

**Misfire.** The charge never gets hot enough, so the cylinder doesn't ignite at all, and essentially the whole fuel charge goes out with the exhaust.

**Partial oxidation without ignition.** In the marginal case, fuel can undergo slow, low-temperature reactions, sometimes called cool-flame chemistry, without proceeding to full ignition. These produce aldehydes and other partially oxidized compounds. They are responsible for the sharp, eye-stinging quality of cold-start white smoke.

Misfire isn't all-or-nothing across a multi-cylinder engine. At idle, especially when cold, some cylinders may fire while others misfire intermittently, depending on small differences in compression, injector condition, and temperature.

## Stage 2: combustion

Even when ignition succeeds, several mechanisms leave fuel unburned.

### Overmixing

During the ignition delay, the edges of each spray plume mix with a large surrounding volume of air. Some of that fuel becomes too dilute to burn: it falls below the lean flammability limit. When the flame develops, it can't spread into those regions.

This is the dominant source of unburned fuel at light load in a warm engine. Little fuel is injected, the delay is relatively long because temperatures are moderate, and there's a large excess of air for the fuel to disperse into.

### Undermixing

Some fuel enters the cylinder too late or too poorly atomized to burn:

- **Nozzle sac volume.** A small volume of fuel stays in the injector tip below the needle seat after injection ends. It seeps out late, at low velocity, and burns poorly.
- **Dribble and secondary injection.** A poorly sealing needle, or pressure waves in the fuel line reopening the needle briefly, can deliver small amounts of fuel late in the cycle.
- **Poor atomization.** Large droplets evaporate slowly and may not have finished evaporating when the combustion window closes.

In this era's mechanical systems (cam-driven unit injectors and jerk pumps), injection pressure depended on engine speed and fuel quantity. At idle and light load, injection pressure was low and atomization correspondingly poorer. That makes undermixing worse precisely at the operating points where overmixing is also worst. Modern common-rail systems largely eliminated this problem by maintaining high injection pressure regardless of speed.

### Wall impingement

If the spray travels far enough to hit the liner or piston crown, it forms a liquid film. On a cold surface, the film evaporates too slowly to burn in time.

Some of this fuel evaporates late, during expansion or the exhaust stroke, and leaves as vapor. Some is scraped down by the piston rings into the crankcase, diluting the lubricating oil.

Impingement is worst when the charge is cold and dense, as at cold start, and when atomization is poor.

### Flame quenching near walls

Near cold surfaces, the flame is extinguished in a thin boundary layer because the wall draws away heat faster than reactions release it. This is less dominant in diesels than in gasoline engines, because diesel combustion happens mainly around the spray rather than as a flame front sweeping the whole chamber. It still matters where burning fuel approaches cold walls.

### Crevices

Narrow gaps, such as the space between the piston and liner above the top ring, are too narrow for a flame to enter. In gasoline engines, they trap unburned fuel-air mixture and are a major source of unburned fuel. In diesels, crevices fill mostly with air during compression, since fuel is injected later, so they contribute relatively little.

## Stage 3: expansion

Fuel that escapes the main combustion has one more chance. During the expansion stroke, it continues to mix with hot burned gas, which still contains oxygen in a diesel. If the gas is hot enough, the leftover fuel oxidizes. This post-flame oxidation can consume a large share of the fuel that escaped the main combustion.

The key factor is temperature during expansion. As the piston descends, the gas cools. Below roughly 1,000 K, oxidation becomes too slow to matter in the time available, and the remaining fuel is effectively frozen in its unburned state.

This creates an important distinction between operating points:

- **At full load,** the gas is hot during most of the expansion, so leftover fuel has a good chance of being oxidized.
- **At light load,** little fuel is burned, the bulk gas is cooler, and expansion cools it quickly. Fuel that escaped combustion is likely to stay unburned.
- **With late combustion** (long delay, retarded timing), combustion is already happening as the gas cools, leaving less time at high temperature.

## Stage 4: the exhaust event

What survives the earlier stages leaves the cylinder in two phases: **blowdown**, the rapid escape when the exhaust valves or ports open, and the remainder pushed or swept out afterward.

**Wall films can release vapor late.** Fuel films on the liner and piston crown continue evaporating during expansion and exhaust, often releasing fuel after the hot gas that might have oxidized it has cooled.

**Four-strokes retain some residual gas.** Some exhaust gas remains in the cylinder and mixes with the next charge. Unburned fuel in it gets another chance to burn. This residual also warms the next charge, which helps ignition.

**Two-strokes scavenge more thoroughly.** Most of the exhaust is swept out by fresh air. Unburned fuel gets little chance of being retained and re-burned, and the next charge loses the heating benefit of residual gas.

**No fuel short-circuits in a direct-injection two-stroke.** In small crankcase-scavenged gasoline two-strokes, some fresh fuel-air mixture passes straight through to the exhaust during scavenging. A diesel two-stroke scavenges with air only, and fuel is injected after the ports close, so this pathway doesn't exist. The exception is a dribbling injector leaking fuel during scavenging.

## What arrives in the manifold

The material leaving the cylinder is a mixture of:

- **Fuel vapor,** from misfire, overmixing, late-evaporating wall films, and late-injected fuel
- **Partially oxidized compounds,** such as aldehydes and carbon monoxide, from cool-flame chemistry and interrupted oxidation
- **Liquid droplets,** from incompletely evaporated spray and films stripped off surfaces, usually a minor share
- **Lubricating oil,** from oil films on the liner and oil passing the rings, which is especially significant at idle when cylinder pressures are low

Whether this material stays as vapor in the manifold depends on manifold temperature, which brings in the condensation processes we're setting aside for now.

## Two-stroke and four-stroke differences

Two-strokes are more vulnerable at idle and light load for several reasons covered earlier. Heavy scavenging fills the cylinder with cool outside air and removes warm residual gas, lowering compression temperature. A large flow of air relative to fuel at idle increases overmixing and lowers bulk temperatures during expansion. Thorough scavenging gives unburned fuel little chance to be retained and re-burned.

Four-strokes benefit from residual gas warming the charge and from retaining some unburned fuel for another attempt, though they also have lower compression ratios when turbocharged.

## Feedback loops

Several mechanisms reinforce each other:

**Misfire cools the cylinder.** A misfiring cylinder doesn't release heat, so its walls stay cold, which makes the next cycle more likely to misfire too.

**The governor compensates.** At idle, misfiring cylinders produce no power, so the governor adds fuel to maintain speed. The firing cylinders get more fuel, while the misfiring ones continue to pass unburned fuel to the exhaust.

**Fuel dilution weakens lubrication.** Fuel scraped into the crankcase thins the oil, increasing wear, which over time can lower compression and make ignition harder.

## The operating map

| Condition | Dominant mechanisms |
|---|---|
| Cold start | Misfire, long ignition delay, cool-flame partial oxidation, wall impingement on cold surfaces |
| Cold idle | Intermittent misfire, overmixing, wall films, low expansion temperatures |
| Warm idle and light load | Overmixing, poor atomization from low injection pressure, low expansion temperatures |
| Medium to full load, warm | Low unburned fuel; hot expansion oxidizes most leftovers. The emission problem shifts toward soot (black smoke). |
| Fault conditions | Dribbling or worn injectors, retarded timing, low compression, low-cetane fuel |

## Summary

Unburned fuel reaches the exhaust manifold when it escapes combustion and then also escapes oxidation during expansion.

It escapes combustion through misfire or very long ignition delay, through overmixing into regions too lean to burn, through late or poorly atomized injection, through liquid films on cold walls, and to a lesser degree through flame quenching near walls.

It escapes oxidation when expansion temperatures are too low, which happens at light load and when combustion starts late.

Low charge temperature drives most of this: it lengthens ignition delay, which promotes misfire and overmixing and delays combustion into the cooling part of the cycle. Mechanical injection systems of this era added poor atomization at low speed. Two-strokes, with cool scavenging air and little residual gas, are especially susceptible at idle and light load.