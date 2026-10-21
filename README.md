# Unlock Extractions
SPT server mod to unlock all extractions on raids, instead of only those furthest away from the player on load
Also converts COOP extractions into a paid extraction with configurable currency & cost

# Installation
Extract the zip in `<SPT Install Directory>/SPT_Runtime/user/mods`

# Config explanation
```json
{
  "EnableAllExits": true, // Wether normal extraction points should be unlocked regardless of player distance on raid start
  "PaidCoopExits": true, // If COOP exits should be paid for like a v-ex
  "Currency": "Roubles", // The currency to use to pay for the COOP exits, can be "Roubles", "Dollars", "Euros" or "GP"
  "Price": 25000 // The amount of currency to pay for the COOP exit, can't exceed the max stack of the selected currency
}
```
