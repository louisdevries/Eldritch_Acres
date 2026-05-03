using System;
using UnityEngine;

namespace EldritchFarm.Player
{
    /// <summary>
    /// Player's currency. Spend on seeds, gain from harvests.
    ///
    /// Lives on the player rather than as a static singleton so it follows the
    /// same pattern as PlayerKnockback — player state belongs to the player.
    /// Other systems get to it via GetComponent on the player tag.
    ///
    /// Fires OnBalanceChanged whenever the value moves, so UI / audio / particles
    /// can react without polling.
    /// </summary>
    public class PlayerWallet : MonoBehaviour
    {
        [Tooltip("Starting balance. Tune to control the difficulty of early-game.")]
        [SerializeField] private int startingBalance = 30;

        public int Coins { get; private set; }

        /// <summary>
        /// Raised whenever the balance changes. Args: previous balance, new balance.
        /// </summary>
        public event Action<int, int> OnBalanceChanged;

        private void Awake()
        {
            Coins = startingBalance;
        }

        /// <summary>
        /// Whether the wallet has at least `cost` coins available.
        /// </summary>
        public bool CanAfford(int cost)
        {
            return Coins >= cost;
        }

        /// <summary>
        /// Try to spend `cost` coins. Returns false (and changes nothing) if insufficient.
        /// </summary>
        public bool TrySpend(int cost)
        {
            if (cost < 0) return false;
            if (Coins < cost) return false;

            int previous = Coins;
            Coins -= cost;
            OnBalanceChanged?.Invoke(previous, Coins);
            return true;
        }

        /// <summary>
        /// Add coins to the balance. Use for harvest income, sales, etc.
        /// </summary>
        public void Add(int amount)
        {
            if (amount <= 0) return;
            int previous = Coins;
            Coins += amount;
            OnBalanceChanged?.Invoke(previous, Coins);
        }
    }
}