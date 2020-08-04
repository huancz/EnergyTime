using System;
using StardewModdingAPI;
namespace EnergyTime
{
    internal class ModConfig
    {
        public float EnergyRequirementMultiplier { get; set; } = 2.0F;
        public SButton PassTimeKey { get; set; } = SButton.U;
    }
}
