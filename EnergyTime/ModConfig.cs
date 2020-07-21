using System;
using StardewModdingAPI;
namespace EnergyTime
{
    internal class ModConfig
    {
        public double EnergyRequirementMultiplier { get; set; } = 4.0;
        public SButton PassTimeKey { get; set; } = SButton.U;
    }
}
