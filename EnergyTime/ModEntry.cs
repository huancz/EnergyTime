﻿using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace EnergyTime
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {

        /********* Properties *********/

        private ModConfig Config;
        private bool IsPassingTime = false;
        private const float TargetIntervals = 150;
        private float UpdateStaminaDelta;
        private float LastStamina;
        private float StaminaUsed;
        private Dictionary<long, float> MultiplayerLastStamina = new Dictionary<long, float>(); 
        

        /*********
        ** Public methods
        *********/
        public override void Entry(IModHelper helper)
        {
            // read config
            this.Config = helper.ReadConfig<ModConfig>();

            // set initial values
            this.StaminaUsed = 0;

            // initialize helpers
            // Time pass keybind
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.Input.ButtonReleased += this.OnButtonReleased;
            // Main utility
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            // Resetting functionality
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.Multiplayer.PeerConnected += this.OnPeerConnected;
            helper.Events.Multiplayer.PeerDisconnected += this.OnPeerDisconnected;
        }


        /*********
        ** Private methods
        *********/

        // Effectful.
        // Updates
        // - this.IsPassingTime
        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!this.IsMainPlayerReady())
                return;

            if (e.Button == this.Config.PassTimeKey)
                this.IsPassingTime = true;
        }

        // Effectful.
        // Updates
        // - this.IsPassingTime
        private void OnButtonReleased(object sender, ButtonReleasedEventArgs e)
        {
            if (!this.IsMainPlayerReady())
                return;

            if (e.Button == this.Config.PassTimeKey)
                this.IsPassingTime = false;
        }

        // Pure
        private int CurrentTickInterval()
        {
            return 7000 + (Game1.currentLocation?.getExtraMillisecondsPerInGameMinuteForThisLocation() ?? 0);
        }

        // Effectful.
        // Updates
        // - this.MultiplayerLastStamina
        private float GetCurrentStamina()
        {
            // In multiplayer, update the local state of each farmer's current
            // stamina, as well as returning the current total stamina
            if (Context.IsMultiplayer)
            {
                float totalStamina = 0;
                foreach (Farmer farmer in Game1.getOnlineFarmers())
                {
                    long farmerID = farmer.UniqueMultiplayerID;
                    float farmerStamina = farmer.Stamina;
                    this.MultiplayerLastStamina[farmerID] = farmerStamina;
                    totalStamina += farmer.Stamina;
                }
                return totalStamina;
            }

            // In single player simply return the player's stamina
            return Game1.player.Stamina;
        }

        // Effectful.
        // Updates
        // - this.MultiplayerLastStamina
        // - this.LastStamina
        //  Note: while calls effectful methods, won't trigger effects due
        //  to it being in a singleplayer scenario where it is pure (yay state)
        private float NextUsedStamina()
        {
            float staminaChange;
            
            // In multiplayer, iterate through each player and determine if
            // their last stamina is greater than their new stamina. If so
            // add the difference. Otherwise, ignore and simply update their
            // state of this.MultiplayerLastStamina
            if (Context.IsMultiplayer)
            {
                float multiStaminaUsed = 0;
                float totalStamina = 0;
                Dictionary<long, float> multiplayerData = this.MultiplayerLastStamina;
                foreach (Farmer farmer in Game1.getOnlineFarmers())
                {
                    long farmerID = farmer.UniqueMultiplayerID;
                    float farmerStamina = farmer.Stamina;
                    float lastStamina = multiplayerData[farmerID];
                    this.MultiplayerLastStamina[farmerID] = farmerStamina;
                    if (lastStamina > farmerStamina)
                        multiStaminaUsed += (lastStamina - farmerStamina);

                    totalStamina += farmer.Stamina;
                }

                this.LastStamina = totalStamina;
                staminaChange = multiStaminaUsed;
            }
            // In singleplayer, simply get the current stamina and if the value
            // is greater than the previous stamina, treat it as no change
            else
            {
                float prevStamina = this.LastStamina;
                float currentStamina = this.GetCurrentStamina();
                this.LastStamina = currentStamina;
                if (currentStamina - prevStamina >= 0)
                    staminaChange = 0;
                else
                    staminaChange = prevStamina - currentStamina;
            }                
                
            return this.StaminaUsed + staminaChange;
        }

        // Pure
        private float DetermineUpdateDelta()
        {
            if (Context.IsMultiplayer)
            {
                float totalStamina = 0;
                foreach (Farmer farmer in Game1.getOnlineFarmers())
                {
                    totalStamina += farmer.MaxStamina;
                }
                return totalStamina / TargetIntervals;
            }

            return Game1.player.MaxStamina / TargetIntervals;
        }

        // Pure
        private bool IsMainPlayerReady()
        {
            // return true if the save is loaded and it is the main player
            return Context.IsWorldReady && Context.IsMainPlayer;
        }

        // Effectful.
        // Updates
        // - this.LastStamina
        // - this.UpdateStaminaDelta
        // - this.StaminaUsed
        // Calls side effectful methods
        private void ResetUpdateStaminaDelta()
        {
            if (!this.IsMainPlayerReady())
                return;

            this.LastStamina = this.GetCurrentStamina();
            this.UpdateStaminaDelta = DetermineUpdateDelta();
            this.StaminaUsed = 0;
        }

        // Effectful
        // Updates
        // - Game1.gameTimeInterval
        private void ManualPassTime(int tickInterval)
        {
            if (this.IsPassingTime)
            {
                int ffTimeInterval = Convert.ToInt32(tickInterval * 0.1);
                Game1.gameTimeInterval += ffTimeInterval;
                return;
            }
            else
                Game1.gameTimeInterval = 0;
        }

        // Effectful
        // Updates
        // - Game1.gameTimeInterval
        // - this.StaminaUsed
        // Calls side effectful methods
        private void CalculateTimePassage(int tickInterval)
        {
            float usedStamina = this.NextUsedStamina();
            float requirement = this.UpdateStaminaDelta * this.Config.EnergyRequirementMultiplier;
            if (usedStamina > requirement)
            {
                Game1.gameTimeInterval = tickInterval;
                this.StaminaUsed = usedStamina % requirement;
            }
            else
                this.StaminaUsed = usedStamina;
        }

        // Effectful
        // Calls side effectful methods
        private void OnUpdateTicked(object sender, EventArgs e)
        {
            if (!this.IsMainPlayerReady())
                return;

            int tickInterval = this.CurrentTickInterval();
            ManualPassTime(tickInterval);
            CalculateTimePassage(tickInterval);            
        }

        // Effectful
        // Calls side effectful methods
        private void OnSaveLoaded(object sender, EventArgs e)
        {
            ResetUpdateStaminaDelta();
        }

        // Effectful
        // Calls side effectful methods
        private void OnDayStarted(object sender, EventArgs e)
        {
            ResetUpdateStaminaDelta();
        }

        // Effectful
        // Updates
        // - this.MultiplayerLastStamina
        // Calls side effectful methods
        private void OnPeerConnected(object sender, PeerConnectedEventArgs e)
        {
            long playerID = e.Peer.PlayerID;
            float playerStamina = Game1.getFarmer(playerID).Stamina;
            this.MultiplayerLastStamina.Add(playerID, playerStamina);
            ResetUpdateStaminaDelta();
        }

        // Effectful
        // Updates
        // - this.MultiplayerLastStamina
        // Calls side effectful methods
        private void OnPeerDisconnected(object sender, PeerDisconnectedEventArgs e)
        {
            this.MultiplayerLastStamina.Remove(e.Peer.PlayerID);
            ResetUpdateStaminaDelta();
        }
    }
}