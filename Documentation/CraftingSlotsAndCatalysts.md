# Crafting Slots, Skill Scaling, and Catalysts

## Backend flow

`SlotCraftingRequest` is the UI-independent request model. Each required slot names the exact item component, item definition, item-instance stack, and quantity to use. One optional catalyst slot may contain any inventory item. The Prototype service exposes two entry paths:

- `BuildAutoFilledSlotCraftingRequest(recipeId)` chooses available inventory stacks for every required recipe input. A future category/list UI can call this and then display the populated slots.
- `BeginSlotCraftAtPrototypeWorkstation(request)` validates a manually arranged request and starts the timed craft.

`BeginCraftAtPrototypeWorkstation(recipeId)` remains the simple list-selection entry point and delegates to auto-fill. Both paths execute through the same recipe, production-requirement, item-identity, composition, quality, affix, durability, and rollback runtimes.

The Prototype workstation UI now exposes this flow directly. Interacting with the station opens an item-type selection page sourced from normalized `craftable.*` recipe tags. Selecting a type opens a second page with a central item outline, component boxes arranged around it, compatible inventory resource choices in each box, auto-fill, and the infusion/catalyst slot below the item. Starting the craft submits a `SlotCraftingRequest`; the UI does not contain a second crafting implementation.

Validation requires exact recipe quantities, exact item-instance identities, unique slot IDs, inventory ownership, and enough combined stack quantity across required and catalyst assignments. A material-family input uses one resource definition per craft (for example, three iron ore or three future copper ore), although it may draw that resource from multiple inventory stacks. Completion revalidates the locked slot request, so moving or spending an assigned item while crafting makes the craft fail safely rather than substituting another stack.

## Resource categories

Recipes can request a normalized resource family instead of one hardcoded item. These families use IDs such as `material.metal`, `material.wood`, `material.leather`, `material.liquid`, `material.glass`, and `material.mineral`. The match comes from a raw resource item's authored composition and its referenced `MaterialDefinition`, not from the item's name. Finished objects that merely contain wood or metal are deliberately excluded, preventing a bow from being consumed as “wood.” This keeps inventory identity specific while making recipes extensible.

Each recipe input is attached to a component role such as `component.blade`, `component.hilt`, or `component.grip`. The actual resource selected is recorded against that input and transferred into the matching output component, so adding copper does not silently produce an iron-composition sword.

Current canonical layouts are:

| Craftable tag | Component slots |
|---|---|
| `craftable.sword` | Blade: metal; Guard: metal; Hilt/Grip: leather; Pommel: metal |
| `craftable.helmet` | Shell: metal ×3; Fittings: metal |
| `craftable.shield` | Face: wood ×2; Rim: metal ×2; Boss: metal |
| `craftable.bow` | Limbs: wood ×2; Grip: wood; Fittings: wood |
| `craftable.arrow` | Shafts: wood; Heads: metal; output quantity: 10 |

Leather is authored as `material.leather` plus the inventory resource `item.leather-strip`; it is catalog content only and has no scene pickup.

To add a new accepted metal, author a `MaterialDefinition` with the Metal category (and normally the `material.metal` tag), use that material in the new resource item's default composition, and include both definitions in the catalog. Recipes already requesting `material.metal` will accept it without recipe edits. More specific tags such as `material.iron-bearing` can still be used when a recipe intentionally requires a narrower family.

## Material-derived crafted names

Crafted item instances receive a persisted custom name after their component composition is created. The primary structural component supplies the leading material, and a significant secondary component supplies the qualifier. Examples include `Iron Sword with Leather Grip`, `Wood Bow with Wood Grip`, and `Wood Arrows with Iron Heads`. Raw-form suffixes such as “Ore,” “Ingot,” and “Log” are removed for the finished-item name. Specific future materials therefore naturally produce names such as `Yew Bow with Velvet Grip` without new recipe definitions.

## Craft time and skill

Each recipe owns `RecipeCraftingScalingData`. Its defaults are:

| Setting | Default |
|---|---:|
| Base duration | 10 seconds |
| Minimum duration | 1 second |
| Unskilled duration multiplier | 1.25x |
| Duration reduction per learned grade tier | 6% |
| Minimum skilled duration multiplier | 0.55x |
| Duration per output rarity rank | +12% |
| Duration per output stat-complexity point | +3% |
| Unskilled quality adjustment | -5 percentage points |
| Quality per learned grade tier | +4 percentage points |
| Maximum skill quality bonus | +35 percentage points |
| Affix chance per learned grade tier | +2.5 percentage points |

The best learned eligible Skill is selected. F through AAA count as tiers 1 through 8. Higher Skill therefore reduces craft time, raises final quality, raises base equipment-stat effectiveness through quality, increases derived rarity potential, and raises the normal affix-generation chance. More inherently rare or stat-heavy outputs take longer to craft.

## Optional catalyst behavior

The optional item is always consumed when the craft succeeds, whether its effect roll succeeds or fails. Catalyst resolution is deterministic for a given operation seed:

1. Find all `CraftingCatalystEffectDefinition` records matching the item, its category, or tags.
2. Select one matching effect by authored generation weight.
3. Calculate effect chance from base chance, catalyst rarity rank, extra copies, and crafting Skill grade.
4. Roll once.
5. On success, select an affix tier from catalyst rarity plus extra-copy thresholds and apply the resulting crafted affix to the primary tracked output.

Current potion defaults use `8% + 10% per rarity rank + 7% per extra copy + 2% per Skill grade tier`, capped at 90%. Every two additional copies advance one affix tier; catalyst rarity also advances the tier. Tier bonuses are +2, +4, +7, +11, and +16 to the related maximum resource.

Authored Prototype mappings are:

| Catalyst | Resulting affix | Applied stat |
|---|---|---|
| Health Potion | Vitality Infused | Maximum Health |
| Mana Potion | Mana Infused | Maximum Mana |
| Stamina Potion | Stamina Infused | Maximum Stamina |

Items without a matching catalyst-effect definition are legal optional inputs: they are consumed, recorded in provenance, and produce no special effect. This allows content to be expanded without changing the execution code.

## Atomicity and persistence

Recipe inputs and catalysts are consumed inside the same rollback boundary as output identity, composition, quality, affixes, durability, tool wear, and inventory projection. Any error restores all participating runtimes and the player inventory.

Completed crafting records persist Skill provenance, duration and quality adjustments, exact catalyst identity/quantity, selected effect and tier, chance, roll, and whether the effect applied. The crafting save schema is version 2; development saves from the previous schema are intentionally unsupported.

## Disassembly provenance

Every crafted material entry also records the exact consumed resource item definition that supplied it. Disassembly reads that composition rather than guessing from the finished item's definition, so an iron blade and leather grip can return iron ore and leather strips independently. Component durability limits both recovery chance and the maximum recoverable quantity; high skill cannot recreate material that no longer physically remains.
