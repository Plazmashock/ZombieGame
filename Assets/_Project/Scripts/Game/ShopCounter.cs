using UnityEngine;
using LastCheckout.Combat;
using LastCheckout.Interaction;
using LastCheckout.Inventory;

namespace LastCheckout.Game
{
    /// <summary>
    /// One thing for sale in the armory: a weapon on the wall, or a crate of ammo.
    /// Look at it and press E to buy. The prompt tells you the price and how much you're short.
    ///   Weapon: bought once, yours for the whole run, taken out straight away.
    ///   Ammo:   buy as many packs as you can afford (and carry).
    /// </summary>
    public class ShopCounter : MonoBehaviour, IInteractable
    {
        static readonly Color PriceColor = new Color(1f, 0.78f, 0.24f);
        static readonly Color OwnedColor = new Color(0.5f, 1f, 0.6f);
        static readonly Color TooExpensive = new Color(1f, 0.4f, 0.32f);

        WeaponSpec spec;
        bool sellsAmmo;
        TextMesh priceLabel;
        PlayerWallet playerWallet;
        PlayerShooting playerShooting;
        float nextRefresh;

        public void Setup(WeaponSpec weapon, bool ammo, TextMesh label)
        {
            spec = weapon;
            sellsAmmo = ammo;
            priceLabel = label;
        }

        int Price => sellsAmmo ? spec.ammoPrice : spec.price;

        public string GetPrompt(PlayerInteractor who)
        {
            var wallet = who.GetComponent<PlayerWallet>();
            var shooting = who.GetComponent<PlayerShooting>();
            var inventory = who.GetComponent<PlayerInventory>();
            int cash = wallet != null ? wallet.Cash : 0;

            if (!sellsAmmo && shooting != null && shooting.Owns(spec))
                return $"{spec.displayName}  ·  OWNED  ·  KEY {WeaponCatalog.GroupKey(spec.group)}";

            string text = sellsAmmo ? $"Buy {spec.ammoName} x{spec.ammoPack}  ·  ${spec.ammoPrice}" : $"Buy {spec.displayName}  ·  ${spec.price}";
            int refund = 0;
            if (!sellsAmmo && wallet != null)
            {
                // One weapon per slot (like CS): buying this one sells the one you have there for half price.
                var old = wallet.OwnedWeaponIn(spec.group);
                if (old != null && old != spec)
                {
                    refund = old.price / 2;
                    text += $"  ·  replaces {old.displayName} (+${refund})";
                }
            }
            if (cash + refund < Price) return text + $"  ·  need ${Price - cash - refund} more";
            if (sellsAmmo && inventory != null && inventory.HowManyFit(spec.ammoItem) < spec.ammoPack) return text + "  ·  backpack full";
            return text;
        }

        public bool CanInteract(PlayerInteractor who)
        {
            var wallet = who.GetComponent<PlayerWallet>();
            if (wallet == null) return false;
            int refund = 0;
            if (!sellsAmmo)
            {
                var old = wallet.OwnedWeaponIn(spec.group);
                if (old != null && old != spec) refund = old.price / 2;
            }
            if (wallet.Cash + refund < Price) return false;
            if (sellsAmmo)
            {
                var inventory = who.GetComponent<PlayerInventory>();
                return inventory != null && spec.ammoItem != null && inventory.HowManyFit(spec.ammoItem) >= spec.ammoPack;
            }
            var shooting = who.GetComponent<PlayerShooting>();
            return shooting != null && !shooting.Owns(spec);
        }

        public void Interact(PlayerInteractor who)
        {
            if (!CanInteract(who)) return;
            var wallet = who.GetComponent<PlayerWallet>();

            if (sellsAmmo)
            {
                var inventory = who.GetComponent<PlayerInventory>();
                if (wallet.TrySpend(spec.ammoPrice, $"{spec.ammoName} x{spec.ammoPack}"))
                    inventory.TryAdd(spec.ammoItem, spec.ammoPack);
            }
            else
            {
                wallet.Grant(spec.id);                       // sells the weapon you had in this slot (half price back)
                if (!wallet.TrySpend(spec.price, spec.displayName)) return;
                var shooting = who.GetComponent<PlayerShooting>();
                if (shooting != null) shooting.EquipById(spec.id); // in your hands right away
            }
            Refresh();
        }

        void Update()
        {
            if (Time.time < nextRefresh) return;
            nextRefresh = Time.time + 0.25f;
            Refresh();
        }

        /// Price tag on the wall: yellow = buyable, red = too expensive, green = OWNED.
        void Refresh()
        {
            if (priceLabel == null || spec == null) return;
            if (playerWallet == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p == null) return;
                playerWallet = p.GetComponent<PlayerWallet>();
                playerShooting = p.GetComponent<PlayerShooting>();
                if (playerWallet == null) return;
            }

            if (!sellsAmmo && playerShooting != null && playerShooting.Owns(spec))
            {
                priceLabel.text = "OWNED";
                priceLabel.color = OwnedColor;
                return;
            }
            priceLabel.text = sellsAmmo ? $"x{spec.ammoPack}  ${spec.ammoPrice}" : $"${spec.price}";
            priceLabel.color = playerWallet.Cash >= Price ? PriceColor : TooExpensive;
        }
    }
}
